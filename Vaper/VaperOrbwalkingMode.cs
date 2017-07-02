// <copyright file="VaperOrbwalkingMode.cs" company="Ensage">
//    Copyright (c) 2017 Ensage.
// </copyright>

namespace Vaper
{
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;

    using Ensage;
    using Ensage.SDK.Extensions;
    using Ensage.SDK.Orbwalker.Modes;

    public abstract class VaperOrbwalkingMode : KeyPressOrbwalkingModeAsync
    {
        private readonly BaseHero baseHero;

        protected VaperOrbwalkingMode(BaseHero hero, float maxTargetRange = 1000.0f)
            : base(hero.Ensage.Orbwalker, hero.Ensage.Input, hero.Menu.General.ComboKey)
        {
            this.baseHero = hero;
            this.MaxTargetRange = maxTargetRange;
        }

        public override bool CanExecute
        {
            get
            {
                var result = base.CanExecute;
                if (!result)
                {
                    this.CurrentTarget = null;
                }

                return result;
            }
        }

        public Unit CurrentTarget { get; protected set; }

        public float MaxTargetRange { get; protected set; }

        protected async Task<bool> ShouldExecute(CancellationToken token)
        {
            if (!this.baseHero.Owner.IsAlive)
            {
                this.CurrentTarget = null;
                await Task.Delay(125, token);
                return false;
            }

            if (this.baseHero.IsKillstealing)
            {
                await Task.Delay(125, token);
                return false;
            }

            if (!this.baseHero.Menu.General.LockTarget || (this.CurrentTarget == null) || !this.CurrentTarget.IsAlive)
            {
                this.CurrentTarget = this.baseHero.Ensage.TargetSelector.Active.GetTargets().FirstOrDefault(x => x.Distance2D(this.Owner) <= this.MaxTargetRange);
            }

            return true;
        }

        protected void OrbwalkToTarget()
        {
            if (this.CurrentTarget != null &&
                (!this.baseHero.Menu.General.KiteMode || this.Owner.Distance2D(this.CurrentTarget) <= this.Owner.AttackRange(this.CurrentTarget)))
            {
                this.baseHero.Ensage.Orbwalker.Active.OrbwalkTo(this.CurrentTarget);
            }
            else
            {
                this.baseHero.Ensage.Orbwalker.Active.OrbwalkTo(null);
            }
        }
    }
}