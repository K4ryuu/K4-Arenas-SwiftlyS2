using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace K4Arenas;

/// <summary>
/// Database player record - Dommel entity for k4_arenas_players table
/// </summary>
[Table("k4_arenas_players")]
public sealed class DbPlayer
{
	[Key]
	[Column("steamid64")]
	public long SteamId64 { get; set; }

	[Column("lastseen")]
	public DateTime LastSeen { get; set; }
}
