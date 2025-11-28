using K4ArenaSharedApi;
using Microsoft.Extensions.Logging;
using SwiftlyS2.Shared.Natives;
using SwiftlyS2.Shared.Players;

namespace K4Arenas;

public sealed partial class Plugin
{
	/// <summary>
	/// Spawn location with position and angle - simple struct for teleport data
	/// </summary>
	public readonly record struct SpawnLocation(Vector Position, QAngle Angle);

	/// <summary>
	/// An arena where two teams duke it out. Holds spawn points and player refs,
	/// but actual player data lives in PlayerManager (single source of truth pattern)
	/// </summary>
	public class Arena
	{
		private readonly PlayerManager _playerManager;

		/// <summary>Arena ID (0-based, -1 for warmup)</summary>
		public int Id { get; private set; } = -1;

		/// <summary>Current arena score/rank</summary>
		public int Score { get; private set; }

		/// <summary>Active round type for this match</summary>
		public RoundType? CurrentRoundType { get; private set; }

		/// <summary>Team 1 spawn points (T side)</summary>
		public IReadOnlyList<SpawnLocation> Team1Spawns { get; }

		/// <summary>Team 2 spawn points (CT side)</summary>
		public IReadOnlyList<SpawnLocation> Team2Spawns { get; }

		/// <summary>Team 1 player refs</summary>
		public List<IPlayer> Team1SteamIds { get; private set; } = [];

		/// <summary>Team 2 player refs</summary>
		public List<IPlayer> Team2SteamIds { get; private set; } = [];

		/// <summary>Result from the last round</summary>
		public ArenaResult Result { get; private set; } = ArenaResult.Empty;

		public Arena(PlayerManager playerManager, IReadOnlyList<SpawnLocation> team1Spawns, IReadOnlyList<SpawnLocation> team2Spawns)
		{
			_playerManager = playerManager;
			Team1Spawns = team1Spawns;
			Team2Spawns = team2Spawns;
		}

		/// <summary>Team 1 ArenaPlayers resolved from PlayerManager</summary>
		public IEnumerable<ArenaPlayer> Team1Players =>
			Team1SteamIds.Select(_playerManager.GetPlayer).Where(p => p != null)!;

		/// <summary>Team 2 ArenaPlayers resolved from PlayerManager</summary>
		public IEnumerable<ArenaPlayer> Team2Players =>
			Team2SteamIds.Select(_playerManager.GetPlayer).Where(p => p != null)!;

		/// <summary>All players in this arena</summary>
		public IEnumerable<ArenaPlayer> AllPlayers => Team1Players.Concat(Team2Players);

		/// <summary>True if both teams have active (non-AFK) players</summary>
		public bool IsActive =>
			Team1Players.Any(p => p.IsValid && !p.IsAfk) &&
			Team2Players.Any(p => p.IsValid && !p.IsAfk);

		/// <summary>True if round is done (one team wiped or arena inactive)</summary>
		public bool HasFinished =>
			!IsActive ||
			Team1Players.All(p => !p.IsValid || !p.IsAlive) ||
			Team2Players.All(p => !p.IsValid || !p.IsAlive);

		/// <summary>True if at least one real player (not bot) is in the arena</summary>
		public bool HasRealPlayers =>
			Team1Players.Any(p => p.IsValid && !p.Player.IsFakeClient) ||
			Team2Players.Any(p => p.IsValid && !p.Player.IsFakeClient);

		/// <summary>True if no players at all</summary>
		public bool IsEmpty => Team1SteamIds.Count == 0 && Team2SteamIds.Count == 0;

		/// <summary>
		/// Sets up a regular match with two teams
		/// </summary>
		public void SetupMatch(List<ArenaPlayer> team1, List<ArenaPlayer>? team2, RoundType roundType, int arenaId, int score = 0)
		{
			Id = arenaId;
			Score = score;
			CurrentRoundType = roundType;
			Result = ArenaResult.Empty;

			Team1SteamIds = team1.Select(p => p.Player).ToList();
			Team2SteamIds = team2?.Select(p => p.Player).ToList() ?? [];

			// randomize spawn sides for variety
			var (t1Spawns, t2Spawns) = Random.Shared.Next(2) == 0
				? (Team1Spawns, Team2Spawns)
				: (Team2Spawns, Team1Spawns);

			AssignTeamDetails(team1, t1Spawns, Team.T, 1);
			if (team2 != null)
				AssignTeamDetails(team2, t2Spawns, Team.CT, 2);
		}

