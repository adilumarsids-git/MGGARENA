using Solo.MOST_IN_ONE;
using UnityEngine;

namespace ProjectA.Networking
{
    public class NetworkMOST_Aim : MonoBehaviour
    {
        [SerializeField] private MOST_Controller shootJoystick;
        [SerializeField] private MOST_Controller throwJoystick;

        public Vector3 ResolveAimDirection(Vector3 movementDirection, Transform basis)
        {
            if (throwJoystick == null || shootJoystick == null)
            {
                AutoWire();
            }

            if (throwJoystick != null)
            {
                var axis = throwJoystick.GetAxis();
                if (axis.sqrMagnitude > 0.04f) return new Vector3(axis.x, 0f, axis.y).normalized;
            }

            if (shootJoystick != null)
            {
                var axis = shootJoystick.GetAxis();
                if (axis.sqrMagnitude > 0.04f) return new Vector3(axis.x, 0f, axis.y).normalized;
            }

            if (movementDirection.sqrMagnitude > 0.04f) return movementDirection.normalized;
            return basis != null && basis.forward.sqrMagnitude > 0.1f ? basis.forward : Vector3.forward;
        }

        private void AutoWire()
        {
            foreach (var controller in GetComponentsInChildren<MOST_Controller>(true))
            {
                var lower = controller.name.ToLowerInvariant();
                if (shootJoystick == null && lower.Contains("shoot")) shootJoystick = controller;
                if (throwJoystick == null && lower.Contains("throw")) throwJoystick = controller;
            }
        }
    }
}
