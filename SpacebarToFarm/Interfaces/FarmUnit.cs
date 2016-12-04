namespace SpacebarToFarm.Interfaces
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    using Ensage;
    using Ensage.Common.Extensions;

    abstract class FarmUnit
    {
        #region Fields

        protected Creep LastTarget;

        protected ParticleEffect RangeEffect;

        private ParticleEffect _farmActiveEffect;

        #endregion

        #region Constructors and Destructors

        protected FarmUnit(Unit controlledUnit)
        {
            ControlledUnit = controlledUnit;
        }

        #endregion

        #region Public Properties

        public Unit ControlledUnit { get; }

        public bool IsFarmActiveEffectActive => _farmActiveEffect != null;

        public bool IsRangeEffectActive => RangeEffect != null;

        public bool IsValid => ControlledUnit.IsValid && ControlledUnit.IsAlive && ControlledUnit.IsControllable;

        #endregion

        #region Properties

        protected bool IsLastTargetValid => LastTarget.IsValid && LastTarget.IsAlive;

        #endregion

        #region Public Methods and Operators

        public void AddEffects()
        {
            AddRangeEffect();
            AddFarmActiveEffect();
        }

        public void AddFarmActiveEffect()
        {
            if (_farmActiveEffect != null || !FarmMenu.ShouldUseActiveEffect) return;

            _farmActiveEffect = ControlledUnit.AddParticleEffect("particles/items2_fx/mjollnir_shield.vpcf");
        }

        public abstract void AddRangeEffect();

        public float GetPseudoHealth(Creep target)
        {
            var attackers =
                InfoCentral.AnimationInformation.Where(
                        x =>
                            x.Key.IsAlive && x.Key.IsAttacking() && x.Key.Team != target.Team
                            && x.Key.Distance2D(target) <= x.Key.AttackRange + x.Key.HullRadius + target.HullRadius
                            && Math.Abs(x.Key.FindRelativeAngle(target.NetworkPosition)) < 0.6)
                    .ToDictionary(x => x.Key, x => x.Value);

            var timeLeft = GetTimeTilAttack(target);
            float damageDone = 0;
            foreach (var attacker in attackers)
            {
                if (attacker.Key == ControlledUnit) continue;

                var projectileTime = 0.0f;
                if (attacker.Key.IsRanged) projectileTime += (float)attacker.Key.ProjectileSpeed() * (attacker.Key.Distance2D(target) - attacker.Key.HullRadius - target.HullRadius);

                var dmg = attacker.Key.DamageAverage * (1.0f - target.DamageResist) + attacker.Key.BonusDamage;
     
                var i = 1;
                do
                {
                    var nextAttack =
                        (float)
                        (attacker.Key.SecondsPerAttack * i - (Game.RawGameTime - attacker.Value)
                         - attacker.Key.AttackPoint());
                   // Console.WriteLine($"{nextAttack} vs {timeLeft} ({i})");
                    if ((nextAttack+projectileTime) >= 0 && nextAttack + projectileTime <= timeLeft)
                    {
                        damageDone += dmg;
                    }
                    else
                    {
                        break;
                    }
                    ++i;
                }
                while (true);
            }

            var result = target.Health - damageDone + (target.HealthRegeneration * timeLeft);
            //Console.WriteLine($"health {target.Health} vs {result}");
            return result;
        }

        public float GetPseudoHealth2(Creep target)
        {
            List<HealthEntry> infoList;
            if (!InfoCentral.HealthInformation.TryGetValue(target, out infoList))
            {
                return target.Health;
            }

            float health;
            var latestEntry =
                infoList.Where(
                        x => x.Time > Game.RawGameTime && x.Time <= (Game.RawGameTime + GetTimeTilAttack(target)))
                    .OrderByDescending(x => x.Time)
                    .FirstOrDefault();
            if (latestEntry != null)
            {
                health = latestEntry.Health;
                if (health > target.Health)
                {
                    health = (int)(target.Health - (health - target.Health));
                }
            }
            else health = target.Health;

            var time = GetTimeTilAttack(target);
            var pastTime = Game.RawGameTime - time;

            var infos = infoList.Where(x => x.Time >= pastTime).OrderBy(x => x.Time).ToList();

            if (infos.Any())
            {
                float healthDiff = 0;
                for (var i = 0; i < infos.Count - 1; ++i)
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

        public abstract void Harras();

        public abstract void LaneClear();

        public abstract void LastHit();

        public void RemoveEffects()
        {
            RemoveRangeEffect();
            RemoveFarmActiveEffect();
        }

        public void RemoveFarmActiveEffect()
        {
            if (_farmActiveEffect == null) return;

            _farmActiveEffect.Dispose();
            _farmActiveEffect = null;
        }

        public void RemoveRangeEffect()
        {
            if (RangeEffect == null) return;

            RangeEffect.Dispose();
            RangeEffect = null;
        }

        #endregion

        #region Methods

        protected virtual float GetAttackDamage(Unit target)
        {
            // include bonus damage
            var attackDamage = GetBaseDamage(target) + ControlledUnit.BonusDamage + GetItemBonusDamage(target);

            // armor
            attackDamage *= (1.0f - target.DamageResist);

            return attackDamage;
        }

        protected abstract float GetBaseDamage(Unit target);

        protected abstract float GetItemBonusDamage(Unit target);

        protected abstract float GetTimeTilAttack(Unit target);

        #endregion
    }
}