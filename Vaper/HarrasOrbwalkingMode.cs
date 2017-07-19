// <copyright file="HarrasOrbwalkingMode.cs" company="Ensage">
//    Copyright (c) 2017 Ensage.
// </copyright>

namespace Vaper
{
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;

    using Ensage;
    using Ensage.SDK.Extensions;
    using Ensage.SDK.Helpers;
    using Ensage.SDK.Orbwalker.Modes;

    public class HarrasOrbwalkingMode : AttackOrbwalkingModeAsync
    {
        private readonly BaseHero baseHero;

        public HarrasOrbwalkingMode(BaseHero hero)
            : base(hero.Ensage.Orbwalker, hero.Ensage.TargetSelector, "Vaper Harras", 'X', false, false, false, false, true, true)
        {
            this.baseHero = hero;
        }

        public override async Task ExecuteAsync(CancellationToken token)
        {
            var unitTarget = this.GetTarget();
            if (unitTarget != null)
            {
                this.Orbwalker.OrbwalkTo(unitTarget);
                return;
            }

            var harrasTarget = EntityManager<Hero>.Entities.Where(x => x.IsVisible && x.IsAlive && !x.IsIllusion && x.IsEnemy(this.Owner) && this.Owner.CanHit(x))
                                                  .OrderBy(x => x.Health)
                                                  .FirstOrDefault();
            if (harrasTarget != null)
            {
                this.Orbwalker.OrbwalkTo(harrasTarget);
                return;
            }

            this.Orbwalker.OrbwalkTo(null);
        }
    }
}