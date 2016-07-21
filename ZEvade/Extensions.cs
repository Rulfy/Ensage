using System;
using System.Linq;

namespace Evade
{
    using Ensage;

    public static class UnitExtensions
    {
        public static float GetPossibleTravelDistance(this Unit entity, float time)
        {
            return time * entity.MovementSpeed;
        }

        public static float GetTurnRate(this Hero entity)
        {
            var turnRate = Game.FindKeyValues(entity.Name + "/MovementTurnRate", KeyValueSource.Hero).FloatValue;
            var stickyNapalm = entity.Modifiers.FirstOrDefault(x => x.Name == "modifier_batrider_sticky_napalm");
            if (stickyNapalm == null) return turnRate;
           
            var mod = 0.7f;

            var abilitySpecialData = stickyNapalm.Ability?.AbilitySpecialData.FirstOrDefault(x => x.Name == "turn_rate_pct");
            if (abilitySpecialData != null)
                mod = Math.Abs(abilitySpecialData.Value) / 100.0f; // -70
            turnRate *= (float)Math.Pow(mod, stickyNapalm.StackCount);
            
            return turnRate;
        }
    }
}
