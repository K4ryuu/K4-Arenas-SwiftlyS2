using K4ArenaSharedApi;
using Microsoft.Extensions.Logging;
using SwiftlyS2.Shared.Helpers;
using SwiftlyS2.Shared.Players;
using SwiftlyS2.Shared.SchemaDefinitions;

namespace K4Arenas;

public sealed partial class Plugin
{
	/// <summary>
	/// Round type definition - file-based or API-registered
	/// </summary>
	public sealed class RoundType
	{
		private static int _nextId;
		private static RoundType? _warmup;

		public int Id { get; }
		public string Name { get; }
		public int TeamSize { get; }
		public bool EnabledByDefault { get; }
		public int Health { get; }
		public bool Armor { get; }
		public bool Helmet { get; }
		public ItemDefinitionIndex? PrimaryWeapon { get; }
		public ItemDefinitionIndex? SecondaryWeapon { get; }
		public bool IsSpecialRound { get; }

		// file-based only
		public CSWeaponType? PrimaryPreference { get; }
		public bool UsePreferredPrimary { get; }
		public bool UsePreferredSecondary { get; }

		// special round only
		public Action<IReadOnlyList<IPlayer>?, IReadOnlyList<IPlayer>?>? OnRoundStart { get; }
		public Action<IReadOnlyList<IPlayer>?, IReadOnlyList<IPlayer>?>? OnRoundEnd { get; }
		public Action<IPlayer>? OnPlayerSpawn { get; }

		/// <summary>Default warmup round type (player preference for both weapons)</summary>
		public static RoundType Warmup => _warmup ??= new RoundType(
			name: "warmup",
			teamSize: 1,
			usePreferredPrimary: true,
			usePreferredSecondary: true
		);

		private RoundType(string name, int teamSize = 1, bool usePreferredPrimary = false, bool usePreferredSecondary = false)
		{
			Id = -1;
			Name = name;
			TeamSize = teamSize;
			UsePreferredPrimary = usePreferredPrimary;
			UsePreferredSecondary = usePreferredSecondary;
			Armor = true;
			Helmet = true;
			Health = 100;
			EnabledByDefault = true;
		}

		/// <summary>From file config</summary>
		public RoundType(RoundFileConfig config)
		{
			Id = _nextId++;
			Name = config.Name;
			TeamSize = config.TeamSize;
			EnabledByDefault = config.EnabledByDefault;
			Health = config.Health;
			Armor = config.Armor;
			Helmet = config.Helmet;
			PrimaryWeapon = Weapons.ParseFromString(config.PrimaryWeapon);
			SecondaryWeapon = Weapons.ParseFromString(config.SecondaryWeapon);
			UsePreferredPrimary = config.UsePreferredPrimary;
			UsePreferredSecondary = config.UsePreferredSecondary;
			PrimaryPreference = config.GetPrimaryPreferenceType();
			IsSpecialRound = false;
		}

		/// <summary>From API config</summary>
		public RoundType(SpecialRoundConfig config)
		{
			Id = _nextId++;
			Name = config.Name;
			TeamSize = config.TeamSize;
			EnabledByDefault = config.EnabledByDefault;
			Health = config.Health;
			Armor = config.Armor;
			Helmet = config.Helmet;
			PrimaryWeapon = config.PrimaryWeapon;
			SecondaryWeapon = config.SecondaryWeapon;
			OnRoundStart = config.OnRoundStart;
			OnRoundEnd = config.OnRoundEnd;
			OnPlayerSpawn = config.OnPlayerSpawn;
			IsSpecialRound = true;
		}

		public RoundTypeInfo ToInfo() => new()
		{
			Id = Id,
			Name = Name,
			TeamSize = TeamSize,
			EnabledByDefault = EnabledByDefault,
			PrimaryWeapon = PrimaryWeapon,
			SecondaryWeapon = SecondaryWeapon,
			IsSpecialRound = IsSpecialRound
		};
	}

	/// <summary>Round type registry</summary>
	public static class RoundTypes
	{
		private static readonly List<RoundType> _types = [];
		private static readonly Dictionary<int, RoundType> _byId = [];
		private static readonly Dictionary<string, RoundType> _byName = [];

		public static IReadOnlyList<RoundType> All => _types;

		public static void LoadFromFiles()
		{
			Clear();
			foreach (var config in RoundConfigLoader.LoadRoundConfigs())
				Register(new RoundType(config));

			Core.Logger.LogInformation("Loaded {Count} round types.", All.Count);
		}

		public static int AddSpecialRound(SpecialRoundConfig config)
		{
			var rt = new RoundType(config);
			Register(rt);
			return rt.Id;
		}

		public static bool RemoveSpecialRound(int id)
		{
			if (!_byId.TryGetValue(id, out var rt) || !rt.IsSpecialRound)
				return false;

			_types.Remove(rt);
			_byId.Remove(id);
			_byName.Remove(rt.Name);
			return true;
		}

		public static void Clear()
		{
			_types.Clear();
			_byId.Clear();
			_byName.Clear();
		}

		public static RoundType? GetById(int id) => _byId.GetValueOrDefault(id);
		public static RoundType? GetByName(string name) => _byName.GetValueOrDefault(name);

		private static void Register(RoundType rt)
		{
			_types.Add(rt);
			_byId[rt.Id] = rt;
			_byName[rt.Name] = rt;
		}
	}
}
