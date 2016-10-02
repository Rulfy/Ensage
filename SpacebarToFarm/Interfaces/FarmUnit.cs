using System.Collections.Generic;
using System.Linq;
using Ensage;

namespace SpacebarToFarm.Interfaces
{
    abstract class FarmUnit
    {
        public Unit ControlledUnit { get; }
        private ParticleEffect _farmActiveEffect;
        protected ParticleEffect RangeEffect;
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
            var latestEntry = infoList.Where(x => x.Time > Game.RawGameTime && x.Time <= (Game.RawGameTime + GetTimeTilAttack(target))).OrderByDescending(x => x.Time).FirstOrDefault();
            if (latestEntry != null)
            {
                health = latestEntry.Health;
                if (health > target.Health)
                {
                    health = (int)(target.Health - (health - target.Health));
                }
            }
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

        public void AddEffects()
        {
            AddRangeEffect();
            AddFarmActiveEffect();
        }

        public void RemoveEffects()
        {
            RemoveRangeEffect();
            RemoveFarmActiveEffect();
        }

        public void RemoveRangeEffect()
        {
            if (RangeEffect == null)
                return;

            RangeEffect.Dispose();
            RangeEffect = null;
        }

        public bool IsRangeEffectActive => RangeEffect != null;
        public bool IsFarmActiveEffectActive => _farmActiveEffect != null;
    }
}
