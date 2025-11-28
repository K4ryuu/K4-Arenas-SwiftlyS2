using K4ArenaSharedApi;
using Microsoft.Extensions.Logging;
using SwiftlyS2.Shared.Helpers;
using SwiftlyS2.Shared.Natives;
using SwiftlyS2.Shared.Players;
using SwiftlyS2.Shared.SchemaDefinitions;

namespace K4Arenas;

public sealed partial class Plugin
{
	/// <summary>
	/// Player wrapper with arena state, weapon preferences, and round type settings.
	/// The glue between IPlayer and arena system logic.
	/// </summary>
	public class ArenaPlayer
	{
		/// <summary>The actual player reference</summary>
		public IPlayer Player { get; private set; }

		/// <summary>Steam ID (stays the same even if IPlayer changes)</summary>
		public ulong SteamId { get; }

		/// <summary>Current arena system state</summary>
		public PlayerState State { get; set; } = PlayerState.Waiting;

		/// <summary>Current arena (null if waiting or AFK)</summary>
		public Arena? CurrentArena { get; set; }

		/// <summary>Team number in current arena (1 or 2)</summary>
		public int? CurrentTeam { get; set; }

		/// <summary>Assigned spawn in current arena</summary>
		public SpawnLocation? SpawnLocation { get; set; }

		/// <summary>True if DB prefs have been loaded</summary>
		public bool IsLoaded { get; set; }

		/// <summary>Flag to prevent team switch kick during arena transitions</summary>
		public bool PlayerIsSafe { get; set; }

		/// <summary>MVP count for this session</summary>
		public ushort MvpCount { get; set; }

		/// <summary>Pending setup cancellation token for debounce</summary>
		public CancellationTokenSource? PendingSetupCts { get; set; }

		/// <summary>Weapon prefs per type (null = random)</summary>
		public Dictionary<CSWeaponType, ItemDefinitionIndex?> WeaponPreferences { get; } = new()
		{
			{ CSWeaponType.WEAPONTYPE_RIFLE, null },
			{ CSWeaponType.WEAPONTYPE_SNIPER_RIFLE, null },
			{ CSWeaponType.WEAPONTYPE_SUBMACHINEGUN, null },
			{ CSWeaponType.WEAPONTYPE_MACHINEGUN, null },
			{ CSWeaponType.WEAPONTYPE_SHOTGUN, null },
			{ CSWeaponType.WEAPONTYPE_PISTOL, null }
		};

		/// <summary>Which round types are enabled for this player</summary>
		public HashSet<int> EnabledRoundTypes { get; } = [];

		public ArenaPlayer(IPlayer player)
		{
			Player = player;
			SteamId = player.SteamID;

			// start with all default rounds enabled
			foreach (var roundType in RoundTypes.All.Where(r => r.EnabledByDefault))
			{
				EnabledRoundTypes.Add(roundType.Id);
			}
		}

		/// <summary>True if player is still connected</summary>
		public bool IsValid => Player.IsValid;

		/// <summary>True if player is alive</summary>
		public bool IsAlive => Player.PlayerPawn?.Health > 0;

		/// <summary>True if AFK state</summary>
		public bool IsAfk => State == PlayerState.Afk;

		/// <summary>True if waiting in queue</summary>
		public bool IsWaiting => State == PlayerState.Waiting;

		/// <summary>Gets clan tag based on current state (arena name or waiting/afk)</summary>
		public string GetClanTag() => CurrentArena?.GetClanTag()
			?? (IsAfk ? $"{Core.Localizer["k4.general.afk"]} |" : $"{Core.Localizer["k4.general.waiting"]} |");

		/// <summary>Updates the IPlayer ref (used on reconnect)</summary>
		public void UpdatePlayerReference(IPlayer player) => Player = player;

		/// <summary>Clears all arena-specific state</summary>
		public void ClearArenaState()
		{
			CurrentArena = null;
			CurrentTeam = null;
			SpawnLocation = null;
			PlayerIsSafe = false;
		}

		/// <summary>Puts player in waiting state</summary>
		public void SetWaiting()
		{
			ClearArenaState();
			State = PlayerState.Waiting;
		}

		/// <summary>Puts player in AFK state</summary>
		public void SetAfk()
		{
			ClearArenaState();
			State = PlayerState.Afk;
		}

		/// <summary>Assigns player to an arena and team</summary>
		public void SetInArena(Arena arena, int team, SpawnLocation spawn)
		{
			State = PlayerState.InArena;
			CurrentArena = arena;
			CurrentTeam = team;
			SpawnLocation = spawn;
		}

