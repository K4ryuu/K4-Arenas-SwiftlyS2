using K4ArenaSharedApi;
using Microsoft.Extensions.Logging;
using SwiftlyS2.Shared;
using SwiftlyS2.Shared.Helpers;
using SwiftlyS2.Shared.Players;
using SwiftlyS2.Shared.Plugins;

namespace K4Arenas_ExampleRound;

/// <summary>
/// Example plugin showing how to register custom arena rounds via the K4-Arenas API
/// </summary>
public class ExampleRoundPlugin(ISwiftlyCore core) : BasePlugin(core)
{
	private IK4ArenaApi? _arenaApi;
	private int _oneHpRoundId = -1;
	private int _highGravityRoundId = -1;

	public override void UseSharedInterface(IInterfaceManager interfaceManager)
	{
		if (!interfaceManager.HasSharedInterface("K4Arena.Api.v1"))
		{
			Core.Logger.LogWarning("K4-Arenas API not found! Make sure K4-Arenas plugin is loaded.");
			return;
		}

		_arenaApi = interfaceManager.GetSharedInterface<IK4ArenaApi>("K4Arena.Api.v1");
	}

	/// <summary>Called after all shared interfaces are injected - safe to use the API here</summary>
	public override void OnSharedInterfaceInjected(IInterfaceManager interfaceManager)
	{
		if (_arenaApi == null)
			return;

		RegisterCustomRounds();
	}

	public override void Load(bool hotReload)
	{
		// API registration happens in OnSharedInterfaceInjected
	}

	public override void Unload()
	{
		if (_arenaApi != null)
		{
			if (_oneHpRoundId >= 0)
				_arenaApi.UnregisterSpecialRound(_oneHpRoundId);

			if (_highGravityRoundId >= 0)
				_arenaApi.UnregisterSpecialRound(_highGravityRoundId);
		}
	}

	private void RegisterCustomRounds()
	{
		if (_arenaApi == null)
			return;

		// One HP Round - deagle only, 1 HP
		_oneHpRoundId = _arenaApi.RegisterSpecialRound(new SpecialRoundConfig
		{
			Name = "k4.rounds.onehp",
			TeamSize = 1,
			EnabledByDefault = true,
			PrimaryWeapon = ItemDefinitionIndex.Deagle,
			SecondaryWeapon = null,
			Armor = false,
			Helmet = false,
			Health = 1,
			OnRoundStart = OnOneHpRoundStart,
			OnRoundEnd = OnOneHpRoundEnd,
			OnPlayerSpawn = OnOneHpPlayerSpawn
		});

		Core.Logger.LogInformation("Registered One HP round with ID: {Id}", _oneHpRoundId);

		// High Gravity Round - 3x gravity
		_highGravityRoundId = _arenaApi.RegisterSpecialRound(new SpecialRoundConfig
		{
			Name = "k4.rounds.highgravity",
			TeamSize = 1,
			EnabledByDefault = true,
			PrimaryWeapon = ItemDefinitionIndex.Ak47,
			SecondaryWeapon = ItemDefinitionIndex.Deagle,
			Armor = true,
			Helmet = true,
			Health = 100,
			OnPlayerSpawn = OnHighGravityPlayerSpawn
		});

		Core.Logger.LogInformation("Registered High Gravity round with ID: {Id}", _highGravityRoundId);
	}

	#region One HP Round

	private void OnOneHpRoundStart(IReadOnlyList<IPlayer>? team1, IReadOnlyList<IPlayer>? team2)
	{
		var allPlayers = new List<IPlayer>();
		if (team1 != null) allPlayers.AddRange(team1);
		if (team2 != null) allPlayers.AddRange(team2);

		foreach (var player in allPlayers)
		{
			if (!player.IsFakeClient)
				player.SendChat("[One HP] Good luck! One shot, one kill!");
		}
	}

	private void OnOneHpRoundEnd(IReadOnlyList<IPlayer>? team1, IReadOnlyList<IPlayer>? team2)
	{
		// cleanup, stats, etc.
	}

	private void OnOneHpPlayerSpawn(IPlayer player)
	{
		// player already has weapons from config, but we can add extras
		player.PlayerPawn?.ItemServices?.GiveItem("weapon_flashbang");
	}

	#endregion

	#region High Gravity Round

	private void OnHighGravityPlayerSpawn(IPlayer player)
	{
		Core.Scheduler.NextWorldUpdate(() =>
		{
			var pawn = player.PlayerPawn;
			if (pawn != null)
			{
				pawn.GravityScale = 3.0f;
				pawn.GravityScaleUpdated();
			}
		});
	}

	#endregion
}
