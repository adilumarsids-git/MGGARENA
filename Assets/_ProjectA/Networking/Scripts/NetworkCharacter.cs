using Fusion;
using Solo.MOST_IN_ONE;
using UnityEngine;

namespace ProjectA.Networking
{
    [RequireComponent(typeof(NetworkObject))]
    public class NetworkCharacter : NetworkBehaviour
    {
        public static NetworkCharacter LocalCharacter { get; private set; }

        [Header("Movement")]
        [SerializeField] private float moveSpeed = 4.5f;

        [Header("Health")]
        [SerializeField] private int maxHealth = 100;
        [Networked] public int Health { get; private set; }
        [SerializeField] private HealthBar healthBar;

        [Header("Projectiles")]
        [SerializeField] private NetworkObject projectilePrefab;
        [SerializeField] private Transform projectileSpawnPoint;

        [Header("Cooldowns")]
        [SerializeField] private float basicCooldownSeconds = 0.35f;
        [SerializeField] private float ultimateCooldownSeconds = 4f;
        [SerializeField] private int basicDamage = 10;
        [SerializeField] private int ultimateDamage = 28;
        [SerializeField] private float basicProjectileSpeed = 14f;
        [SerializeField] private float ultimateProjectileSpeed = 10f;

        [Networked] private TickTimer BasicCooldown { get; set; }
        [Networked] private TickTimer UltimateCooldown { get; set; }

        [Header("MOST Input Sources")]
        [SerializeField] private MOST_Controller moveJoystick;
        [SerializeField] private MOST_Controller shootJoystick;
        [SerializeField] private MOST_Controller throwJoystick;

        public override void Spawned()
        {
            if (Object.HasStateAuthority)
            {
                Health = maxHealth;
            }

            AutoWireReferences();
            ConfigureLocalOnlyUI(Object.HasInputAuthority);
            SyncHealthBarImmediate();

            if (Object.HasInputAuthority)
            {
                LocalCharacter = this;
            }
        }

        public override void Render()
        {
            SyncHealthBarImmediate();
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
                TryFireBasic(direction);
            }

            if (input.Buttons.IsSet(NetworkPlayerInputData.Ultimate))
            {
                TryFireUltimate(direction);
            }
        }

        public Vector2 ReadMoveInput()
        {
            return moveJoystick != null ? moveJoystick.GetAxis() : Vector2.zero;
        }

        public bool ReadBasicPressed()
        {
            return shootJoystick != null ? shootJoystick.GetMagnitude() > 0.6f : Input.GetMouseButton(0);
        }

        public bool ReadUltimatePressed()
        {
            return throwJoystick != null ? throwJoystick.GetMagnitude() > 0.6f : Input.GetKey(KeyCode.E);
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

        private void TryFireBasic(Vector3 movementDirection)
        {
            if (!BasicCooldown.ExpiredOrNotRunning(Runner))
            {
                return;
            }

            BasicCooldown = TickTimer.CreateFromSeconds(Runner, basicCooldownSeconds);
            SpawnProjectile(basicDamage, basicProjectileSpeed, movementDirection);
        }

        private void TryFireUltimate(Vector3 movementDirection)
        {
            if (!UltimateCooldown.ExpiredOrNotRunning(Runner))
            {
                return;
            }

            UltimateCooldown = TickTimer.CreateFromSeconds(Runner, ultimateCooldownSeconds);
            SpawnProjectile(ultimateDamage, ultimateProjectileSpeed, movementDirection);
        }

        private void SpawnProjectile(int damage, float speed, Vector3 movementDirection)
        {
            if (projectilePrefab == null)
            {
                return;
            }

            var origin = projectileSpawnPoint != null ? projectileSpawnPoint.position : transform.position + Vector3.up;
            var aim = ResolveAimDirection(movementDirection);
            var projectileObject = Runner.Spawn(projectilePrefab, origin, Quaternion.LookRotation(aim), Object.InputAuthority);
            var projectile = projectileObject.GetComponent<NetworkProjectile>();
            projectile?.Initialize(this, aim, speed, damage);
        }

        private Vector3 ResolveAimDirection(Vector3 movementDirection)
        {
            if (throwJoystick != null)
            {
                var axis = throwJoystick.GetAxis();
                if (axis.sqrMagnitude > 0.04f)
                {
                    return new Vector3(axis.x, 0f, axis.y).normalized;
                }
            }

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

        private void AutoWireReferences()
        {
            if (healthBar == null)
            {
                healthBar = GetComponentInChildren<HealthBar>(true);
            }

            if (moveJoystick == null || shootJoystick == null || throwJoystick == null)
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

                    if (shootJoystick == null && lower.Contains("shoot"))
                    {
                        shootJoystick = controller;
                        continue;
                    }

                    if (throwJoystick == null && lower.Contains("throw"))
                    {
                        throwJoystick = controller;
                    }
                }
            }

            if (projectileSpawnPoint == null)
            {
                projectileSpawnPoint = transform;
            }
        }

        private void SyncHealthBarImmediate()
        {
            if (healthBar == null)
            {
                return;
            }

            if (healthBar.MaxHealth <= 0f)
            {
                healthBar.ResetMaxHealth(maxHealth);
            }

            if (Mathf.Abs(healthBar.Health - Health) > 0.01f)
            {
                healthBar.UpdateHealth(Health);
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
