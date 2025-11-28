using Microsoft.Extensions.Logging;
using SwiftlyS2.Shared.Natives;
using SwiftlyS2.Shared.SchemaDefinitions;

namespace K4Arenas;

public sealed partial class Plugin
{
	/// <summary>
	/// Finds and pairs spawn points into arenas. Supports both normal maps and Cybershoke-style teleport destinations.
	/// Uses clustering algorithm to group nearby spawns into arena pairs.
	/// </summary>
	public sealed class SpawnFinder
	{
		// clustering params (same as original K4-Arenas)
		private const float MergeThreshold = 1.5f;
		private const float DistanceFactor = 1.1f;

		/// <summary>
		/// Finds spawns and returns them as arena pairs (T spawns, CT spawns)
		/// </summary>
		public List<(IReadOnlyList<SpawnLocation> Team1, IReadOnlyList<SpawnLocation> Team2)> GetArenaPairs()
		{
			var ctSpawns = GetSpawnPoints("info_player_counterterrorist");
			var tSpawns = GetSpawnPoints("info_player_terrorist");

			// check for cybershoke teleport destinations
			var teleportDestinations = Core.EntitySystem.GetAllEntitiesByDesignerName<CBaseEntity>("info_teleport_destination").ToList();

			List<(IReadOnlyList<SpawnLocation> Team1, IReadOnlyList<SpawnLocation> Team2)> arenas;

			if (teleportDestinations.Count > 0)
			{
				Core.Logger.LogInformation("Detected {tpDestinations} teleport destinations. Using Cybershoke compatibility mode.", teleportDestinations.Count);
				arenas = CreateArenasFromTeleportDestinations(teleportDestinations, ctSpawns, tSpawns);
			}
			else
			{
				if (ctSpawns.Count == 0 || tSpawns.Count == 0)
				{
					Core.Logger.LogError("No spawn points detected on map!");
					return [];
				}

				Core.Logger.LogInformation("Found {ctCount} CT spawns and {tCount} T spawns.", ctSpawns.Count, tSpawns.Count);
				arenas = CreateArenasFromSpawnClustering(ctSpawns, tSpawns);
			}

			Core.Logger.LogInformation("Successfully created {arenaCount} arena(s).", arenas.Count);

			if (arenas.Count > 0)
			{
				var maxTeamSize = arenas.Max(a => Math.Min(a.Team1.Count, a.Team2.Count));
				Core.Logger.LogInformation("Supported modes: 1v1 to {MaxTeamSize}v{MaxTeamSize}", maxTeamSize, maxTeamSize);
			}

			return arenas;
		}

		private static List<SpawnPoint> GetSpawnPoints(string designerName) =>
			Core.EntitySystem.GetAllEntitiesByDesignerName<SpawnPoint>(designerName).ToList();

		/// <summary>
		/// Cybershoke compatibility - disables normal spawns and creates new ones at teleport locations
		/// </summary>
		private static List<(IReadOnlyList<SpawnLocation> Team1, IReadOnlyList<SpawnLocation> Team2)> CreateArenasFromTeleportDestinations(List<CBaseEntity> teleportDestinations, List<SpawnPoint> ctSpawns, List<SpawnPoint> tSpawns)
		{
			// disable original spawns
			foreach (var spawn in ctSpawns)
				spawn.AcceptInput("SetDisabled", "");

			foreach (var spawn in tSpawns)
				spawn.AcceptInput("SetDisabled", "");

			// pair teleport destinations by distance
			var unpaired = new HashSet<CBaseEntity>(teleportDestinations);
			var pairs = new List<(CBaseEntity, CBaseEntity)>();

			while (unpaired.Count >= 2)
			{
				var first = unpaired.First();
				unpaired.Remove(first);

				var firstOrigin = first.CBodyComponent?.SceneNode?.AbsOrigin ?? Vector.Zero;

				var closest = unpaired
					.OrderBy(t =>
					{
						var origin = t.CBodyComponent?.SceneNode?.AbsOrigin ?? Vector.Zero;
						return DistanceTo(firstOrigin, origin);
					})
					.FirstOrDefault();

				if (closest != null)
				{
					unpaired.Remove(closest);
					pairs.Add((first, closest));
				}
			}

			// create spawn entities at teleport locations
			var arenas = new List<(IReadOnlyList<SpawnLocation>, IReadOnlyList<SpawnLocation>)>();

			foreach (var (tp1, tp2) in pairs)
			{
				var tp1Origin = tp1.CBodyComponent?.SceneNode?.AbsOrigin ?? Vector.Zero;
				var tp1Rotation = tp1.CBodyComponent?.SceneNode?.AbsRotation ?? QAngle.Zero;
				var tp2Origin = tp2.CBodyComponent?.SceneNode?.AbsOrigin ?? Vector.Zero;
				var tp2Rotation = tp2.CBodyComponent?.SceneNode?.AbsRotation ?? QAngle.Zero;

				tp1.Despawn();
				tp2.Despawn();

				var ctSpawn = Core.EntitySystem.CreateEntityByDesignerName<SpawnPoint>("info_player_counterterrorist");
				ctSpawn.Teleport(tp1Origin, tp1Rotation, Vector.Zero);
				ctSpawn.DispatchSpawn();

				var tSpawn = Core.EntitySystem.CreateEntityByDesignerName<SpawnPoint>("info_player_terrorist");
				tSpawn.Teleport(tp2Origin, tp2Rotation, Vector.Zero);
				tSpawn.DispatchSpawn();

				var ctSpawnLocation = new SpawnLocation(tp1Origin, tp1Rotation);
				var tSpawnLocation = new SpawnLocation(tp2Origin, tp2Rotation);

				arenas.Add((new List<SpawnLocation> { ctSpawnLocation }, new List<SpawnLocation> { tSpawnLocation }));
			}

			return arenas;
		}

