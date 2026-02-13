using Fusion;
using UnityEngine;

namespace ProjectA.Networking
{
    public class NetworkInputProvider_MOST : MonoBehaviour
    {
        public NetworkPlayerInputData Capture()
        {
            var data = new NetworkPlayerInputData();

            var localCharacter = NetworkCharacter.LocalCharacter;
            if (localCharacter != null)
            {
                data.Move = localCharacter.ReadMoveInput();
                if (localCharacter.ReadBasicPressed())
                {
                    data.Buttons.Set(NetworkPlayerInputData.Basic, true);
                }

                if (localCharacter.ReadActivePressed())
                {
                    data.Buttons.Set(NetworkPlayerInputData.Active, true);
                }

                if (localCharacter.ReadUltimatePressed())
                {
                    data.Buttons.Set(NetworkPlayerInputData.Ultimate, true);
                }

                if (localCharacter.ReadJumpPressed())
                {
                    data.Buttons.Set(NetworkPlayerInputData.Jump, true);
                }

                return data;
            }

            var move = Vector2.zero;
            if (Input.GetKey(KeyCode.W)) move.y += 1f;
            if (Input.GetKey(KeyCode.S)) move.y -= 1f;
            if (Input.GetKey(KeyCode.A)) move.x -= 1f;
            if (Input.GetKey(KeyCode.D)) move.x += 1f;
            if (move.sqrMagnitude > 1f) move.Normalize();

            data.Move = move;
            if (Input.GetMouseButton(0)) data.Buttons.Set(NetworkPlayerInputData.Basic, true);
            if (Input.GetKey(KeyCode.Q)) data.Buttons.Set(NetworkPlayerInputData.Active, true);
            if (Input.GetKey(KeyCode.E)) data.Buttons.Set(NetworkPlayerInputData.Ultimate, true);
            if (Input.GetKey(KeyCode.Space)) data.Buttons.Set(NetworkPlayerInputData.Jump, true);
            return data;
        }
    }
}
