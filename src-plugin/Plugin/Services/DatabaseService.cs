using Dommel;
using K4Arenas.Database.Migrations;
using Microsoft.Extensions.Logging;
using SwiftlyS2.Shared.Helpers;
using SwiftlyS2.Shared.SchemaDefinitions;

namespace K4Arenas;

public sealed partial class Plugin
{
	/// <summary>
	/// Handles player preference persistence in database (MySQL, PostgreSQL, SQLite)
	/// Only stores non-default values - if preference matches default, it's deleted.
	/// </summary>
	public sealed class DatabaseService
	{
		private readonly string _connectionName;
		private readonly int _purgeDays;

		/// <summary>True if DB is configured and ready</summary>
		public bool IsEnabled { get; private set; }

		public DatabaseService(string connectionName, int purgeDays)
		{
			_connectionName = connectionName;
			_purgeDays = purgeDays;
		}

		/// <summary>Sets up DB connection and runs migrations</summary>
		public async Task InitializeAsync()
		{
			try
			{
				using var connection = Core.Database.GetConnection(_connectionName);
				MigrationRunner.RunMigrations(connection);
				IsEnabled = true;
				Core.Logger.LogInformation("Database initialized with migrations.");
			}
			catch (Exception ex)
			{
				Core.Logger.LogError(ex, "Failed to initialize database. Player preferences will not be saved.");
				IsEnabled = false;
			}
		}

		/// <summary>
		/// Loads player prefs from DB. Auto-cleans prefs for deleted rounds.
		/// </summary>
		public async Task<bool> LoadPlayerAsync(ArenaPlayer player)
		{
			if (!IsEnabled)
				return false;

			var steamId = (long)player.SteamId;

			try
			{
				using var connection = Core.Database.GetConnection(_connectionName);
				connection.Open();

				// Check if player exists
				var dbPlayer = await connection.FirstOrDefaultAsync<DbPlayer>(p => p.SteamId64 == steamId);

				if (dbPlayer == null)
				{
					// Create new player record
					dbPlayer = new DbPlayer
					{
						SteamId64 = steamId,
						LastSeen = DateTime.UtcNow
					};
					await connection.InsertAsync(dbPlayer);
				}
				else
				{
					// Update last seen
					dbPlayer.LastSeen = DateTime.UtcNow;
					await connection.UpdateAsync(dbPlayer);
				}

				// Load weapon prefs
				var weapons = await connection.SelectAsync<DbWeaponPreference>(w => w.SteamId64 == steamId);

				foreach (var weapon in weapons)
				{
					var weaponType = ParseWeaponType(weapon.WeaponType);
					if (weaponType.HasValue)
						player.SetWeaponPreference(weaponType.Value, (ItemDefinitionIndex)weapon.WeaponId);
				}

				// Load round prefs and clean up deleted rounds
				var rounds = await connection.SelectAsync<DbRoundPreference>(r => r.SteamId64 == steamId);
				var roundsToDelete = new List<int>();

				// Start with defaults
				player.EnabledRoundTypes.Clear();
				foreach (var roundType in RoundTypes.All)
				{
					if (roundType.EnabledByDefault)
						player.EnabledRoundTypes.Add(roundType.Id);
				}

				// Apply stored prefs
				foreach (var round in rounds)
				{
					var roundType = RoundTypes.GetByName(round.RoundName);

					if (roundType == null)
					{
						roundsToDelete.Add(round.Id);
						continue;
					}

					if (round.Enabled)
						player.EnabledRoundTypes.Add(roundType.Id);
					else
						player.EnabledRoundTypes.Remove(roundType.Id);
				}

				// Clean up deleted rounds
				if (roundsToDelete.Count > 0)
				{
					foreach (var id in roundsToDelete)
					{
						var toDelete = await connection.GetAsync<DbRoundPreference>(id);
						if (toDelete != null)
							await connection.DeleteAsync(toDelete);
					}
					Core.Logger.LogDebug("Cleaned up {Count} deleted round preferences for {SteamId}", roundsToDelete.Count, steamId);
				}

				// Ensure at least one round type enabled
				if (player.EnabledRoundTypes.Count == 0)
				{
					foreach (var rt in RoundTypes.All)
					{
						player.EnabledRoundTypes.Add(rt.Id);
						break;
					}
				}

				player.IsLoaded = true;
				return true;
			}
			catch (Exception ex)
			{
				Core.Logger.LogError(ex, "Failed to load player preferences for {SteamId}", steamId);
			}

			return false;
		}

