using Fusion;
using UnityEngine;

namespace ProjectA.Networking
{
    public enum AbilityType
    {
        Basic,
        Active,
        Ultimate
    }

    [System.Serializable]
    public struct AbilityDefinition
    {
        public AbilityType type;
        public float cooldownSeconds;
        public int damage;
        public float projectileSpeed;
    }

    public class AbilitySystem : NetworkBehaviour
    {
        [SerializeField] private AbilityDefinition basic = new() { type = AbilityType.Basic, cooldownSeconds = 0.35f, damage = 10, projectileSpeed = 14f };
        [SerializeField] private AbilityDefinition active = new() { type = AbilityType.Active, cooldownSeconds = 3f, damage = 20, projectileSpeed = 12f };
        [SerializeField] private AbilityDefinition ultimate = new() { type = AbilityType.Ultimate, cooldownSeconds = 9f, damage = 35, projectileSpeed = 10f };

        [Networked] private TickTimer BasicCooldown { get; set; }
        [Networked] private TickTimer ActiveCooldown { get; set; }
        [Networked] private TickTimer UltimateCooldown { get; set; }

        public bool TryConsume(AbilityType ability, out AbilityDefinition definition)
        {
            definition = GetDefinition(ability);
            if (!Object.HasStateAuthority)
            {
                return false;
            }

            switch (ability)
            {
                case AbilityType.Basic:
                    if (!BasicCooldown.ExpiredOrNotRunning(Runner)) return false;
                    BasicCooldown = TickTimer.CreateFromSeconds(Runner, definition.cooldownSeconds);
                    return true;
                case AbilityType.Active:
                    if (!ActiveCooldown.ExpiredOrNotRunning(Runner)) return false;
                    ActiveCooldown = TickTimer.CreateFromSeconds(Runner, definition.cooldownSeconds);
                    return true;
                case AbilityType.Ultimate:
                    if (!UltimateCooldown.ExpiredOrNotRunning(Runner)) return false;
                    UltimateCooldown = TickTimer.CreateFromSeconds(Runner, definition.cooldownSeconds);
                    return true;
                default:
                    return false;
            }
        }

        public float CooldownRemaining(AbilityType ability)
        {
            var timer = ability switch
            {
                AbilityType.Basic => BasicCooldown,
                AbilityType.Active => ActiveCooldown,
                AbilityType.Ultimate => UltimateCooldown,
                _ => default
            };

            if (!timer.IsRunning)
            {
                return 0f;
            }

            return timer.RemainingTime(Runner) ?? 0f;
        }

        private AbilityDefinition GetDefinition(AbilityType ability)
        {
            return ability switch
            {
                AbilityType.Basic => basic,
                AbilityType.Active => active,
                AbilityType.Ultimate => ultimate,
                _ => basic
            };
        }
    }
}