		/// <summary>
		/// Gives weapons and applies round type settings (armor, health, gravity, speed)
		/// </summary>
		public void SetupWeapons(RoundType roundType)
		{
			if (!IsValid)
				return;

			var playerPawn = Player.PlayerPawn;
			var itemServices = playerPawn?.ItemServices;
			if (itemServices?.IsValid != true)
				return;

			itemServices.RemoveItems();

			itemServices.GiveItem("weapon_knife");

			// warmup = always random weapons
			if (CurrentArena?.Id == -1)
			{
				var primaries = Weapons.GetAllPrimaries();
				if (primaries.Count > 0)
				{
					var randomPrimary = primaries[Random.Shared.Next(primaries.Count)];
					var primaryClassname = Core.Helpers.GetClassnameByDefinitionIndex((int)randomPrimary);
					if (!string.IsNullOrEmpty(primaryClassname))
						itemServices.GiveItem(primaryClassname);
				}

				var pistols = Weapons.GetAllPistols();
				if (pistols.Count > 0)
				{
					var randomSecondary = pistols[Random.Shared.Next(pistols.Count)];
					var secondaryClassname = Core.Helpers.GetClassnameByDefinitionIndex((int)randomSecondary);
					if (!string.IsNullOrEmpty(secondaryClassname))
						itemServices.GiveItem(secondaryClassname);
				}
			}
			else
			{
				// primary weapon
				if (roundType.PrimaryWeapon is { } primary)
				{
					var classname = Core.Helpers.GetClassnameByDefinitionIndex((int)primary);
					if (!string.IsNullOrEmpty(classname))
						itemServices.GiveItem(classname);
				}
				else if (roundType.UsePreferredPrimary)
				{
					var prefType = roundType.PrimaryPreference ?? CSWeaponType.WEAPONTYPE_RIFLE;
					var preferred = WeaponPreferences.GetValueOrDefault(prefType);
					var weapon = preferred ?? Weapons.GetRandom(prefType);
					var classname = Core.Helpers.GetClassnameByDefinitionIndex((int)weapon);
					if (!string.IsNullOrEmpty(classname))
						itemServices.GiveItem(classname);
				}

				// secondary weapon
				if (roundType.SecondaryWeapon is { } secondary)
				{
					var classname = Core.Helpers.GetClassnameByDefinitionIndex((int)secondary);
					if (!string.IsNullOrEmpty(classname))
						itemServices.GiveItem(classname);
				}
				else if (roundType.UsePreferredSecondary)
				{
					var preferred = WeaponPreferences.GetValueOrDefault(CSWeaponType.WEAPONTYPE_PISTOL);
					var weapon = preferred ?? Weapons.GetRandom(CSWeaponType.WEAPONTYPE_PISTOL);
					var classname = Core.Helpers.GetClassnameByDefinitionIndex((int)weapon);
					if (!string.IsNullOrEmpty(classname))
						itemServices.GiveItem(classname);
				}
			}

			// apply health/armor/gravity/speed on next frame
			Core.Scheduler.NextWorldUpdate(() =>
			{
				if (!IsValid)
					return;

				var pawn = Player.PlayerPawn;
				if (pawn == null)
					return;

				pawn.Health = roundType.Health;
				pawn.HealthUpdated();

				pawn.MaxHealth = roundType.Health;
				pawn.MaxHealthUpdated();

				pawn.ArmorValue = roundType.Armor ? 100 : 0;
				pawn.ArmorValueUpdated();

				if (pawn.ItemServices is CCSPlayer_ItemServices csItemServices)
				{
					csItemServices.HasHelmet = roundType.Helmet;
					csItemServices.HasHelmetUpdated();
				}
			});
		}

		/// <summary>Teleports player to their assigned spawn</summary>
		public void TeleportToSpawn()
		{
			if (!IsValid || SpawnLocation is not { } spawn)
				return;

			Player.Teleport(spawn.Position, spawn.Angle, Vector.Zero);
		}

		/// <summary>
		/// Toggles a round type on/off. Returns new state, or null if can't disable (last one)
		/// </summary>
		public bool? ToggleRoundType(RoundType roundType)
		{
			if (EnabledRoundTypes.Contains(roundType.Id))
			{
				if (EnabledRoundTypes.Count <= 1)
					return null;

				EnabledRoundTypes.Remove(roundType.Id);
				return false;
			}

			EnabledRoundTypes.Add(roundType.Id);
			return true;
		}

		/// <summary>Sets weapon preference for a type</summary>
		public void SetWeaponPreference(CSWeaponType type, ItemDefinitionIndex? weapon) =>
			WeaponPreferences[type] = weapon;

		/// <summary>Checks if round type is enabled</summary>
		public bool HasRoundTypeEnabled(RoundType roundType) =>
			EnabledRoundTypes.Contains(roundType.Id);

		/// <summary>Gets a random enabled round type for matchmaking</summary>
		public RoundType? GetRandomEnabledRoundType()
		{
			if (EnabledRoundTypes.Count == 0)
				return null;

			var ids = EnabledRoundTypes.ToArray();
			var randomId = ids[Random.Shared.Next(ids.Length)];
			return RoundTypes.GetById(randomId);
		}

		/// <summary>Finds a round type both players have enabled</summary>
		public RoundType? FindCommonRoundType(ArenaPlayer other)
		{
			var commonIds = EnabledRoundTypes.Where(other.EnabledRoundTypes.Contains).ToArray();
			if (commonIds.Length == 0)
				return null;

			var randomId = commonIds[Random.Shared.Next(commonIds.Length)];
			return RoundTypes.GetById(randomId);
		}

		/// <summary>Finds a round type all players have enabled</summary>
		public static RoundType? FindCommonRoundType(IEnumerable<ArenaPlayer> players)
		{
			using var enumerator = players.GetEnumerator();
			if (!enumerator.MoveNext())
				return null;

			var commonIds = new HashSet<int>(enumerator.Current.EnabledRoundTypes);

			while (enumerator.MoveNext())
			{
				commonIds.IntersectWith(enumerator.Current.EnabledRoundTypes);
				if (commonIds.Count == 0)
					return null;
			}

			if (commonIds.Count == 0)
				return null;

			var ids = commonIds.ToArray();
			var randomId = ids[Random.Shared.Next(ids.Length)];
			return RoundTypes.GetById(randomId);
		}
	}
}
