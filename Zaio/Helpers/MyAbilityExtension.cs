using System.Collections.Generic;
using Ensage;
using Ensage.Common.Enums;
using Ensage.Common.Extensions;

namespace Zaio.Helpers
{
    public static class MyAbilityExtension
    {
        public static Dictionary<string, bool> AbilityStatus = new Dictionary<string, bool>();
        public static Dictionary<string, bool> AbilityKillStealStatus = new Dictionary<string, bool>();
        public static bool IsAbilityEnabled(this Ability ability)
        {
            bool value;
            if (AbilityStatus.TryGetValue(ability.Name, out value))
                return value;
            return true;
        }

        public static bool IsKillstealAbilityEnabled(this Ability ability)
        {
            bool value;
            if (AbilityKillStealStatus.TryGetValue(ability.Name, out value))
                return value;
            return true;
        }

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