		/// <summary>
		/// Saves weapon pref. Deletes from DB if set to default (random).
		/// </summary>
		public async Task SaveWeaponPreferenceAsync(ArenaPlayer player, CSWeaponType weaponType, ItemDefinitionIndex? weapon)
		{
			if (!IsEnabled || !player.IsLoaded)
				return;

			var steamId = (long)player.SteamId;

			try
			{
				using var connection = Core.Database.GetConnection(_connectionName);
				connection.Open();

				var weaponTypeStr = GetWeaponTypeString(weaponType);
				if (weaponTypeStr == null)
					return;

				// Find existing preference
				var existing = (await connection.SelectAsync<DbWeaponPreference>(w =>
					w.SteamId64 == steamId && w.WeaponType == weaponTypeStr)).FirstOrDefault();

				if (weapon.HasValue)
				{
					if (existing != null)
					{
						// Update existing
						existing.WeaponId = (int)weapon.Value;
						await connection.UpdateAsync(existing);
					}
					else
					{
						// Insert new
						var newPref = new DbWeaponPreference
						{
							SteamId64 = steamId,
							WeaponType = weaponTypeStr,
							WeaponId = (int)weapon.Value
						};
						await connection.InsertAsync(newPref);
					}
				}
				else if (existing != null)
				{
					// Delete if set to random (default)
					await connection.DeleteAsync(existing);
				}
			}
			catch (Exception ex)
			{
				Core.Logger.LogError(ex, "Failed to save weapon preference for {SteamId}", steamId);
			}
		}

		/// <summary>
		/// Saves round pref. Only stores if different from EnabledByDefault.
		/// </summary>
		public async Task SaveRoundPreferenceAsync(ArenaPlayer player, RoundType roundType, bool enabled)
		{
			if (!IsEnabled || !player.IsLoaded)
				return;

			var steamId = (long)player.SteamId;

			try
			{
				using var connection = Core.Database.GetConnection(_connectionName);
				connection.Open();

				// Find existing preference
				var existing = (await connection.SelectAsync<DbRoundPreference>(r =>
					r.SteamId64 == steamId && r.RoundName == roundType.Name)).FirstOrDefault();

				if (enabled == roundType.EnabledByDefault)
				{
					// Matches default, delete from DB
					if (existing != null)
					{
						await connection.DeleteAsync(existing);
					}
				}
				else
				{
					if (existing != null)
					{
						// Update existing
						existing.Enabled = enabled;
						await connection.UpdateAsync(existing);
					}
					else
					{
						// Insert new
						var newPref = new DbRoundPreference
						{
							SteamId64 = steamId,
							RoundName = roundType.Name,
							Enabled = enabled
						};
						await connection.InsertAsync(newPref);
					}
				}
			}
			catch (Exception ex)
			{
				Core.Logger.LogError(ex, "Failed to save round preference for {SteamId}", steamId);
			}
		}

		/// <summary>
		/// Removes old player records and their associated preferences
		/// </summary>
		public async Task PurgeOldRecordsAsync()
		{
			if (!IsEnabled || _purgeDays <= 0)
				return;

			try
			{
				using var connection = Core.Database.GetConnection(_connectionName);
				connection.Open();

				var cutoffDate = DateTime.UtcNow.AddDays(-_purgeDays);
				var oldPlayers = await connection.SelectAsync<DbPlayer>(p => p.LastSeen < cutoffDate);
				var deletedCount = 0;

				foreach (var player in oldPlayers)
				{
					// Delete associated weapon preferences
					var weapons = await connection.SelectAsync<DbWeaponPreference>(w => w.SteamId64 == player.SteamId64);
					foreach (var weapon in weapons)
						await connection.DeleteAsync(weapon);

					// Delete associated round preferences
					var rounds = await connection.SelectAsync<DbRoundPreference>(r => r.SteamId64 == player.SteamId64);
					foreach (var round in rounds)
						await connection.DeleteAsync(round);

					// Delete player
					await connection.DeleteAsync(player);
					deletedCount++;
				}

				if (deletedCount > 0)
					Core.Logger.LogInformation("Purged {Count} old player records from database.", deletedCount);
			}
			catch (Exception ex)
			{
				Core.Logger.LogError(ex, "Failed to purge old records from database.");
			}
		}

		private static string? GetWeaponTypeString(CSWeaponType type) => type switch
		{
			CSWeaponType.WEAPONTYPE_RIFLE => "rifle",
			CSWeaponType.WEAPONTYPE_SNIPER_RIFLE => "sniper",
			CSWeaponType.WEAPONTYPE_SHOTGUN => "shotgun",
			CSWeaponType.WEAPONTYPE_SUBMACHINEGUN => "smg",
			CSWeaponType.WEAPONTYPE_MACHINEGUN => "lmg",
			CSWeaponType.WEAPONTYPE_PISTOL => "pistol",
			_ => null
		};

		private static CSWeaponType? ParseWeaponType(string type) => type switch
		{
			"rifle" => CSWeaponType.WEAPONTYPE_RIFLE,
			"sniper" => CSWeaponType.WEAPONTYPE_SNIPER_RIFLE,
			"shotgun" => CSWeaponType.WEAPONTYPE_SHOTGUN,
			"smg" => CSWeaponType.WEAPONTYPE_SUBMACHINEGUN,
			"lmg" => CSWeaponType.WEAPONTYPE_MACHINEGUN,
			"pistol" => CSWeaponType.WEAPONTYPE_PISTOL,
			_ => null
		};
	}
}
