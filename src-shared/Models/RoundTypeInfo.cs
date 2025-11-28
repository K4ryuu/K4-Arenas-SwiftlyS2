using SwiftlyS2.Shared.Helpers;

namespace K4ArenaSharedApi;

public sealed class RoundTypeInfo
{
	public required int Id { get; init; }
	public required string Name { get; init; }
	public required int TeamSize { get; init; }
	public required bool EnabledByDefault { get; init; }
	public ItemDefinitionIndex? PrimaryWeapon { get; init; }
	public ItemDefinitionIndex? SecondaryWeapon { get; init; }
	public bool IsSpecialRound { get; init; }
}
