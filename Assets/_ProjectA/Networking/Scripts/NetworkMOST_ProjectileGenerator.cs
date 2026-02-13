using Fusion;
using UnityEngine;

namespace ProjectA.Networking
{
    public class NetworkMOST_ProjectileGenerator : MonoBehaviour
    {
        public void Spawn(NetworkCharacter owner, NetworkObject projectilePrefab, Transform spawnPoint, int damage, float speed, Vector3 aim)
        {
            if (owner == null || owner.Runner == null || projectilePrefab == null) return;
            if (!owner.Object.HasStateAuthority) return;

            var origin = spawnPoint != null ? spawnPoint.position : owner.transform.position + Vector3.up;
            var finalAim = aim.sqrMagnitude > 0.01f ? aim.normalized : owner.transform.forward;

            var projectileObject = owner.Runner.Spawn(projectilePrefab, origin, Quaternion.LookRotation(finalAim), owner.Object.InputAuthority);
            projectileObject.GetComponent<NetworkProjectile>()?.Initialize(owner, finalAim, speed, damage);
        }
    }
}
