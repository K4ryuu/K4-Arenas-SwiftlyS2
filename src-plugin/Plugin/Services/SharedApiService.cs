using K4ArenaSharedApi;
using SwiftlyS2.Shared.Helpers;
using SwiftlyS2.Shared.Players;
using SwiftlyS2.Shared.SchemaDefinitions;

namespace K4Arenas;

public sealed partial class Plugin
{
	/// <summary>IK4ArenaApi implementation for external plugins</summary>
	internal sealed class K4ArenaApiService(PlayerManager playerManager, ArenaManager arenaManager) : IK4ArenaApi
	{
		public int RegisterSpecialRound(SpecialRoundConfig config) =>
			string.IsNullOrWhiteSpace(config.Name) ? -1 : RoundTypes.AddSpecialRound(config);

		public bool UnregisterSpecialRound(int roundId) => RoundTypes.RemoveSpecialRound(roundId);

		public IReadOnlyList<RoundTypeInfo> GetRoundTypes() =>
			[.. RoundTypes.All.Select(rt => rt.ToInfo())];

		public ArenaInfo? GetPlayerArena(IPlayer player) =>
			playerManager.GetPlayer(player)?.CurrentArena?.ToInfo();

		public PlayerState GetPlayerState(IPlayer player) =>
			playerManager.GetPlayer(player)?.State ?? PlayerState.Waiting;

		public IReadOnlyList<IPlayer> GetOpponents(IPlayer player)
		{
			var arena = playerManager.GetPlayer(player)?.CurrentArena;
			return arena == null ? [] : [.. arena.GetOpponents(player).Where(p => p.IsValid).Select(p => p.Player)];
		}

		public IReadOnlyList<IPlayer> GetTeammates(IPlayer player)
		{
			var ap = playerManager.GetPlayer(player);
			if (ap?.CurrentArena is not { } arena || ap.CurrentTeam is not { } team)
				return [];

			var teammates = team == 1 ? arena.Team1Players : arena.Team2Players;
			return [.. teammates.Where(p => p.IsValid && p.Player != player).Select(p => p.Player)];
		}

		public int GetQueuePosition(IPlayer player)
		{
			var ap = playerManager.GetPlayer(player);
			return ap != null ? arenaManager.GetQueuePosition(ap) : 0;
		}

		public ItemDefinitionIndex? GetWeaponPreference(IPlayer player, WeaponType weaponType)
		{
			var ap = playerManager.GetPlayer(player);
			if (ap == null) return null;

			var csType = weaponType switch
			{
				WeaponType.Rifle => CSWeaponType.WEAPONTYPE_RIFLE,
				WeaponType.Sniper => CSWeaponType.WEAPONTYPE_SNIPER_RIFLE,
				WeaponType.SMG => CSWeaponType.WEAPONTYPE_SUBMACHINEGUN,
				WeaponType.LMG => CSWeaponType.WEAPONTYPE_MACHINEGUN,
				WeaponType.Shotgun => CSWeaponType.WEAPONTYPE_SHOTGUN,
				WeaponType.Pistol => CSWeaponType.WEAPONTYPE_PISTOL,
				_ => CSWeaponType.WEAPONTYPE_UNKNOWN
			};

			return ap.WeaponPreferences.GetValueOrDefault(csType);
		}

		public void SetAfk(IPlayer player, bool afk)
		{
			var ap = playerManager.GetPlayer(player);
			if (ap == null) return;

			if (afk)
			{
				arenaManager.RemovePlayerFromArena(player);
				ap.SetAfk();
			}
			else
			{
				ap.SetWaiting();
				playerManager.EnqueueWaiting(ap);
			}
		}

		public int ArenaCount => arenaManager.Arenas.Count;
		public int WaitingCount => arenaManager.WaitingCount;
	}
}
