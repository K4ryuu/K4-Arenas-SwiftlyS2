using SwiftlyS2.Shared.Helpers;
using SwiftlyS2.Shared.Players;

namespace K4ArenaSharedApi;

public interface IK4ArenaApi
{
	int RegisterSpecialRound(SpecialRoundConfig config);
	bool UnregisterSpecialRound(int roundId);
	IReadOnlyList<RoundTypeInfo> GetRoundTypes();
	ArenaInfo? GetPlayerArena(IPlayer player);
	PlayerState GetPlayerState(IPlayer player);
	IReadOnlyList<IPlayer> GetOpponents(IPlayer player);
	IReadOnlyList<IPlayer> GetTeammates(IPlayer player);
	int GetQueuePosition(IPlayer player);
	ItemDefinitionIndex? GetWeaponPreference(IPlayer player, WeaponType weaponType);
	void SetAfk(IPlayer player, bool afk);
	int ArenaCount { get; }
	int WaitingCount { get; }
}
