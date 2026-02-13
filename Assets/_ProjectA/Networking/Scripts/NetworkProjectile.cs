using Fusion;
using UnityEngine;

namespace ProjectA.Networking
{
    [RequireComponent(typeof(NetworkObject))]
    public class NetworkProjectile : NetworkBehaviour
    {
        [Networked] private Vector3 Direction { get; set; }
        [Networked] private float Speed { get; set; }
        [Networked] private int Damage { get; set; }
        [Networked] private TickTimer LifeTimer { get; set; }
        [Networked] private NetworkId OwnerId { get; set; }
        [Networked] private int OwnerPlayerRaw { get; set; }

        [SerializeField] private float lifetimeSeconds = 3f;
        [SerializeField] private float hitRadius = 0.3f;

        public void Initialize(NetworkCharacter owner, Vector3 direction, float speed, int damage)
        {
            if (!Object.HasStateAuthority)
            {
                return;
            }

            OwnerId = owner.Object.Id;
            OwnerPlayerRaw = owner.PlayerRaw;
            Direction = direction.sqrMagnitude < 0.001f ? owner.transform.forward : direction.normalized;
            Speed = Mathf.Max(0f, speed);
            Damage = Mathf.Max(1, damage);
            LifeTimer = TickTimer.CreateFromSeconds(Runner, lifetimeSeconds);
        }

        public override void FixedUpdateNetwork()
        {
            if (!Object.HasStateAuthority)
            {
                return;
            }

            if (NetworkGameManager.Instance != null && NetworkGameManager.Instance.IsMatchLocked)
            {
                Runner.Despawn(Object);
                return;
            }

            if (LifeTimer.ExpiredOrNotRunning(Runner))
            {
                Runner.Despawn(Object);
                return;
            }

            transform.position += Direction * Speed * Runner.DeltaTime;

            var hits = Physics.OverlapSphere(transform.position, hitRadius);
            foreach (var hit in hits)
            {
                var target = hit.GetComponentInParent<NetworkCharacter>();
                if (target == null || target.Object.Id == OwnerId)
                {
                    continue;
                }

                target.ApplyDamage(Damage, OwnerPlayerRaw);
                Runner.Despawn(Object);
                break;
            }
        }
    }
}
