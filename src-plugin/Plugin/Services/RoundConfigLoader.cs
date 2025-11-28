using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using SwiftlyS2.Shared.SchemaDefinitions;

namespace K4Arenas;

public sealed partial class Plugin
{
	/// <summary>
	/// Loads round configs from JSONC files in resources/rounds/
	/// </summary>
	public sealed class RoundConfigLoader
	{
		private static readonly JsonSerializerOptions JsonOptions = new()
		{
			PropertyNameCaseInsensitive = true,
			ReadCommentHandling = JsonCommentHandling.Skip,
			AllowTrailingCommas = true,
			Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
		};

		/// <summary>Loads all round configs from resources/rounds/</summary>
		public static List<RoundFileConfig> LoadRoundConfigs()
		{
			var configs = new List<RoundFileConfig>();
			var roundsPath = Path.Combine(Core.PluginPath, "resources", "rounds");

			if (!Directory.Exists(roundsPath))
			{
				Core.Logger.LogError("Rounds directory not found at {Path}. No rounds will be loaded.", roundsPath);
				return configs;
			}

			var files = Directory.GetFiles(roundsPath, "*.jsonc");

			if (files.Length == 0)
			{
				Core.Logger.LogWarning("No round configuration files found in {Path}.", roundsPath);
				return configs;
			}

			foreach (var file in files)
			{
				try
				{
					var config = LoadRoundFile(file);
					if (config != null)
						configs.Add(config);
				}
				catch (Exception ex)
				{
					Core.Logger.LogError(ex, "Failed to load round config from {File}", file);
				}
			}

			Core.Logger.LogInformation("Loaded {Count} round configuration(s) from {Path}", configs.Count, roundsPath);
			return configs;
		}

		private static RoundFileConfig? LoadRoundFile(string filePath)
		{
			var jsonContent = File.ReadAllText(filePath);
			return JsonSerializer.Deserialize<RoundFileConfig>(jsonContent, JsonOptions);
		}
	}

	/// <summary>
	/// Round config model matching the JSONC file structure
	/// </summary>
	public sealed class RoundFileConfig
	{
		/// <summary>Translation key for round name</summary>
		public required string Name { get; set; }

		/// <summary>Team size (1 = 1v1, 2 = 2v2, etc.)</summary>
		public int TeamSize { get; set; } = 1;

		/// <summary>Enabled by default for new players</summary>
		public bool EnabledByDefault { get; set; } = true;

		/// <summary>Fixed primary weapon (null = use preference)</summary>
		public string? PrimaryWeapon { get; set; }

		/// <summary>Fixed secondary weapon (null = use preference)</summary>
		public string? SecondaryWeapon { get; set; }

		/// <summary>Use player's preferred primary</summary>
		public bool UsePreferredPrimary { get; set; }

		/// <summary>Use player's preferred secondary</summary>
		public bool UsePreferredSecondary { get; set; }

		/// <summary>Primary preference type (RIFLE, SNIPER, SMG, etc.)</summary>
		public string? PrimaryPreference { get; set; }

		/// <summary>Give armor</summary>
		public bool Armor { get; set; } = true;

		/// <summary>Give helmet</summary>
		public bool Helmet { get; set; } = true;

		/// <summary>Player health</summary>
		public int Health { get; set; } = 100;

		/// <summary>Parses PrimaryPreference string to CSWeaponType</summary>
		public CSWeaponType? GetPrimaryPreferenceType()
		{
			if (string.IsNullOrEmpty(PrimaryPreference))
				return null;

			return PrimaryPreference.ToUpperInvariant() switch
			{
				"RIFLE" => CSWeaponType.WEAPONTYPE_RIFLE,
				"SNIPER_RIFLE" or "SNIPER" => CSWeaponType.WEAPONTYPE_SNIPER_RIFLE,
				"SHOTGUN" => CSWeaponType.WEAPONTYPE_SHOTGUN,
				"SMG" or "SUBMACHINEGUN" => CSWeaponType.WEAPONTYPE_SUBMACHINEGUN,
				"LMG" or "MACHINEGUN" => CSWeaponType.WEAPONTYPE_MACHINEGUN,
				"PISTOL" => CSWeaponType.WEAPONTYPE_PISTOL,
				_ => null
			};
		}
	}
}