		private List<(IReadOnlyList<SpawnLocation> Team1, IReadOnlyList<SpawnLocation> Team2)> CreateArenasFromSpawnClustering(List<SpawnPoint> ctSpawnEntities, List<SpawnPoint> tSpawnEntities)
		{
			var ctArr = ctSpawnEntities
				.Where(s => s.CBodyComponent?.SceneNode != null)
				.Select(s => (
					Location: new SpawnLocation(s.CBodyComponent!.SceneNode!.AbsOrigin, s.CBodyComponent.SceneNode.AbsRotation),
					IsCT: true
				))
				.ToArray();

			var tArr = tSpawnEntities
				.Where(s => s.CBodyComponent?.SceneNode != null)
				.Select(s => (
					Location: new SpawnLocation(s.CBodyComponent!.SceneNode!.AbsOrigin, s.CBodyComponent.SceneNode.AbsRotation),
					IsCT: false
				))
				.ToArray();

			var allSpawns = new List<(SpawnLocation Location, bool IsCT)>(ctArr.Length + tArr.Length);
			for (var i = 0; i < ctArr.Length; i++) allSpawns.Add(ctArr[i]);
			for (var i = 0; i < tArr.Length; i++) allSpawns.Add(tArr[i]);

			return CreateArenasFromSpawnClustering_Core(allSpawns, new List<(SpawnLocation, bool)>(ctArr), new List<(SpawnLocation, bool)>(tArr));
		}

