using SwiftlyS2.Shared.Helpers;
using SwiftlyS2.Shared.Players;

namespace K4ArenaSharedApi;

public sealed class SpecialRoundConfig
{
	public required string Name { get; init; }
	public int TeamSize { get; init; } = 1;
	public bool EnabledByDefault { get; init; } = true;
	public ItemDefinitionIndex? PrimaryWeapon { get; init; }
	public ItemDefinitionIndex? SecondaryWeapon { get; init; }
	public bool Armor { get; init; } = true;
	public bool Helmet { get; init; } = true;
	public int Health { get; init; } = 100;
	public Action<IReadOnlyList<IPlayer>?, IReadOnlyList<IPlayer>?>? OnRoundStart { get; init; }
	public Action<IReadOnlyList<IPlayer>?, IReadOnlyList<IPlayer>?>? OnRoundEnd { get; init; }
	public Action<IPlayer>? OnPlayerSpawn { get; init; }
}
