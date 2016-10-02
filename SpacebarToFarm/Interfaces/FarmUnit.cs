using System.Collections.Generic;
using System.Linq;
using Ensage;
using SharpDX;

namespace SpacebarToFarm.Interfaces
{
    abstract class FarmUnit
    {
        public Unit ControlledUnit { get; }
        private ParticleEffect _farmActiveEffect;
        protected ParticleEffect _rangeEffect;
        protected Creep LastTarget;

        protected FarmUnit(Unit controlledUnit)
        {
            ControlledUnit = controlledUnit;
        }

        #region Abstract Methods
        public abstract void LastHit();
        public abstract void LaneClear();
        public abstract void Harras();
        protected abstract float GetItemBonusDamage(Unit target);
        protected abstract float GetBaseDamage(Unit target);
        protected abstract float GetTimeTilAttack(Unit target);
        #endregion

        protected virtual float GetAttackDamage(Unit target)
        {
            // include bonus damage
            float attackDamage = GetBaseDamage(target) + ControlledUnit.BonusDamage + GetItemBonusDamage(target);

            // armor
            attackDamage *= (1.0f - target.DamageResist);

            return attackDamage;
        }

        public float GetPseudoHealth(Creep target)
        {
            List<HealthEntry> infoList;
            if (!InfoCentral.HealthInformation.TryGetValue(target, out infoList))
            {
                return target.Health;
            }

            float health;
            var latestEntry = infoList.Where(x => x.Time > Game.RawGameTime).OrderByDescending(x => x.Time).FirstOrDefault();
            if (latestEntry != null)
                health = latestEntry.Health;
            else
                health = target.Health;

            float time = GetTimeTilAttack(target);
            float pastTime = Game.RawGameTime - time;

            var infos = infoList.Where(x => x.Time >= pastTime).OrderBy(x => x.Time).ToList();
          
            if (infos.Any())
            {
                float healthDiff = 0;
                for (int i = 0; i < infos.Count - 1; ++i)
                {
                    healthDiff += (infos[i].Health - infos[i + 1].Health);
                }
                healthDiff += infos[infos.Count - 1].Health - health;
                // healthDiff avg in time
                healthDiff /= (infos.Count + 1);
                // scale by needed time
                healthDiff /= infos.First().Time;
                healthDiff *= time;

                health -= healthDiff;
            }

            health += (target.HealthRegeneration * time);

            return health;
        }
  
        public bool IsValid => ControlledUnit.IsValid && ControlledUnit.IsAlive && ControlledUnit.IsControllable;

        protected bool IsLastTargetValid => LastTarget.IsValid && LastTarget.IsAlive;

        public void AddFarmActiveEffect()
        {
            if (_farmActiveEffect != null || !FarmMenu.ShouldUseActiveEffect)
                return;

            _farmActiveEffect = ControlledUnit.AddParticleEffect("particles/items2_fx/mjollnir_shield.vpcf");
        }

        public void RemoveFarmActiveEffect()
        {
            if (_farmActiveEffect == null)
                return;

            _farmActiveEffect.Dispose();
            _farmActiveEffect = null;
        }

        public abstract void AddRangeEffect();

        public void RemoveRangeEffect()
        {
            if (_rangeEffect == null)
                return;

            _rangeEffect.Dispose();
            _rangeEffect = null;
        }
    }
}