		/// <summary>
		/// Sets up warmup mode (can be solo or with opponent)
		/// </summary>
		public void SetupWarmup(ArenaPlayer player1, ArenaPlayer? player2)
		{
			Id = -1;
			Score = 0;
			CurrentRoundType = null;
			Result = ArenaResult.Empty;

			Team1SteamIds = [player1.Player];
			Team2SteamIds = player2 != null ? [player2.Player] : [];

			var (t1Spawns, t2Spawns) = Random.Shared.Next(2) == 0
				? (Team1Spawns, Team2Spawns)
				: (Team2Spawns, Team1Spawns);

			AssignTeamDetails([player1], t1Spawns, Team.T, 1);
			if (player2 != null)
				AssignTeamDetails([player2], t2Spawns, Team.CT, 2);
		}

		/// <summary>
		/// Adds an opponent to a warmup arena that only has one player
		/// </summary>
		public void AddWarmupOpponent(ArenaPlayer player2)
		{
			if (Team2SteamIds.Count > 0)
				return;

			Team2SteamIds = [player2.Player];

			// figure out which spawn set team1 is using, give the other to team2
			var team1Player = Team1Players.FirstOrDefault();
			var team1UsesSpawn1 = team1Player?.SpawnLocation is { } loc &&
				Team1Spawns.Any(s => s.Position == loc.Position);

			var spawns = team1UsesSpawn1 ? Team2Spawns : Team1Spawns;
			AssignTeamDetails([player2], spawns, Team.CT, 2);
		}

		/// <summary>
		/// Removes a player from the arena
		/// </summary>
		public bool RemovePlayer(IPlayer player)
		{
			var removed = Team1SteamIds.Remove(player) || Team2SteamIds.Remove(player);

			if (removed)
			{
				var arenaPlayer = _playerManager.GetPlayer(player);
				arenaPlayer?.ClearArenaState();
			}

			return removed;
		}

		/// <summary>
		/// Gets which team a player is on (1 or 2, 0 if not found)
		/// </summary>
		public int GetPlayerTeam(IPlayer player)
		{
			if (Team1SteamIds.Contains(player)) return 1;
			if (Team2SteamIds.Contains(player)) return 2;
			return 0;
		}

		/// <summary>
		/// Gets the opponents for a player
		/// </summary>
		public IEnumerable<ArenaPlayer> GetOpponents(IPlayer player)
		{
			return GetPlayerTeam(player) switch
			{
				1 => Team2Players,
				2 => Team1Players,
				_ => []
			};
		}

		/// <summary>
		/// Sets up a player that just spawned - teleport, weapons, UI, etc.
		/// </summary>
		public void SetupPlayer(ArenaPlayer player)
		{
			if (!player.IsValid || player.SpawnLocation is null)
				return;

			player.TeleportToSpawn();

			var pawn = player.Player.PlayerPawn;
			pawn?.Health = 100;

			var roundType = CurrentRoundType ?? RoundType.Warmup;

			player.SetupWeapons(roundType);

			// Call special round OnPlayerSpawn callback
			if (roundType.IsSpecialRound)
				roundType.OnPlayerSpawn?.Invoke(player.Player);

			SendArenaMessage(player);
		}

