using Solo.MOST_IN_ONE;
using UnityEngine;

namespace ProjectA.Networking
{
    public class NetworkMOST_ActionAuthority : MonoBehaviour
    {
        public void Apply(GameObject root, bool isLocal)
        {
            foreach (var controller in root.GetComponentsInChildren<MOST_Controller>(true))
            {
                controller.enabled = isLocal;
                if (!isLocal) controller.gameObject.SetActive(false);
            }

            foreach (var action in root.GetComponentsInChildren<MOST_Action>(true))
            {
                action.enabled = isLocal;
            }

            foreach (var cam in root.GetComponentsInChildren<Camera>(true))
            {
                cam.enabled = isLocal;
            }

            foreach (var listener in root.GetComponentsInChildren<AudioListener>(true))
            {
                listener.enabled = isLocal;
            }

            // Disable non-network MOST gameplay movers/drivers on all instances.
            foreach (var free in root.GetComponentsInChildren<MOST_FreeMovement>(true)) free.enabled = false;
            foreach (var grid in root.GetComponentsInChildren<MOST_GridMovement>(true)) grid.enabled = false;
            foreach (var aim in root.GetComponentsInChildren<MOST_Aim>(true)) aim.enabled = false;
            foreach (var generator in root.GetComponentsInChildren<MOST_ProjectileGenerator>(true)) generator.enabled = false;
            foreach (var damage in root.GetComponentsInChildren<MOST_Damage>(true)) damage.enabled = false;
        }
    }
}
