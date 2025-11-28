using K4ArenaSharedApi;

namespace K4Arenas;

public sealed partial class Plugin
{
	/// <summary>
	/// Round result - who won, who lost, or tie/empty
	/// </summary>
	public readonly record struct ArenaResult(ArenaResultType Type, IReadOnlyList<ArenaPlayer>? Winners = null, IReadOnlyList<ArenaPlayer>? Losers = null)
	{
		public static ArenaResult Empty => new(ArenaResultType.Empty);

		public static ArenaResult Tie(IReadOnlyList<ArenaPlayer> team1, IReadOnlyList<ArenaPlayer> team2) =>
			new(ArenaResultType.Tie, team1, team2);

		public static ArenaResult Win(IReadOnlyList<ArenaPlayer> winners, IReadOnlyList<ArenaPlayer> losers) =>
			new(ArenaResultType.Win, winners, losers);

		public static ArenaResult NoOpponent(IReadOnlyList<ArenaPlayer> winners) =>
			new(ArenaResultType.NoOpponent, winners);
	}
}
