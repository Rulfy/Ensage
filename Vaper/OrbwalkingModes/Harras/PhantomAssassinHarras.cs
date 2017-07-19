// <copyright file="PhantomAssassinHarras.cs" company="Ensage">
//    Copyright (c) 2017 Ensage.
// </copyright>

namespace Vaper.OrbwalkingModes.Harras
{
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;

    using Ensage;
    using Ensage.SDK.Extensions;
    using Ensage.SDK.Helpers;

    using Vaper.Heroes;

    public class PhantomAssassinHarras : HarrasOrbwalkingMode
    {
        private readonly PhantomAssassin hero;

        public PhantomAssassinHarras(PhantomAssassin hero)
            : base(hero)
        {
            this.hero = hero;
        }

        public override async Task ExecuteAsync(CancellationToken token)
        {
            var unitTarget = this.GetTarget();
            if (unitTarget != null)
            {
                this.Orbwalker.OrbwalkTo(unitTarget);
                return;
            }

            var harrasTarget = EntityManager<Hero>.Entities.Where(x => x.IsVisible && x.IsAlive && !x.IsIllusion && x.IsEnemy(this.Owner)).ToList();
            if (harrasTarget.Any())
            {
                var dagger = this.hero.Dagger;
                if (dagger.CanBeCasted)
                {
                    var daggerTarget = harrasTarget.Where(x => dagger.CanHit(x)).OrderBy(x => x.Health).FirstOrDefault();
                    if (daggerTarget != null)
                    {
                        dagger.UseAbility(daggerTarget);
                        await Task.Delay(dagger.GetCastDelay(daggerTarget), token);
                        return;
                    }
                }

                var target = harrasTarget.Where(x => this.Owner.CanHit(x)).OrderBy(x => x.Health).FirstOrDefault();
                if (target != null)
                {
                    this.Orbwalker.OrbwalkTo(target);
                    return;
                }
            }

            this.Orbwalker.OrbwalkTo(null);
        }
    }
}