using K4ArenaSharedApi;
using Microsoft.Extensions.Logging;
using SwiftlyS2.Shared.Players;
using SwiftlyS2.Shared.SchemaDefinitions;

namespace K4Arenas;

public sealed partial class Plugin
{
	/// <summary>
	/// The brain of the arena system - handles matchmaking, round flow, and arena lifecycle.
	/// Delegates player data management to PlayerManager.
	/// </summary>
	public sealed class ArenaManager
	{
		private readonly PlayerManager _playerManager;
		private readonly SpawnFinder _spawnFinder;

		/// <summary>All arenas on current map</summary>
		public List<Arena> Arenas { get; } = [];

		/// <summary>True during round transitions</summary>
		public bool IsBetweenRounds { get; set; }

		public ArenaManager(PlayerManager playerManager)
		{
			_playerManager = playerManager;
			_spawnFinder = new SpawnFinder();
		}

		/// <summary>Sets up arenas from map spawns. Call on map start.</summary>
		public void Initialize()
		{
			if (Arenas.Count > 0)
				return;

			IsBetweenRounds = false;
			_playerManager.ClearAllArenaAssignments();

			var spawnPairs = _spawnFinder.GetArenaPairs();
			foreach (var (team1Spawns, team2Spawns) in spawnPairs)
			{
				var arena = new Arena(_playerManager, team1Spawns, team2Spawns);
				Arenas.Add(arena);
			}

			Core.Logger.LogInformation("Arena Manager initialized with {Count} arena(s).", Arenas.Count);
		}

		/// <summary>Cleans up all arenas. Call on map end.</summary>
		public void Shutdown()
		{
			foreach (var arena in Arenas)
			{
				arena.Clear();
			}

			Arenas.Clear();
			IsBetweenRounds = false;
		}

		/// <summary>Removes player from their current arena</summary>
		public void RemovePlayerFromArena(IPlayer player)
		{
			var arenaPlayer = _playerManager.GetPlayer(player);
			if (arenaPlayer == null)
				return;

			arenaPlayer.CurrentArena?.RemovePlayer(player);
			arenaPlayer.SetWaiting();
		}

		/// <summary>Gets waiting players in FIFO order (real players first)</summary>
		public List<ArenaPlayer> GetWaitingPlayersOrdered() => _playerManager.GetWaitingPlayersOrdered();

		/// <summary>Gets queue position for player (1-based, 0 if not waiting)</summary>
		public int GetQueuePosition(ArenaPlayer player) => _playerManager.GetQueuePosition(player);

		/// <summary>Total waiting players</summary>
		public int WaitingCount => _playerManager.WaitingCount;

		/// <summary>
		/// Processes end of round - determines results and sets up next round matchmaking.
		/// This is where the ladder magic happens.
		/// </summary>
		public void ProcessRoundEnd()
		{
			var winners = new List<ArenaPlayer>();
			var losers = new List<ArenaPlayer>();

			// process each arena result
			foreach (var arena in Arenas.OrderBy(a => a.Id < 0).ThenBy(a => Math.Abs(a.Id)))
			{
				if (arena.Id == -1)
				{
					AddValidPlayers(arena.Team1Players, losers);
					AddValidPlayers(arena.Team2Players, losers);
					continue;
				}

				var result = arena.DetermineResult();

				switch (result.Type)
				{
					case ArenaResultType.Win:
						AddValidPlayers(result.Winners, winners);
						AddValidPlayers(result.Losers, losers);
						break;

					case ArenaResultType.NoOpponent:
						AddValidPlayers(result.Winners, winners);
						break;

					case ArenaResultType.Tie:
						// tie = both teams go to losers (no advancement)
						AddValidPlayers(arena.Team1Players, losers);
						AddValidPlayers(arena.Team2Players, losers);
						break;

					case ArenaResultType.Empty:
						break;
				}
			}

			// clear arenas for next round
			foreach (var arena in Arenas)
			{
				arena.Clear();
			}

			// build ranked queue: winners first (pairs), then losers, then waiting
			var rankedPlayers = BuildRankedQueue(winners, losers);
			ShuffleArenas();

			// separate afk and active players
			var activePlayers = new List<ArenaPlayer>();
			var afkPlayers = new List<ArenaPlayer>();

			foreach (var player in rankedPlayers)
			{
				if (!player.IsValid)
					continue;

				if (player.IsAfk)
					afkPlayers.Add(player);
				else
					activePlayers.Add(player);
			}

			// real players before bots
			activePlayers.Sort((a, b) => a.Player.IsFakeClient.CompareTo(b.Player.IsFakeClient));

			// rebuild waiting queue
			_playerManager.ClearWaitingQueue();
			foreach (var player in activePlayers)
			{
				_playerManager.EnqueueWaiting(player);
			}

			AssignPlayersToArenas(activePlayers);

			// keep afk players afk
			foreach (var afkPlayer in afkPlayers)
			{
				afkPlayer.SetAfk();
			}
		}

