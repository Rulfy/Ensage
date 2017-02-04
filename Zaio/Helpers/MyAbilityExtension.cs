using Ensage;

namespace Zaio.Helpers
{
    public static class MyAbilityExtension
    {
        public static bool IsActiveAbility(this Ability ability)
        {
            var behavior = ability.AbilityBehavior;
            return !behavior.HasFlag(AbilityBehavior.Passive) && !behavior.HasFlag(AbilityBehavior.Hidden) &&
                   !behavior.HasFlag(AbilityBehavior.Aura) &&
                   (behavior.HasFlag(AbilityBehavior.AreaOfEffect)
                    ||
                    behavior.HasFlag(AbilityBehavior.UnitTarget)
                    ||
                    behavior.HasFlag(AbilityBehavior.Point)
                    ||
                    behavior.HasFlag(AbilityBehavior.NoTarget));
        }
    }
}