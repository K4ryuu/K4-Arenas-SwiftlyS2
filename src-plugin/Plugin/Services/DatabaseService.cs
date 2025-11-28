using Dapper;
using Microsoft.Extensions.Logging;
using SwiftlyS2.Shared.Helpers;
using SwiftlyS2.Shared.SchemaDefinitions;

namespace K4Arenas;

public sealed partial class Plugin
{
	/// <summary>
	/// Handles player preference persistence in MySQL.
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

		/// <summary>Sets up DB connection and creates tables</summary>
		public async Task InitializeAsync()
		{
			try
			{
				await CreateTablesAsync();
				IsEnabled = true;
			}
			catch (Exception ex)
			{
				Core.Logger.LogError(ex, "Failed to initialize database. Player preferences will not be saved.");
				IsEnabled = false;
			}
		}

		private async Task CreateTablesAsync()
		{
			const string sql = @"
				CREATE TABLE IF NOT EXISTS `k4_arenas_players` (
					`steamid64` BIGINT UNSIGNED NOT NULL PRIMARY KEY,
					`lastseen` TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP
				);

				CREATE TABLE IF NOT EXISTS `k4_arenas_weapons` (
					`steamid64` BIGINT UNSIGNED NOT NULL,
					`weapon_type` VARCHAR(32) NOT NULL,
					`weapon_id` INT NOT NULL,
					PRIMARY KEY (`steamid64`, `weapon_type`),
					FOREIGN KEY (`steamid64`) REFERENCES `k4_arenas_players`(`steamid64`) ON DELETE CASCADE
				);

				CREATE TABLE IF NOT EXISTS `k4_arenas_rounds` (
					`steamid64` BIGINT UNSIGNED NOT NULL,
					`round_name` VARCHAR(64) NOT NULL,
					`enabled` BOOLEAN NOT NULL,
					PRIMARY KEY (`steamid64`, `round_name`),
					FOREIGN KEY (`steamid64`) REFERENCES `k4_arenas_players`(`steamid64`) ON DELETE CASCADE
				);";

			using var connection = Core.Database.GetConnection(_connectionName);
			connection.Open();
			await connection.ExecuteAsync(sql);
		}