		/// <summary>
		/// Shows arena info message to a player (called once on initial assignment, not on respawns)
		/// </summary>
		public void SendArenaMessage(ArenaPlayer player)
		{
			if (!player.IsValid || player.Player.IsFakeClient)
				return;

			if (!player.IsValid)
				return;

			var localizer = Core.Translation.GetPlayerLocalizer(player.Player);
			var opponentNames = string.Join(", ", GetOpponents(player.Player).Select(p => p.Player.Controller.PlayerName));
			var arenaName = GetDisplayName();
			var roundName = Id == -1
				? localizer["k4.general.random"]
				: localizer[CurrentRoundType?.Name ?? "k4.general.random"];

			var freezeTimeMs = (Core.ConVar.Find<int>("mp_freezetime")?.Value ?? 7) * 1000;
			player.Player.SendCenterHTML(localizer["k4.chat.arena_roundstart_html", arenaName, opponentNames, roundName], freezeTimeMs);
			player.Player.SendChat($"{localizer["k4.general.prefix"]} {localizer["k4.chat.arena_roundstart", arenaName, opponentNames, roundName]}");
		}

		/// <summary>Display name (server language)</summary>
		public string GetDisplayName() => Id == -1
			? Core.Localizer["k4.general.warmup"]
			: $"{Id + 1}";

		/// <summary>Clan tag for scoreboard</summary>
		public string GetClanTag() => Id == -1
			? $"{Core.Localizer["k4.general.warmup"]} |"
			: $"{Core.Localizer["k4.general.arena"]} {Id + 1} |";

		/// <summary>Converts to API DTO</summary>
		public ArenaInfo ToInfo() => new()
		{
			Id = Id,
			DisplayName = GetDisplayName(),
			Score = Score,
			CurrentRoundTypeName = CurrentRoundType?.Name,
			Team1Count = Team1SteamIds.Count,
			Team2Count = Team2SteamIds.Count,
			IsActive = IsActive
		};

		/// <summary>
		/// Figures out who won the round and sets the Result
		/// </summary>
		public ArenaResult DetermineResult()
		{
			var team1 = Team1Players.ToList();
			var team2 = Team2Players.ToList();

			// warmup = always tie
			if (Id == -1)
			{
				Result = team1.Count > 0 && team2.Count > 0
					? ArenaResult.Tie(team1, team2)
					: ArenaResult.Empty;
				return Result;
			}

			if (team1.Count == 0 && team2.Count == 0)
			{
				Result = ArenaResult.Empty;
				return Result;
			}

			if (team1.Count == 0 || team2.Count == 0)
			{
				var winners = team1.Count > 0 ? team1 : team2;
				Result = ArenaResult.NoOpponent(winners);
				return Result;
			}

			var team1Alive = team1.Count(p => p.IsValid && p.IsAlive);
			var team2Alive = team2.Count(p => p.IsValid && p.IsAlive);

			if (team1Alive == team2Alive)
			{
				Result = ArenaResult.Tie(team1, team2);
			}
			else
			{
				var winners = team1Alive > team2Alive ? team1 : team2;
				var losers = team1Alive > team2Alive ? team2 : team1;

				foreach (var winner in winners)
				{
					winner.MvpCount++;
				}

				Result = ArenaResult.Win(winners, losers);
			}

			return Result;
		}

		/// <summary>
		/// Resets the arena for next round
		/// </summary>
		public void Clear()
		{
			foreach (var player in AllPlayers)
			{
				player.ClearArenaState();
			}

			Team1SteamIds = [];
			Team2SteamIds = [];
			CurrentRoundType = null;
			Result = ArenaResult.Empty;
			Id = -1;
			Score = 0;
		}

		private void AssignTeamDetails(IReadOnlyList<ArenaPlayer> team, IReadOnlyList<SpawnLocation> spawns, Team switchTo, int teamNumber)
		{
			if (team.Count == 0 || spawns.Count == 0)
				return;

			var availableSpawns = spawns.ToList();

			foreach (var player in team)
			{
				if (!player.IsValid || availableSpawns.Count == 0)
					continue;

				var spawnIndex = Random.Shared.Next(availableSpawns.Count);
				var spawn = availableSpawns[spawnIndex];
				availableSpawns.RemoveAt(spawnIndex);

				player.SetInArena(this, teamNumber, spawn);

				player.PlayerIsSafe = true;
				if (player.Player.Controller?.Team > Team.Spectator)
					player.Player.SwitchTeam(switchTo);
				else
					player.Player.ChangeTeam(switchTo);
				player.PlayerIsSafe = false;
			}
		}
	}
}
