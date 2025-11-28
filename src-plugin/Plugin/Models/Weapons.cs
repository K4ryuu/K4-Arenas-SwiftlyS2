using Microsoft.Extensions.Logging;
using SwiftlyS2.Shared.Helpers;
using SwiftlyS2.Shared.SchemaDefinitions;

namespace K4Arenas;

public sealed partial class Plugin
{
	/// <summary>
	/// Static weapon helpers with lazy caching. Uses native SwiftlyS2 weapon data.
	/// </summary>
	public static class Weapons
	{
		private static Dictionary<CSWeaponType, IReadOnlyList<ItemDefinitionIndex>>? _weaponCache;
		private static IReadOnlyList<ItemDefinitionIndex>? _allPrimariesCache;

		/// <summary>Builds weapon cache - call once at plugin load</summary>
		public static void InitializeCache()
		{
			_weaponCache = [];
			_allPrimariesCache = null;

			foreach (var type in Enum.GetValues<CSWeaponType>())
			{
				if (type == CSWeaponType.WEAPONTYPE_UNKNOWN)
					continue;

				Core.Logger.LogInformation($"Weapons in enum: {Enum.GetValues<ItemDefinitionIndex>().Length}");

				var weapons = Enum.GetValues<ItemDefinitionIndex>()
					.Where(w => Core.Helpers.GetWeaponCSDataFromKey(w)?.WeaponType == type)
					.ToList();

				_weaponCache[type] = weapons;
				Core.Logger.LogInformation($"Cached {weapons.Count} weapons for type {type}");
			}

			_allPrimariesCache = [.. Enum.GetValues<ItemDefinitionIndex>()
				.Where(w =>
				{
					var vdata = Core.Helpers.GetWeaponCSDataFromKey(w);
					return vdata?.WeaponType is
						CSWeaponType.WEAPONTYPE_RIFLE or
						CSWeaponType.WEAPONTYPE_SNIPER_RIFLE or
						CSWeaponType.WEAPONTYPE_SHOTGUN or
						CSWeaponType.WEAPONTYPE_SUBMACHINEGUN or
						CSWeaponType.WEAPONTYPE_MACHINEGUN;
				})];
		}

		/// <summary>Gets weapon list for a type (cached)</summary>
		public static IReadOnlyList<ItemDefinitionIndex> GetByType(CSWeaponType type)
		{
			if (type == CSWeaponType.WEAPONTYPE_UNKNOWN)
				return GetAllPrimaries();

			if (_weaponCache?.TryGetValue(type, out var cached) == true)
				return cached;

			// fallback if cache not ready
			return [.. Enum.GetValues<ItemDefinitionIndex>().Where(w => Core.Helpers.GetWeaponCSDataFromKey((int)w)?.WeaponType == type)];
		}

		/// <summary>Gets all primary weapons (cached)</summary>
		public static IReadOnlyList<ItemDefinitionIndex> GetAllPrimaries()
		{
			if (_allPrimariesCache != null)
				return _allPrimariesCache;

			return [.. Enum.GetValues<ItemDefinitionIndex>()
				.Where(w =>
				{
					var vdata = Core.Helpers.GetWeaponCSDataFromKey((int)w);
					return vdata?.WeaponType is
						CSWeaponType.WEAPONTYPE_RIFLE or
						CSWeaponType.WEAPONTYPE_SNIPER_RIFLE or
						CSWeaponType.WEAPONTYPE_SHOTGUN or
						CSWeaponType.WEAPONTYPE_SUBMACHINEGUN or
						CSWeaponType.WEAPONTYPE_MACHINEGUN;
				})];
		}

		/// <summary>Gets all pistols (cached)</summary>
		public static IReadOnlyList<ItemDefinitionIndex> GetAllPistols() => GetByType(CSWeaponType.WEAPONTYPE_PISTOL);

		/// <summary>Gets random weapon of type</summary>
		public static ItemDefinitionIndex GetRandom(CSWeaponType type)
		{
			var weapons = GetByType(type);
			return weapons.Count > 0
				? weapons[Random.Shared.Next(weapons.Count)]
				: ItemDefinitionIndex.Ak47;
		}

		/// <summary>Parses weapon classname to ItemDefinitionIndex</summary>
		public static ItemDefinitionIndex? ParseFromString(string? weaponName)
		{
			if (string.IsNullOrEmpty(weaponName))
				return null;

			var classname = weaponName.StartsWith("weapon_", StringComparison.OrdinalIgnoreCase)
				? weaponName
				: $"weapon_{weaponName}";

			var index = Core.Helpers.GetDefinitionIndexByClassname(classname.ToLowerInvariant());
			return index.HasValue ? (ItemDefinitionIndex)index.Value : null;
		}

		/// <summary>Gets translation key for weapon type</summary>
		public static string GetTranslationKey(CSWeaponType type) => type switch
		{
			CSWeaponType.WEAPONTYPE_RIFLE => "k4.weapontype.rifle",
			CSWeaponType.WEAPONTYPE_SNIPER_RIFLE => "k4.weapontype.sniper",
			CSWeaponType.WEAPONTYPE_SHOTGUN => "k4.weapontype.shotgun",
			CSWeaponType.WEAPONTYPE_SUBMACHINEGUN => "k4.weapontype.smg",
			CSWeaponType.WEAPONTYPE_MACHINEGUN => "k4.weapontype.lmg",
			CSWeaponType.WEAPONTYPE_PISTOL => "k4.weapontype.pistol",
			_ => "k4.general.unknown"
		};
	}
}
