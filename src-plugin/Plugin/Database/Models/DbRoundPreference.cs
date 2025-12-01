using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace K4Arenas;

/// <summary>
/// Database round preference record - Dommel entity for k4_arenas_rounds table
/// </summary>
[Table("k4_arenas_rounds")]
public sealed class DbRoundPreference
{
	[Key]
	[Column("id")]
	public int Id { get; set; }

	[Column("steamid64")]
	public long SteamId64 { get; set; }

	[Column("round_name")]
	public string RoundName { get; set; } = string.Empty;

	[Column("enabled")]
	public bool Enabled { get; set; }
}
