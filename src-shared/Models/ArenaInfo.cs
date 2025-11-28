namespace K4ArenaSharedApi;

public sealed class ArenaInfo
{
	public required int Id { get; init; }
	public required string DisplayName { get; init; }
	public required int Score { get; init; }
	public string? CurrentRoundTypeName { get; init; }
	public required int Team1Count { get; init; }
	public required int Team2Count { get; init; }
	public required bool IsActive { get; init; }
	public bool IsWarmup => Id == -1;
}
