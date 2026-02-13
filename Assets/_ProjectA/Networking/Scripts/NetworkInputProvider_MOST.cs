using Fusion;
using Solo.MOST_IN_ONE;
using UnityEngine;

namespace ProjectA.Networking
{
    public class NetworkInputProvider_MOST : MonoBehaviour
    {
        [SerializeField] private MOST_Controller moveJoystick;
        [SerializeField] private MOST_Controller shootJoystick;
        [SerializeField] private MOST_Controller throwJoystick;

        public NetworkPlayerInputData Capture()
        {
            TryAutoWireControllers();

            var data = new NetworkPlayerInputData();
            var localCharacter = NetworkCharacter.LocalCharacter;

            var move = localCharacter != null ? localCharacter.ReadMoveInput() : Vector2.zero;
            if (move.sqrMagnitude < 0.0001f && moveJoystick != null)
            {
                move = moveJoystick.GetRawValue();
            }

            if (move.sqrMagnitude < 0.0001f)
            {
                if (Input.GetKey(KeyCode.W)) move.y += 1f;
                if (Input.GetKey(KeyCode.S)) move.y -= 1f;
                if (Input.GetKey(KeyCode.A)) move.x -= 1f;
                if (Input.GetKey(KeyCode.D)) move.x += 1f;
            }

            if (move.sqrMagnitude > 1f)
            {
                move.Normalize();
            }

            data.Move = move;

            var basicPressed = localCharacter != null
                ? localCharacter.ReadBasicPressed()
                : (shootJoystick != null ? shootJoystick.GetMagnitude() > 0.6f : Input.GetMouseButton(0));
            if (basicPressed)
            {
                data.Buttons.Set(NetworkPlayerInputData.Basic, true);
            }

            var ultimatePressed = localCharacter != null
                ? localCharacter.ReadUltimatePressed()
                : (throwJoystick != null ? throwJoystick.GetMagnitude() > 0.6f : Input.GetKey(KeyCode.E));
            if (ultimatePressed)
            {
                data.Buttons.Set(NetworkPlayerInputData.Ultimate, true);
            }

            var jumpPressed = localCharacter != null
                ? localCharacter.ReadJumpPressed()
                : Input.GetKey(KeyCode.Space);
            if (jumpPressed)
            {
                data.Buttons.Set(NetworkPlayerInputData.Jump, true);
            }

            return data;
        }

        private void TryAutoWireControllers()
        {
            if (moveJoystick != null && shootJoystick != null && throwJoystick != null)
            {
                return;
            }

            foreach (var controller in FindObjectsOfType<MOST_Controller>(true))
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
    }
}
