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
        [Networked] private Vector3 NetPosition { get; set; }
        [Networked] private Quaternion NetRotation { get; set; }

        [Header("Identity")]
        [SerializeField] private string nickname = "";

        [Header("Health")]
        [SerializeField] private int maxHealth = 100;
        [Networked] public int Health { get; private set; }
        [Networked] public NetworkBool IsEliminated { get; private set; }
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

        [Header("Network MOST Wrappers")]
        [SerializeField] private NetworkMOST_ActionAuthority actionAuthority;
        [SerializeField] private NetworkMOST_HealthBarSync healthBarSync;
        [SerializeField] private NetworkMOST_Aim networkAim;
        [SerializeField] private NetworkMOST_ProjectileGenerator projectileGenerator;

        public int PlayerRaw => Object.InputAuthority.RawEncoded;

        public override void Spawned()
        {
            if (Object.HasStateAuthority)
            {
                Health = maxHealth;
                IsEliminated = false;
                NetPosition = transform.position;
                NetRotation = transform.rotation;
            }

            AutoWireReferences();
            ConfigureLocalOnlyUI(Object.HasInputAuthority);
            SyncHealthBarImmediate();

            if (Object.HasInputAuthority)
            {
                LocalCharacter = this;
                if (NetworkGameManager.Instance != null)
                {
                    var finalName = string.IsNullOrWhiteSpace(nickname) ? $"Player {PlayerRaw}" : nickname;
                    NetworkGameManager.Instance.RegisterNickname(PlayerRaw, finalName);
                }
            }
        }

        public override void Render()
        {
            transform.SetPositionAndRotation(NetPosition, NetRotation);
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
            if (!Object.HasStateAuthority || IsEliminated)
            {
                return;
            }

            if (NetworkGameManager.Instance != null && NetworkGameManager.Instance.IsMatchLocked)
            {
                return;
            }

            if (!GetInput(out NetworkPlayerInputData input))
            {
                return;
            }

            var moveInput = SanitizeMoveInput(input.Move);
            var direction = new Vector3(moveInput.x, 0f, moveInput.y);
            if (direction.sqrMagnitude > 0.0001f)
            {
                var moveDir = direction.normalized;
                NetRotation = Quaternion.LookRotation(moveDir);
                NetPosition += moveDir * (moveSpeed * moveInput.magnitude * Runner.DeltaTime);
            }

            transform.SetPositionAndRotation(NetPosition, NetRotation);

            if (input.Buttons.IsSet(NetworkPlayerInputData.Basic))
            {
                TryFireBasic(direction);
            }

            if (input.Buttons.IsSet(NetworkPlayerInputData.Ultimate))
            {
                TryFireUltimate(direction);
            }
        }

        public Vector2 ReadMoveInput() => moveJoystick != null ? moveJoystick.GetRawValue() : Vector2.zero;
        public bool ReadBasicPressed() => shootJoystick != null ? shootJoystick.GetMagnitude() > 0.6f : Input.GetMouseButton(0);
        public bool ReadUltimatePressed() => throwJoystick != null ? throwJoystick.GetMagnitude() > 0.6f : Input.GetKey(KeyCode.E);
        public bool ReadJumpPressed() => Input.GetKey(KeyCode.Space);

        public void ApplyDamage(int damage, int attackerPlayerRaw = int.MinValue)
        {
            if (NetworkGameManager.Instance != null && NetworkGameManager.Instance.IsMatchLocked)
            {
                return;
            }

            if (Object.HasStateAuthority)
            {
                ApplyDamageStateAuthority(damage, attackerPlayerRaw);
                return;
            }

            RPC_RequestDamage(damage, attackerPlayerRaw);
        }

        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        private void RPC_RequestDamage(int damage, int attackerPlayerRaw, RpcInfo info = default)
        {
            ApplyDamageStateAuthority(damage, attackerPlayerRaw);
        }

        private void ApplyDamageStateAuthority(int damage, int attackerPlayerRaw)
        {
            if (IsEliminated)
            {
                return;
            }

            Health = Mathf.Max(0, Health - Mathf.Abs(damage));
            if (Health > 0)
            {
                return;
            }

            IsEliminated = true;
            if (NetworkGameManager.Instance != null)
            {
                NetworkGameManager.Instance.RegisterElimination(attackerPlayerRaw, PlayerRaw);
            }
        }

        private void TryFireBasic(Vector3 movementDirection)
        {
            if (!BasicCooldown.ExpiredOrNotRunning(Runner)) return;
            BasicCooldown = TickTimer.CreateFromSeconds(Runner, basicCooldownSeconds);
            SpawnProjectile(basicDamage, basicProjectileSpeed, movementDirection);
        }

        private void TryFireUltimate(Vector3 movementDirection)
        {
            if (!UltimateCooldown.ExpiredOrNotRunning(Runner)) return;
            UltimateCooldown = TickTimer.CreateFromSeconds(Runner, ultimateCooldownSeconds);
            SpawnProjectile(ultimateDamage, ultimateProjectileSpeed, movementDirection);
        }

        private void SpawnProjectile(int damage, float speed, Vector3 movementDirection)
        {
            if (projectilePrefab == null) return;

            var aim = ResolveAimDirection(movementDirection);
            if (projectileGenerator != null)
            {
                projectileGenerator.Spawn(this, projectilePrefab, projectileSpawnPoint, damage, speed, aim);
                return;
            }

            var origin = projectileSpawnPoint != null ? projectileSpawnPoint.position : transform.position + Vector3.up;
            var projectileObject = Runner.Spawn(projectilePrefab, origin, Quaternion.LookRotation(aim), Object.InputAuthority);
            projectileObject.GetComponent<NetworkProjectile>()?.Initialize(this, aim, speed, damage);
        }


        private static Vector2 SanitizeMoveInput(Vector2 move)
        {
            if (!float.IsFinite(move.x) || !float.IsFinite(move.y))
            {
                return Vector2.zero;
            }

            if (move.sqrMagnitude > 1f)
            {
                move.Normalize();
            }

            return move;
        }

        private Vector3 ResolveAimDirection(Vector3 movementDirection)
        {
            if (networkAim != null)
            {
                return networkAim.ResolveAimDirection(movementDirection, transform);
            }

            if (movementDirection.sqrMagnitude > 0.04f) return movementDirection.normalized;
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
                foreach (var controller in GetComponentsInChildren<MOST_Controller>(true))
                {
                    var lower = controller.name.ToLowerInvariant();
                    if (moveJoystick == null && lower.Contains("move")) { moveJoystick = controller; continue; }
                    if (shootJoystick == null && lower.Contains("shoot")) { shootJoystick = controller; continue; }
                    if (throwJoystick == null && lower.Contains("throw")) { throwJoystick = controller; }
                }
            }

            if (actionAuthority == null) actionAuthority = GetComponent<NetworkMOST_ActionAuthority>() ?? gameObject.AddComponent<NetworkMOST_ActionAuthority>();
            if (healthBarSync == null) healthBarSync = GetComponent<NetworkMOST_HealthBarSync>() ?? gameObject.AddComponent<NetworkMOST_HealthBarSync>();
            if (networkAim == null) networkAim = GetComponentInChildren<NetworkMOST_Aim>(true) ?? gameObject.AddComponent<NetworkMOST_Aim>();
            if (projectileGenerator == null) projectileGenerator = GetComponent<NetworkMOST_ProjectileGenerator>() ?? gameObject.AddComponent<NetworkMOST_ProjectileGenerator>();

            if (projectileSpawnPoint == null) projectileSpawnPoint = transform;
        }


        private void SyncHealthBarImmediate()
        {
            if (healthBarSync != null)
            {
                healthBarSync.Sync(Health, maxHealth);
                return;
            }

            if (healthBar == null) return;
            if (healthBar.MaxHealth <= 0f) healthBar.ResetMaxHealth(maxHealth);
            if (Mathf.Abs(healthBar.Health - Health) > 0.01f) healthBar.UpdateHealth(Health);
        }

        private void ConfigureLocalOnlyUI(bool isLocal)
        {
            if (actionAuthority != null)
            {
                actionAuthority.Apply(gameObject, isLocal);
                return;
            }

            foreach (var controller in GetComponentsInChildren<MOST_Controller>(true))
            {
                controller.enabled = isLocal;
                if (!isLocal) controller.gameObject.SetActive(false);
            }

            foreach (var action in GetComponentsInChildren<MOST_Action>(true))
            {
                action.enabled = isLocal;
            }

            foreach (var cam in GetComponentsInChildren<Camera>(true))
            {
                cam.enabled = isLocal;
            }

            foreach (var listener in GetComponentsInChildren<AudioListener>(true))
            {
                listener.enabled = isLocal;
            }
        }
    }
}