		private List<(IReadOnlyList<SpawnLocation>, IReadOnlyList<SpawnLocation>)> CreateArenasFromSpawnClustering_Core(List<(SpawnLocation Location, bool IsCT)> allSpawns, List<(SpawnLocation Location, bool IsCT)> ctSpawns, List<(SpawnLocation Location, bool IsCT)> tSpawns)
		{
			if (allSpawns.Count == 0)
				return [];

			// calculate median enemy distance
			var enemyDistances = new List<float>();
			for (var i = 0; i < allSpawns.Count; i++)
			{
				var item = allSpawns[i];
				float minDist = float.MaxValue;
				var found = false;
				for (var j = 0; j < allSpawns.Count; j++)
				{
					if (i == j) continue;
					var other = allSpawns[j];
					if (other.IsCT == item.IsCT) continue;
					var d = DistanceTo(item.Location.Position, other.Location.Position);
					if (d < minDist) minDist = d;
					found = true;
				}
				if (found) enemyDistances.Add(minDist);
			}

			if (enemyDistances.Count == 0)
				return [];

			enemyDistances.Sort();
			var medianDistance = enemyDistances.Count % 2 == 1
				? enemyDistances[enemyDistances.Count / 2]
				: (enemyDistances[enemyDistances.Count / 2 - 1] + enemyDistances[enemyDistances.Count / 2]) / 2;

			var threshold = medianDistance * DistanceFactor;

			// union-find clustering
			var parent = allSpawns.ToDictionary(s => s, s => s);

			(SpawnLocation Location, bool IsCT) Find((SpawnLocation Location, bool IsCT) item)
			{
				if (!parent[item].Equals(item))
					parent[item] = Find(parent[item]);
				return parent[item];
			}

			void Union((SpawnLocation Location, bool IsCT) a, (SpawnLocation Location, bool IsCT) b)
			{
				var rootA = Find(a);
				var rootB = Find(b);
				if (!rootA.Equals(rootB))
					parent[rootB] = rootA;
			}

			// cluster enemy spawns within threshold
			foreach (var ct in ctSpawns)
			{
				foreach (var t in tSpawns)
				{
					var dist = DistanceTo(ct.Location.Position, t.Location.Position);
					if (dist <= threshold)
						Union(ct, t);
				}
			}

			// group by cluster root
			var clustersMap = new Dictionary<(SpawnLocation Location, bool IsCT), List<(SpawnLocation Location, bool IsCT)>>(allSpawns.Count);
			for (var i = 0; i < allSpawns.Count; i++)
			{
				var s = allSpawns[i];
				var root = Find(s);
				if (!clustersMap.TryGetValue(root, out var list))
				{
					list = [];
					clustersMap[root] = list;
				}
				list.Add(s);
			}

			var clusters = new List<List<(SpawnLocation Location, bool IsCT)>>(clustersMap.Count);
			foreach (var kv in clustersMap) clusters.Add(kv.Value);

			// merge close clusters
			var mergeThreshold = threshold * MergeThreshold;
			clusters = MergeClusters(clusters, mergeThreshold);

			// create arena pairs from valid clusters
			var arenas = new List<(IReadOnlyList<SpawnLocation>, IReadOnlyList<SpawnLocation>)>();

			foreach (var cluster in clusters)
			{
				var clusterCT = new List<SpawnLocation>();
				var clusterT = new List<SpawnLocation>();
				for (var i = 0; i < cluster.Count; i++)
				{
					var s = cluster[i];
					if (s.IsCT) clusterCT.Add(s.Location); else clusterT.Add(s.Location);
				}

				if (clusterCT.Count > 0 && clusterT.Count > 0)
					arenas.Add((clusterCT, clusterT));
			}

			// fallback: use all spawns as one arena
			if (arenas.Count == 0 && ctSpawns.Count > 0 && tSpawns.Count > 0)
			{
				Core.Logger.LogWarning("No suitable arenas found with clustering. Using fallback mode.");
				var fallbackCT = new List<SpawnLocation>(ctSpawns.Count);
				for (var i = 0; i < ctSpawns.Count; i++) fallbackCT.Add(ctSpawns[i].Location);
				var fallbackT = new List<SpawnLocation>(tSpawns.Count);
				for (var i = 0; i < tSpawns.Count; i++) fallbackT.Add(tSpawns[i].Location);
				arenas.Add((fallbackCT, fallbackT));
			}

			return arenas;
		}

		private static List<List<(SpawnLocation Location, bool IsCT)>> MergeClusters(List<List<(SpawnLocation Location, bool IsCT)>> clusters, float mergeThreshold)
		{
			bool merged;
			do
			{
				merged = false;
				for (var i = 0; i < clusters.Count; i++)
				{
					for (var j = i + 1; j < clusters.Count; j++)
					{
						var centroid1 = ComputeCentroid(clusters[i]);
						var centroid2 = ComputeCentroid(clusters[j]);

						if (DistanceTo(centroid1, centroid2) < mergeThreshold)
						{
							clusters[i].AddRange(clusters[j]);
							clusters.RemoveAt(j);
							merged = true;
							break;
						}
					}
					if (merged) break;
				}
			} while (merged);

			return clusters;
		}

		private static Vector ComputeCentroid(List<(SpawnLocation Location, bool IsCT)> cluster)
		{
			if (cluster.Count == 0)
				return Vector.Zero;

			var sumX = cluster.Sum(s => s.Location.Position.X);
			var sumY = cluster.Sum(s => s.Location.Position.Y);
			var sumZ = cluster.Sum(s => s.Location.Position.Z);
			var count = cluster.Count;

			return new Vector(sumX / count, sumY / count, sumZ / count);
		}

		private static float DistanceTo(Vector a, Vector b)
		{
			var dx = a.X - b.X;
			var dy = a.Y - b.Y;
			var dz = a.Z - b.Z;
			return MathF.Sqrt(dx * dx + dy * dy + dz * dz);
		}
	}
}
