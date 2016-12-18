namespace SpacebarToFarm.Interfaces
{
    using System;
    using System.Linq;

    using Ensage;
    using Ensage.Common;
    using Ensage.Common.Extensions;

    using SharpDX;

    class FarmUnitRanged : FarmUnit
    {
        #region Constants

        private const float DamageMultiplier = 1.45f;

        #endregion

        #region Constructors and Destructors

        public FarmUnitRanged(Unit controlledUnit)
            : base(controlledUnit)
        {
        }

        #endregion

        #region Properties

        protected virtual float AttackRange => ControlledUnit.AttackRange;

        #endregion

        #region Public Methods and Operators

        public override void AddRangeEffect()
        {
            if (RangeEffect != null || !FarmMenu.ShouldDrawLasthitRange) return;

            RangeEffect = ControlledUnit.AddParticleEffect("particles/ui_mouseactions/drag_selected_ring.vpcf");
            RangeEffect.SetControlPoint(1, new Vector3(FarmMenu.RedColor, FarmMenu.GreenColor, FarmMenu.BlueColor));
            // R G B
            RangeEffect.SetControlPoint(2, new Vector3(AttackRange + FarmMenu.RangedBonusRange, 255, 0));
        }

        public override bool Harras()
        {
            throw new NotImplementedException();
        }

        public override bool LaneClear()
        {
            throw new NotImplementedException();
        }

        public override bool LastHit()
        {
            if (LastTarget != null && !IsLastTargetValid)
            {
                if (FarmMenu.IsAutoStopEnabled) ControlledUnit.Stop();
                LastTarget = null;
            }

            if (!Utils.SleepCheck($"lasthit_{ControlledUnit.Handle}")) return LastTarget != null;

            if (FarmMenu.IsLasthittingActive)
            {
                var couldKill =
                    InfoCentral.EnemyCreeps.Where(x => x.Distance2D(ControlledUnit) < (AttackRange + FarmMenu.RangedBonusRange))
                        .Where(x => GetPseudoHealth(x) <= (GetAttackDamage(x) * DamageMultiplier))
                        .OrderBy(x => x.Distance2D(ControlledUnit))
                        .FirstOrDefault();

                if (couldKill != null)
                {
                    LastTarget = couldKill;

                    if (ControlledUnit.IsAttacking() && GetPseudoHealth(couldKill) > GetAttackDamage(couldKill))
                    {
                        ControlledUnit.Stop();
                        Utils.Sleep((ControlledUnit.AttackPoint()* 250), $"lasthit_{ControlledUnit.Handle}");
                        return false;
                    }

                    ControlledUnit.Attack(couldKill);
                    Utils.Sleep((ControlledUnit.AttackPoint()*250), $"lasthit_{ControlledUnit.Handle}");
                    return true;
                }
            }

            if (!FarmMenu.IsDenyModeActive) return false;

            var couldDeny =
                InfoCentral.AlliedCreeps.Where(
                        x =>
                            x.Distance2D(ControlledUnit) < (AttackRange + FarmMenu.RangedBonusRange)
                            && GetPseudoHealth(x) <= (GetAttackDamage(x) * DamageMultiplier))
                    .OrderBy(x => x.Distance2D(ControlledUnit))
                    .FirstOrDefault();
            if (couldDeny != null)
            {
                LastTarget = couldDeny;

                if (ControlledUnit.IsAttacking() && GetPseudoHealth(couldDeny) > GetAttackDamage(couldDeny))
                {
                    ControlledUnit.Stop();
                    Utils.Sleep((ControlledUnit.AttackPoint() * 250), $"lasthit_{ControlledUnit.Handle}");
                    return false;
                }

                ControlledUnit.Attack(couldDeny);
                Utils.Sleep((ControlledUnit.AttackPoint() * 250), $"lasthit_{ControlledUnit.Handle}");
                return true;
            }
            return false;
        }

        #endregion

        #region Methods

        protected override float GetBaseDamage(Unit target)
        {
            var baseDamage = ((float)ControlledUnit.MinimumDamage + ControlledUnit.MaximumDamage) / 2;
            // base damage amplification
            if (target.Team != ControlledUnit.Team)
            {
                var quellingBlade =
                    ControlledUnit.Inventory.Items.FirstOrDefault(
                        x => x.ClassID == ClassID.CDOTA_Item_QuellingBlade || x.ClassID == ClassID.CDOTA_Item_Iron_Talon);

                if (quellingBlade != null)
                {
                    // 140
                    var abilitySpecialData =
                        quellingBlade.AbilitySpecialData.FirstOrDefault(x => x.Name == "damage_bonus_ranged");
                    if (abilitySpecialData != null) baseDamage += abilitySpecialData.Value;
                }

                var battleFury =
                    ControlledUnit.Inventory.Items.FirstOrDefault(x => x.ClassID == ClassID.CDOTA_Item_Battlefury);
                if (battleFury != null)
                {
                    // 160
                    var abilitySpecialData =
                        battleFury.AbilitySpecialData.FirstOrDefault(x => x.Name == "quelling_bonus_ranged");
                    if (abilitySpecialData != null) baseDamage *= (abilitySpecialData.Value / 100.0f);
                }
            }
            return baseDamage;
        }

        protected override float GetItemBonusDamage(Unit target)
        {
            float damageBonus = 0;
            // mana burn
            if (target.Mana > 0)
            {
                var diffusal =
                    ControlledUnit.Inventory.Items.FirstOrDefault(
                        x =>
                            x.ClassID == ClassID.CDOTA_Item_Diffusal_Blade
                            || x.ClassID == ClassID.CDOTA_Item_Diffusal_Blade_Level2);
                if (diffusal != null)
                {
                    if (ControlledUnit.IsIllusion)
                    {
                        var abilitySpecialData =
                            diffusal.AbilitySpecialData.FirstOrDefault(
                                x => x.Name == "feedback_mana_burn_illusion_ranged");
                        if (abilitySpecialData != null) damageBonus += Math.Min(abilitySpecialData.Value, target.Mana);
                    }
                    else
                    {
                        var firstOrDefault =
                            diffusal.AbilitySpecialData.FirstOrDefault(x => x.Name == "feedback_mana_burn");
                        if (firstOrDefault != null) damageBonus += Math.Min(firstOrDefault.Value, target.Mana);
                    }
                }
            }
            return damageBonus;
        }

        protected override float GetTimeTilAttack(Unit target)
        {
            var distance = target.Distance2D(ControlledUnit) - target.HullRadius - ControlledUnit.HullRadius;

            var projectileTime = Math.Min(AttackRange, distance) / (float)ControlledUnit.ProjectileSpeed();
            return (Math.Max(0, distance - AttackRange) / ControlledUnit.MovementSpeed)
                   + (float)ControlledUnit.AttackPoint() + (float)ControlledUnit.GetTurnTime(target) + projectileTime
                   - Game.Ping / 1000.0f;
        }

        #endregion
    }
}