		/// <summary>
		/// Fills warmup arenas with waiting players.
		/// First fills arenas with 1 player (needs opponent), then empty arenas.
		/// Kicks bots from arenas if real players are waiting and no empty arenas.
		/// </summary>
		public void PopulateWarmupMatches()
		{
			var waitingPlayers = _playerManager.GetWaitingPlayersOrdered();
			var waitingRealPlayers = waitingPlayers.Where(p => !p.Player.IsFakeClient).ToList();
			var emptyArenas = Arenas.Count(a => a.IsEmpty);

			// If real players are waiting but no empty arenas, kick bots from bot-only arenas
			if (waitingRealPlayers.Count > 0 && emptyArenas == 0)
			{
				foreach (var arena in Arenas)
				{
					if (arena.IsEmpty)
						continue;

					// Check if arena has only bots
					var hasRealPlayer = arena.AllPlayers.Any(p => p.IsValid && !p.Player.IsFakeClient);
					if (!hasRealPlayer)
					{
						// Move bots back to waiting queue
						var botsToMove = arena.AllPlayers.ToList();
						arena.Clear();

						foreach (var bot in botsToMove)
						{
							if (bot.IsValid)
							{
								bot.SetWaiting();
								_playerManager.EnqueueWaiting(bot);

								bot.PlayerIsSafe = true;
								bot.Player.ChangeTeam(Team.Spectator);
								bot.PlayerIsSafe = false;
							}
						}

						break;
					}
				}
			}

			// First fill arenas that need opponents
			foreach (var arena in Arenas)
			{
				if (arena.Team1SteamIds.Count > 0 && arena.Team2SteamIds.Count == 0)
				{
					var player2 = DequeueValidWaitingPlayer();
					if (player2 != null)
					{
						arena.AddWarmupOpponent(player2);
						RespawnPlayer(player2);
					}
				}
			}

			// Then fill empty arenas - only if we have at least 2 players (don't put someone in alone)
			var availableWaiting = _playerManager.GetWaitingPlayersOrdered().Count(p => p.IsValid && !p.IsAfk);
			if (availableWaiting < 2)
				return;

			foreach (var arena in Arenas)
			{
				if (!arena.IsEmpty)
					continue;

				var player1 = DequeueValidWaitingPlayer();
				if (player1 == null)
					break;

				var player2 = DequeueValidWaitingPlayer();
				if (player2 == null)
				{
					// Put player1 back if no opponent available
					player1.SetWaiting();
					_playerManager.EnqueueWaiting(player1);
					break;
				}

				arena.SetupWarmup(player1, player2);

				RespawnPlayer(player1);
				RespawnPlayer(player2);
			}
		}

		/// <summary>Respawns player into their assigned arena</summary>
		private static void RespawnPlayer(ArenaPlayer player)
		{
			if (!player.IsValid)
				return;

			var pawn = player.Player.PlayerPawn;
			if (pawn?.LifeState == (byte)LifeState_t.LIFE_ALIVE)
				return; // Already alive

			player.Player.Respawn();
		}

		/// <summary>Sets up player that just spawned in their arena</summary>
		public void SetupSpawnedPlayer(IPlayer player)
		{
			var arenaPlayer = _playerManager.GetPlayer(player);
			if (arenaPlayer == null)
				return;

			arenaPlayer.CurrentArena?.SetupPlayer(arenaPlayer);
		}

		/// <summary>True if all arenas with real players are done</summary>
		public bool AllArenasFinished() =>
			Arenas.All(a => !a.HasRealPlayers || a.HasFinished);

		private List<ArenaPlayer> BuildRankedQueue(List<ArenaPlayer> winners, List<ArenaPlayer> losers)
		{
			var ranked = new List<ArenaPlayer>();

			// top 2 winners stay at top
			if (winners.Count > 1)
			{
				ranked.Add(winners[0]);
				ranked.Add(winners[1]);
				winners.RemoveRange(0, 2);
			}

			// interleave remaining
			var winnerIndex = 0;
			var loserIndex = 0;

			while (winnerIndex < winners.Count)
			{
				ranked.Add(winners[winnerIndex++]);
				if (loserIndex < losers.Count)
					ranked.Add(losers[loserIndex++]);
			}

			while (loserIndex < losers.Count)
			{
				ranked.Add(losers[loserIndex++]);
			}

			// add waiting players not already ranked (real players first)
			var rankedIds = new HashSet<ulong>(ranked.Count);
			for (var i = 0; i < ranked.Count; i++)
				rankedIds.Add(ranked[i].SteamId);

			foreach (var player in _playerManager.GetWaitingPlayersOrdered())
			{
				if (!rankedIds.Contains(player.SteamId))
					ranked.Add(player);
			}

			return ranked;
		}

