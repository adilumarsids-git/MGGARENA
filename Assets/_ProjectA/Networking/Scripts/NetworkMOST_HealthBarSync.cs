using Solo.MOST_IN_ONE;
using UnityEngine;

namespace ProjectA.Networking
{
    public class NetworkMOST_HealthBarSync : MonoBehaviour
    {
        [SerializeField] private HealthBar healthBar;

        public void Sync(int health, int maxHealth)
        {
            if (healthBar == null) healthBar = GetComponentInChildren<HealthBar>(true);
            if (healthBar == null) return;

            if (healthBar.MaxHealth <= 0f) healthBar.ResetMaxHealth(maxHealth);
            if (Mathf.Abs(healthBar.Health - health) > 0.01f) healthBar.UpdateHealth(health);
        }
    }
}
