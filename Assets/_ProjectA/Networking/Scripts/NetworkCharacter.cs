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

        [Header("MOST Input Sources")]
        [SerializeField] private MOST_Controller moveJoystick;
        [SerializeField] private MOST_Controller shootJoystick;

        [Header("Damage Test")]
        [SerializeField] private int actionDamage = 10;
        [SerializeField] private float actionRange = 2f;

        public override void Spawned()
        {
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

            if (input.Buttons.IsSet(NetworkPlayerInputData.Action))
            {
                TryDealDamage();
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

        public bool ReadActionPressed()
        {
            if (shootJoystick != null)
            {
                return shootJoystick.GetMagnitude() > 0.6f;
            }

            return Input.GetMouseButton(0);
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

        private void TryDealDamage()
        {
            var hits = Physics.OverlapSphere(transform.position, actionRange);
            foreach (var hit in hits)
            {
                var target = hit.GetComponentInParent<NetworkCharacter>();
                if (target == null || target == this)
                {
                    continue;
                }

                target.ApplyDamage(actionDamage);
                break;
            }
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

                    if (shootJoystick == null && (lower.Contains("shoot") || lower.Contains("aim")))
                    {
                        shootJoystick = controller;
                    }
                }
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
