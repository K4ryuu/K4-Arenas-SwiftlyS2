using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace K4Arenas;

/// <summary>
/// Database weapon preference record - Dommel entity for k4_arenas_weapons table
/// </summary>
[Table("k4_arenas_weapons")]
public sealed class DbWeaponPreference
{
	[Key]
	[Column("id")]
	public int Id { get; set; }

	[Column("steamid64")]
	public long SteamId64 { get; set; }

	[Column("weapon_type")]
	public string WeaponType { get; set; } = string.Empty;

	[Column("weapon_id")]
	public int WeaponId { get; set; }
}