		/// <summary>
		/// Loads player prefs from DB. Auto-cleans prefs for deleted rounds.
		/// </summary>
		public async Task<bool> LoadPlayerAsync(ArenaPlayer player)
		{
			if (!IsEnabled)
				return false;

			var steamId = player.SteamId;

			try
			{
				using var connection = Core.Database.GetConnection(_connectionName);
				connection.Open();

				// ensure player record exists
				const string upsertPlayer = @"
					INSERT INTO `k4_arenas_players` (`steamid64`)
					VALUES (@SteamId)
					ON DUPLICATE KEY UPDATE `lastseen` = CURRENT_TIMESTAMP;";

				await connection.ExecuteAsync(upsertPlayer, new { SteamId = steamId });

				// load weapon prefs
				const string selectWeapons = @"
					SELECT `weapon_type`, `weapon_id`
					FROM `k4_arenas_weapons`
					WHERE `steamid64` = @SteamId;";

				var weapons = await connection.QueryAsync<WeaponDto>(selectWeapons, new { SteamId = steamId });

				foreach (var weapon in weapons)
				{
					var weaponType = ParseWeaponType(weapon.WeaponType);
					if (weaponType.HasValue)
						player.SetWeaponPreference(weaponType.Value, (ItemDefinitionIndex)weapon.WeaponId);
				}

				// load round prefs and clean up deleted rounds
				const string selectRounds = @"
					SELECT `round_name`, `enabled`
					FROM `k4_arenas_rounds`
					WHERE `steamid64` = @SteamId;";

				var rounds = await connection.QueryAsync<RoundDto>(selectRounds, new { SteamId = steamId });
				var roundsToDelete = new List<string>();

				// start with defaults
				player.EnabledRoundTypes.Clear();
				foreach (var roundType in RoundTypes.All)
				{
					if (roundType.EnabledByDefault)
						player.EnabledRoundTypes.Add(roundType.Id);
				}

				// apply stored prefs
				foreach (var round in rounds)
				{
					var roundType = RoundTypes.GetByName(round.RoundName);

					if (roundType == null)
					{
						roundsToDelete.Add(round.RoundName);
						continue;
					}

					if (round.Enabled)
						player.EnabledRoundTypes.Add(roundType.Id);
					else
						player.EnabledRoundTypes.Remove(roundType.Id);
				}

				// clean up deleted rounds
				if (roundsToDelete.Count > 0)
				{
					const string deleteRounds = @"
						DELETE FROM `k4_arenas_rounds`
						WHERE `steamid64` = @SteamId AND `round_name` IN @RoundNames;";

					await connection.ExecuteAsync(deleteRounds, new { SteamId = steamId, RoundNames = roundsToDelete });
					Core.Logger.LogDebug("Cleaned up {Count} deleted round preferences for {SteamId}", roundsToDelete.Count, steamId);
				}

				// ensure at least one round type enabled
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

			var steamId = player.SteamId;

			try
			{
				using var connection = Core.Database.GetConnection(_connectionName);
				connection.Open();

				var weaponTypeStr = GetWeaponTypeString(weaponType);
				if (weaponTypeStr == null)
					return;

				if (weapon.HasValue)
				{
					const string upsert = @"
						INSERT INTO `k4_arenas_weapons` (`steamid64`, `weapon_type`, `weapon_id`)
						VALUES (@SteamId, @WeaponType, @WeaponId)
						ON DUPLICATE KEY UPDATE `weapon_id` = @WeaponId;";

					await connection.ExecuteAsync(upsert, new
					{
						SteamId = steamId,
						WeaponType = weaponTypeStr,
						WeaponId = (int)weapon.Value
					});
				}
				else
				{
					const string delete = @"
						DELETE FROM `k4_arenas_weapons`
						WHERE `steamid64` = @SteamId AND `weapon_type` = @WeaponType;";

					await connection.ExecuteAsync(delete, new { SteamId = steamId, WeaponType = weaponTypeStr });
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

			var steamId = player.SteamId;

			try
			{
				using var connection = Core.Database.GetConnection(_connectionName);
				connection.Open();

				if (enabled == roundType.EnabledByDefault)
				{
					// matches default, delete from DB
					const string delete = @"
						DELETE FROM `k4_arenas_rounds`
						WHERE `steamid64` = @SteamId AND `round_name` = @RoundName;";

					await connection.ExecuteAsync(delete, new { SteamId = steamId, RoundName = roundType.Name });
				}
				else
				{
					const string upsert = @"
						INSERT INTO `k4_arenas_rounds` (`steamid64`, `round_name`, `enabled`)
						VALUES (@SteamId, @RoundName, @Enabled)
						ON DUPLICATE KEY UPDATE `enabled` = @Enabled;";

					await connection.ExecuteAsync(upsert, new
					{
						SteamId = steamId,
						RoundName = roundType.Name,
						Enabled = enabled
					});
				}
			}
			catch (Exception ex)
			{
				Core.Logger.LogError(ex, "Failed to save round preference for {SteamId}", steamId);
			}
		}

		/// <summary>
		/// Removes old player records (cascades to weapons and rounds)
		/// </summary>
		public async Task PurgeOldRecordsAsync()
		{
			if (!IsEnabled || _purgeDays <= 0)
				return;

			try
			{
				const string sql = @"
					DELETE FROM `k4_arenas_players`
					WHERE `lastseen` < DATE_SUB(NOW(), INTERVAL @PurgeDays DAY);";

				using var connection = Core.Database.GetConnection(_connectionName);
				connection.Open();

				var deletedCount = await connection.ExecuteAsync(sql, new { PurgeDays = _purgeDays });

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

		private sealed class WeaponDto
		{
			public string WeaponType { get; init; } = "";
			public int WeaponId { get; init; }
		}

		private sealed class RoundDto
		{
			public string RoundName { get; init; } = "";
			public bool Enabled { get; init; }
		}
	}
}
