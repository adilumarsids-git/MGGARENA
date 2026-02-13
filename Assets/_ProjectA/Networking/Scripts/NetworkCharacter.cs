using Fusion;
using Solo.MOST_IN_ONE;
using UnityEngine;

namespace ProjectA.Networking
{
    [RequireComponent(typeof(NetworkObject))]
    [RequireComponent(typeof(AbilitySystem))]
    public class NetworkCharacter : NetworkBehaviour
    {
        public static NetworkCharacter LocalCharacter { get; private set; }

        [Header("Movement")]
        [SerializeField] private float moveSpeed = 4.5f;

        [Header("Health")]
        [SerializeField] private int maxHealth = 100;
        [Networked] public int Health { get; private set; }

        [Header("Projectiles")]
        [SerializeField] private NetworkObject projectilePrefab;
        [SerializeField] private Transform projectileSpawnPoint;

        [Header("MOST Input Sources")]
        [SerializeField] private MOST_Controller moveJoystick;
        [SerializeField] private MOST_Controller shootJoystick;

        public AbilitySystem Ability { get; private set; }

        public override void Spawned()
        {
            Ability = GetComponent<AbilitySystem>();

            if (Object.HasStateAuthority)
            {
                Health = maxHealth;
            }

            AutoWireControllers();
            ConfigureLocalOnlyUI(Object.HasInputAuthority);

            if (Object.HasInputAuthority)
            {
                LocalCharacter = this;
            }
        }

        public override void Despawned(NetworkRunner runner, bool hasState)
        {
            if (LocalCharacter == this)
            {
                LocalCharacter = null;
            }
        }

        public override void FixedUpdateNetwork()
        {
            if (!Object.HasStateAuthority)
            {
                return;
            }

            if (!GetInput(out NetworkPlayerInputData input))
            {
                return;
            }

            var direction = new Vector3(input.Move.x, 0f, input.Move.y);
            transform.position += direction * moveSpeed * Runner.DeltaTime;

            if (input.Buttons.IsSet(NetworkPlayerInputData.Basic))
            {
                TryFireAbility(AbilityType.Basic, direction);
            }

            if (input.Buttons.IsSet(NetworkPlayerInputData.Active))
            {
                TryFireAbility(AbilityType.Active, direction);
            }

            if (input.Buttons.IsSet(NetworkPlayerInputData.Ultimate))
            {
                TryFireAbility(AbilityType.Ultimate, direction);
            }
        }

        public Vector2 ReadMoveInput()
        {
            if (moveJoystick != null)
            {
                return moveJoystick.GetAxis();
            }

            return Vector2.zero;
        }

        public bool ReadBasicPressed()
        {
            if (shootJoystick != null)
            {
                return shootJoystick.GetMagnitude() > 0.6f;
            }

            return Input.GetMouseButton(0);
        }

        public bool ReadActivePressed()
        {
            return Input.GetKey(KeyCode.Q);
        }

        public bool ReadUltimatePressed()
        {
            return Input.GetKey(KeyCode.E);
        }

        public bool ReadJumpPressed()
        {
            return Input.GetKey(KeyCode.Space);
        }

        public void ApplyDamage(int damage)
        {
            if (Object.HasStateAuthority)
            {
                Health = Mathf.Max(0, Health - Mathf.Abs(damage));
                return;
            }

            RPC_RequestDamage(damage);
        }

        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        private void RPC_RequestDamage(int damage, RpcInfo info = default)
        {
            Health = Mathf.Max(0, Health - Mathf.Abs(damage));
        }

        private void TryFireAbility(AbilityType abilityType, Vector3 movementDirection)
        {
            if (Ability == null || projectilePrefab == null)
            {
                return;
            }

            if (!Ability.TryConsume(abilityType, out var definition))
            {
                return;
            }

            var origin = projectileSpawnPoint != null ? projectileSpawnPoint.position : transform.position + Vector3.up;
            var aim = ResolveAimDirection(movementDirection);
            var projectileObject = Runner.Spawn(projectilePrefab, origin, Quaternion.LookRotation(aim), Object.InputAuthority);
            var projectile = projectileObject.GetComponent<NetworkProjectile>();
            projectile?.Initialize(this, aim, definition.projectileSpeed, definition.damage);
        }

        private Vector3 ResolveAimDirection(Vector3 movementDirection)
        {
            if (shootJoystick != null)
            {
                var shootAxis = shootJoystick.GetAxis();
                if (shootAxis.sqrMagnitude > 0.04f)
                {
                    return new Vector3(shootAxis.x, 0f, shootAxis.y).normalized;
                }
            }

            if (movementDirection.sqrMagnitude > 0.04f)
            {
                return movementDirection.normalized;
            }

            return transform.forward.sqrMagnitude > 0.1f ? transform.forward : Vector3.forward;
        }

        private void AutoWireControllers()
        {
            if (moveJoystick == null || shootJoystick == null)
            {
                var controllers = GetComponentsInChildren<MOST_Controller>(true);
                foreach (var controller in controllers)
                {
                    var lower = controller.name.ToLowerInvariant();
                    if (moveJoystick == null && lower.Contains("move"))
                    {
                        moveJoystick = controller;
                        continue;
                    }

                    if (shootJoystick == null && (lower.Contains("shoot") || lower.Contains("aim") || lower.Contains("throw")))
                    {
                        shootJoystick = controller;
                    }
                }
            }

            if (projectileSpawnPoint == null)
            {
                projectileSpawnPoint = transform;
            }
        }

        private void ConfigureLocalOnlyUI(bool isLocal)
        {
            var controllers = GetComponentsInChildren<MOST_Controller>(true);
            foreach (var controller in controllers)
            {
                controller.enabled = isLocal;
                if (!isLocal)
                {
                    controller.gameObject.SetActive(false);
                }
            }
        }
    }
}
