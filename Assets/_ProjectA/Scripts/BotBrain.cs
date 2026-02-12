using Fusion;
using UnityEngine;

namespace ProjectA
{
    public class BotBrain : NetworkBehaviour
    {
        [SerializeField] private NetworkPlayer_MOST player;
        [SerializeField] private float thinkRange = 12f;
        [SerializeField] private float throwChance = 0.01f;

        public override void FixedUpdateNetwork()
        {
            if (!Object.HasStateAuthority || !player || !player.IsBot) return;

            var nearest = FindNearestEnemy();
            var move = Vector2.zero;
            var aim = Vector2.up;
            var shoot = false;

            if (nearest)
            {
                var delta = nearest.transform.position - transform.position;
                move = new Vector2(delta.x, delta.z).normalized;
                aim = move;
                shoot = delta.sqrMagnitude < thinkRange * thinkRange;
            }
            else
            {
                var center = -transform.position;
                move = new Vector2(center.x, center.z).normalized;
                aim = move;
            }

            player.Simulate(new FusionInputData
            {
                Move = move,
                Aim = aim,
                ShootHeld = shoot,
                ThrowPressed = Random.value < throwChance,
                ActivePressed = false,
                UltimatePressed = false
            });
        }

        private NetworkPlayer_MOST FindNearestEnemy()
        {
            var all = FindObjectsOfType<NetworkPlayer_MOST>();
            NetworkPlayer_MOST best = null;
            var bestDist = float.MaxValue;
            foreach (var p in all)
            {
                if (p == player) continue;
                var dist = (p.transform.position - transform.position).sqrMagnitude;
                if (dist < bestDist)
                {
                    bestDist = dist;
                    best = p;
                }
            }
            return best;
        }
    }
}