		private void AssignPlayersToArenas(List<ArenaPlayer> players)
		{
			var playerQueue = new Queue<ArenaPlayer>(players);
			var displayIndex = 1;

			var hasTeamRoundTypes = RoundTypes.All.Any(rt => rt.TeamSize > 1);

			for (var arenaIndex = 0; arenaIndex < Arenas.Count; arenaIndex++)
			{
				var arena = Arenas[arenaIndex];

				// try team modes first
				if (hasTeamRoundTypes)
				{
					var teamRoundTypes = RoundTypes.All.Where(rt => rt.TeamSize > 1);
					var assignedTeam = false;

					foreach (var roundType in teamRoundTypes)
					{
						if (TryAssignTeamToArena(arena, roundType, playerQueue, displayIndex))
						{
							assignedTeam = true;
							displayIndex++;
							break;
						}
					}

					if (assignedTeam)
						continue;
				}

				// standard 1v1
				if (playerQueue.Count >= 1)
				{
					var player1 = playerQueue.Dequeue();
					playerQueue.TryDequeue(out var player2);

					var roundType = FindCommonRoundType(player1, player2);
					var score = (Arenas.Count - displayIndex) * 50;

					arena.SetupMatch([player1], player2 != null ? [player2] : null, roundType, displayIndex - 1, score);
					displayIndex++;
				}
				else
				{
					arena.Clear();
				}
			}

			// remaining players stay waiting
			while (playerQueue.Count > 0)
			{
				var player = playerQueue.Dequeue();
				player.SetWaiting();
			}
		}

		private bool TryAssignTeamToArena(Arena arena, RoundType roundType, Queue<ArenaPlayer> players, int displayIndex)
		{
			var teamSize = roundType.TeamSize;
			var totalNeeded = teamSize * 2;

			if (players.Count < totalNeeded)
				return false;

			if (arena.Team1Spawns.Count < teamSize || arena.Team2Spawns.Count < teamSize)
				return false;

			var team1Preview = players.Take(teamSize).ToList();
			var team2Preview = players.Skip(teamSize).Take(teamSize).ToList();

			var allPlayers = team1Preview.Concat(team2Preview).ToList();
			var commonRoundType = ArenaPlayer.FindCommonRoundType(allPlayers);

			if (commonRoundType?.Id != roundType.Id)
				return false;

			for (var i = 0; i < totalNeeded; i++)
				players.Dequeue();

			var score = (Arenas.Count - displayIndex) * 50;
			arena.SetupMatch(team1Preview, team2Preview, roundType, displayIndex - 1, score);

			return true;
		}

		private static RoundType FindCommonRoundType(ArenaPlayer player1, ArenaPlayer? player2)
		{
			if (player2 == null)
			{
				return player1.GetRandomEnabledRoundType()
					?? RoundTypes.All.FirstOrDefault(rt => rt.TeamSize == 1)
					?? RoundTypes.All.First();
			}

			var commonType = player1.FindCommonRoundType(player2);
			if (commonType != null && commonType.TeamSize == 1)
				return commonType;

			var oneVOneTypes = RoundTypes.All.Where(rt => rt.TeamSize == 1).ToArray();
			return oneVOneTypes.Length > 0
				? oneVOneTypes[Random.Shared.Next(oneVOneTypes.Length)]
				: RoundTypes.All.First();
		}

		private ArenaPlayer? DequeueValidWaitingPlayer()
		{
			var ordered = _playerManager.GetWaitingPlayersOrdered();
			var player = ordered.FirstOrDefault(p => p.IsValid && !p.IsAfk);

			if (player != null)
				_playerManager.DequeueWaiting(player);

			return player;
		}

		private static void AddValidPlayers(IEnumerable<ArenaPlayer>? players, List<ArenaPlayer> target)
		{
			if (players == null)
				return;

			foreach (var player in players)
			{
				if (player.IsValid && !target.Any(p => p.SteamId == player.SteamId))
					target.Add(player);
			}
		}

		private void ShuffleArenas()
		{
			var n = Arenas.Count;
			while (n > 1)
			{
				n--;
				var k = Random.Shared.Next(n + 1);
				(Arenas[k], Arenas[n]) = (Arenas[n], Arenas[k]);
			}
		}
	}
}
