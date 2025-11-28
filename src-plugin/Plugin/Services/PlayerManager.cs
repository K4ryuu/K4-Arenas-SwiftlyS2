using System.Collections.Concurrent;
using K4ArenaSharedApi;
using SwiftlyS2.Shared.Players;

namespace K4Arenas;

public sealed partial class Plugin
{
	/// <summary>
	/// Single source of truth for player data. Tracks all players in the arena system.
	/// Players stay here until disconnect - arenas only hold references.
	/// </summary>
	public sealed class PlayerManager
	{
		private readonly ConcurrentDictionary<int, ArenaPlayer> _players = new();
		private readonly List<int> _waitingQueue = []; // slot order = FIFO

		/// <summary>Total tracked players</summary>
		public int Count => _players.Count;

		/// <summary>
		/// Adds or updates a player. If exists, updates IPlayer ref.
		/// </summary>
		public ArenaPlayer AddOrUpdatePlayer(IPlayer player)
		{
			return _players.AddOrUpdate(player.PlayerID, _ => new ArenaPlayer(player), (_, existing) =>
			{
				existing.UpdatePlayerReference(player);
				return existing;
			});
		}

		/// <summary>Removes player (call only on disconnect)</summary>
		public bool RemovePlayer(IPlayer player)
		{
			_waitingQueue.Remove(player.PlayerID);
			return _players.TryRemove(player.PlayerID, out _);
		}

		/// <summary>Gets player by IPlayer ref (O(1))</summary>
		public ArenaPlayer? GetPlayer(IPlayer player) =>
			_players.TryGetValue(player.PlayerID, out var arenaPlayer) ? arenaPlayer : null;

		/// <summary>Checks if player exists</summary>
		public bool HasPlayer(IPlayer player) => _players.ContainsKey(player.PlayerID);

		/// <summary>All tracked players</summary>
		public IEnumerable<ArenaPlayer> GetAllPlayers() => _players.Values;

		/// <summary>All valid (connected) players</summary>
		public IEnumerable<ArenaPlayer> GetValidPlayers() =>
			_players.Values.Where(p => p.IsValid);

		/// <summary>Players in waiting state (unordered)</summary>
		public IEnumerable<ArenaPlayer> GetWaitingPlayers() =>
			_players.Values.Where(p => p.IsValid && p.State == PlayerState.Waiting);

		/// <summary>
		/// Waiting players in FIFO order. Real players come before bots.
		/// </summary>
		public List<ArenaPlayer> GetWaitingPlayersOrdered()
		{
			var result = new List<ArenaPlayer>();
			var bots = new List<ArenaPlayer>();

			foreach (var slot in _waitingQueue)
			{
				if (_players.TryGetValue(slot, out var player) && player.IsValid && player.State == PlayerState.Waiting)
				{
					if (player.Player.IsFakeClient)
						bots.Add(player);
					else
						result.Add(player);
				}
			}

			result.AddRange(bots);
			return result;
		}

		/// <summary>Adds player to waiting queue</summary>
		public void EnqueueWaiting(ArenaPlayer player)
		{
			if (!_waitingQueue.Contains(player.Player.PlayerID))
				_waitingQueue.Add(player.Player.PlayerID);
		}

		/// <summary>Removes player from waiting queue</summary>
		public void DequeueWaiting(ArenaPlayer player) =>
			_waitingQueue.Remove(player.Player.PlayerID);

		/// <summary>Gets queue position (1-based, 0 if not waiting)</summary>
		public int GetQueuePosition(ArenaPlayer player)
		{
			if (player.State != PlayerState.Waiting)
				return 0;

			var ordered = GetWaitingPlayersOrdered();
			var index = ordered.FindIndex(p => p.Player.PlayerID == player.Player.PlayerID);
			return index >= 0 ? index + 1 : 0;
		}

		/// <summary>Total waiting players</summary>
		public int WaitingCount => _waitingQueue.Count(slot =>
			_players.TryGetValue(slot, out var p) && p.IsValid && p.State == PlayerState.Waiting);

		/// <summary>Clears the waiting queue</summary>
		public void ClearWaitingQueue() => _waitingQueue.Clear();

		/// <summary>Players currently in arenas</summary>
		public IEnumerable<ArenaPlayer> GetPlayersInArenas() =>
			_players.Values.Where(p => p.IsValid && p.State == PlayerState.InArena);

		/// <summary>Players in a specific arena</summary>
		public IEnumerable<ArenaPlayer> GetPlayersInArena(Arena arena) =>
			_players.Values.Where(p => p.IsValid && p.CurrentArena == arena);

		/// <summary>Players in specific arena and team</summary>
		public IEnumerable<ArenaPlayer> GetPlayersInArenaTeam(Arena arena, int team) =>
			_players.Values.Where(p => p.IsValid && p.CurrentArena == arena && p.CurrentTeam == team);

		/// <summary>All AFK players</summary>
		public IEnumerable<ArenaPlayer> GetAfkPlayers() =>
			_players.Values.Where(p => p.IsValid && p.State == PlayerState.Afk);

		/// <summary>All non-AFK valid players</summary>
		public IEnumerable<ArenaPlayer> GetActivePlayers() =>
			_players.Values.Where(p => p.IsValid && p.State != PlayerState.Afk);

		/// <summary>Count of players in a specific state</summary>
		public int GetPlayerCountByState(PlayerState state) =>
			_players.Values.Count(p => p.IsValid && p.State == state);

		/// <summary>Resets all players to waiting (call on map change)</summary>
		public void ClearAllArenaAssignments()
		{
			foreach (var player in _players.Values)
			{
				if (player.IsValid)
					player.SetWaiting();
			}
		}

		/// <summary>Clears everything (call on plugin unload)</summary>
		public void Clear()
		{
			_waitingQueue.Clear();
			_players.Clear();
		}
	}
}
