using Fusion;
using Solo.MOST_IN_ONE;
using UnityEngine;

namespace ProjectA
{
    public class NetworkPlayer_MOST : NetworkBehaviour
    {
        [Header("MOST Refs")]
        [SerializeField] private MOST_FreeMovement freeMovement;
        [SerializeField] private MOST_Aim shootAim;
        [SerializeField] private MOST_Aim throwAim;
        [SerializeField] private MOST_ProjectileGenerator shootGenerator;
        [SerializeField] private MOST_ProjectileGenerator throwGenerator;
        [SerializeField] private MOST_Damage damage;
        [SerializeField] private MOST_Action action;
        [SerializeField] private Canvas localInputCanvas;
        [SerializeField] private Camera localCamera;

        [Networked] public int Kills { get; set; }
        [Networked] public int Deaths { get; set; }
        [Networked] public float Health { get; set; }
        [Networked] public NetworkBool IsBot { get; set; }

        [SerializeField] private float maxHealth = 100;
        [SerializeField] private float shootRange = 10f;
        [SerializeField] private float shootDamage = 15f;
        [SerializeField] private LayerMask hitMask;

        private TickTimer _shootCooldown;

        public override void Spawned()
        {
            if (Object.HasStateAuthority) Health = maxHealth;
            SetLocalPresentation(Object.HasInputAuthority);
        }

        public override void FixedUpdateNetwork()
        {
            if (IsBot)
            {
                return;
            }

            if (!GetInput(out FusionInputData data)) return;
            Simulate(data);
        }

        public void Simulate(FusionInputData data)
        {
            ApplyMovement(data.Move);
            ApplyAim(data.Aim);

            if (data.ShootHeld && _shootCooldown.ExpiredOrNotRunning(Runner))
            {
                _shootCooldown = TickTimer.CreateFromSeconds(Runner, 0.2f);
                FireHitscan(data.Aim.normalized);
                shootGenerator?.SpawnProjectile(transform.position + new Vector3(data.Aim.x, 0, data.Aim.y) * shootRange);
            }

            if (data.ThrowPressed)
            {
                throwGenerator?.SpawnProjectile(transform.position + new Vector3(data.Aim.x, 0, data.Aim.y) * 6f);
            }

            if (data.ActivePressed) action?.PlayAction("Active");
            if (data.UltimatePressed) action?.PlayAction("Ultimate");
        }

        private void ApplyMovement(Vector2 move)
        {
            if (!freeMovement) return;
            if (move.sqrMagnitude > 0.0001f)
            {
                freeMovement.OnInputDetected();
                freeMovement.OnRawValueUpdated(move);
            }
            else
            {
                freeMovement.OnInputReleased();
            }
        }

        private void ApplyAim(Vector2 aim)
        {
            if (aim.sqrMagnitude < 0.0001f) return;
            shootAim?.OnInputUpdated(aim);
            throwAim?.OnInputUpdated(aim);
        }

        private void FireHitscan(Vector2 aim)
        {
            if (!Object.HasStateAuthority || aim.sqrMagnitude < 0.001f) return;
            var direction = new Vector3(aim.x, 0, aim.y);
            if (Physics.Raycast(transform.position + Vector3.up * 0.5f, direction, out var hit, shootRange, hitMask))
            {
                var target = hit.collider.GetComponentInParent<NetworkPlayer_MOST>();
                if (target && target != this)
                {
                    target.RPC_ApplyDamage(shootDamage, Object.InputAuthority.PlayerId);
                }
            }
        }

        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        private void RPC_ApplyDamage(float amount, int attackerPlayerId)
        {
            if (!Object.HasStateAuthority) return;
            Health = Mathf.Max(0, Health - amount);
            damage?.OnDamage(amount);
            if (Health <= 0)
            {
                Deaths++;
                Health = maxHealth;
                RPC_OnKilled(attackerPlayerId);
            }
        }

        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        private void RPC_OnKilled(int attackerPlayerId)
        {
            var players = FindObjectsOfType<NetworkPlayer_MOST>();
            foreach (var p in players)
            {
                if (p.Object && p.Object.InputAuthority.PlayerId == attackerPlayerId)
                {
                    p.Kills++;
                    break;
                }
            }
        }

        public void SetLocalPresentation(bool isLocal)
        {
            if (localInputCanvas) localInputCanvas.enabled = isLocal;
            if (localCamera) localCamera.enabled = isLocal;
        }
    }
}
