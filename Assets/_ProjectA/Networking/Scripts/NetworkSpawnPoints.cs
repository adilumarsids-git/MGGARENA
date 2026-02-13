using Fusion;
using UnityEngine;

namespace ProjectA.Networking
{
    public class NetworkSpawnPoints : MonoBehaviour
    {
        [Header("FFA Spawn Points (default 4 corners)")]
        [SerializeField] private Vector3[] ffaSpawns =
        {
            new(-6f, 0.5f, -6f),
            new(6f, 0.5f, -6f),
            new(-6f, 0.5f, 6f),
            new(6f, 0.5f, 6f)
        };

        [Header("Teams Spawn Sets (placeholder)")]
        [SerializeField] private Vector3[] teamASpawns =
        {
            new(-8f, 0.5f, -2f),
            new(-8f, 0.5f, 2f)
        };

        [SerializeField] private Vector3[] teamBSpawns =
        {
            new(8f, 0.5f, -2f),
            new(8f, 0.5f, 2f)
        };

        public Vector3 ResolveSpawn(FusionGameMode mode, PlayerRef player)
        {
            var index = Mathf.Abs(player.RawEncoded);

            if (mode == FusionGameMode.Teams)
            {
                var isTeamA = (index % 2) == 0;
                var pool = isTeamA ? teamASpawns : teamBSpawns;
                return Pick(pool, index);
            }

            return Pick(ffaSpawns, index);
        }

        private static Vector3 Pick(Vector3[] set, int index)
        {
            if (set == null || set.Length == 0)
            {
                return Vector3.up * 0.5f;
            }

            return set[index % set.Length];
        }
    }
}
