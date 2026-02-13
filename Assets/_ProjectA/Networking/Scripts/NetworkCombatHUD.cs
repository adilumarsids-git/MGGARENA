using UnityEngine;
using UnityEngine.UI;

namespace ProjectA.Networking
{
    public class NetworkCombatHUD : MonoBehaviour
    {
        [SerializeField] private Text healthText;
        [SerializeField] private Text basicCooldownText;
        [SerializeField] private Text activeCooldownText;
        [SerializeField] private Text ultimateCooldownText;

        private void Update()
        {
            var local = NetworkCharacter.LocalCharacter;
            if (local == null)
            {
                return;
            }

            var ability = local.Ability;
            if (healthText != null)
            {
                healthText.text = $"HP: {local.Health}";
            }

            if (ability == null)
            {
                return;
            }

            if (basicCooldownText != null)
            {
                basicCooldownText.text = $"Basic CD: {ability.CooldownRemaining(AbilityType.Basic):0.0}";
            }

            if (activeCooldownText != null)
            {
                activeCooldownText.text = $"Active CD: {ability.CooldownRemaining(AbilityType.Active):0.0}";
            }

            if (ultimateCooldownText != null)
            {
                ultimateCooldownText.text = $"Ult CD: {ability.CooldownRemaining(AbilityType.Ultimate):0.0}";
            }
        }
    }
}
