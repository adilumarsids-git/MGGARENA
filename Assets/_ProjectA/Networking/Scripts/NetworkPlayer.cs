using Fusion;
using UnityEngine;

namespace ProjectA.Networking
{
    [RequireComponent(typeof(NetworkObject))]
    public class NetworkPlayer : NetworkBehaviour
    {
        [SerializeField] private float moveSpeed = 5f;

        public override void Spawned()
        {
            if (transform.childCount > 0)
            {
                return;
            }

            var visual = GameObject.CreatePrimitive(PrimitiveType.Cube);
            visual.name = "Visual";
            visual.transform.SetParent(transform, false);
            visual.transform.localScale = Vector3.one;
        }

        public override void FixedUpdateNetwork()
        {
            if (!GetInput(out NetworkPlayerInputData input))
            {
                return;
            }

            var direction = new Vector3(input.Move.x, 0f, input.Move.y);
            transform.position += direction * moveSpeed * Runner.DeltaTime;
        }
    }
}
