using Fusion;
using UnityEngine;

namespace ProjectA.Networking
{
    public class NetworkInputProvider : MonoBehaviour
    {
        public NetworkPlayerInputData Capture()
        {
            var move = Vector2.zero;

            if (Input.GetKey(KeyCode.W)) move.y += 1f;
            if (Input.GetKey(KeyCode.S)) move.y -= 1f;
            if (Input.GetKey(KeyCode.A)) move.x -= 1f;
            if (Input.GetKey(KeyCode.D)) move.x += 1f;

            if (move.sqrMagnitude > 1f)
            {
                move.Normalize();
            }

            var data = new NetworkPlayerInputData { Move = move };

            if (Input.GetKey(KeyCode.Space))
            {
                data.Buttons.Set(NetworkPlayerInputData.Jump, true);
            }

            if (Input.GetMouseButton(0))
            {
                data.Buttons.Set(NetworkPlayerInputData.Action, true);
            }

            return data;
        }
    }
}
