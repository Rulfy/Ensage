// <copyright file="PhantomAssassinOrbwalker.cs" company="Ensage">
//    Copyright (c) 2017 Ensage.
// </copyright>

namespace Vaper.OrbwalkingModes
{
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;

    using Ensage.SDK.Extensions;

    using Vaper.Heroes;

    public class PhantomAssassinOrbwalker : VaperOrbwalkingMode
    {
        private readonly PhantomAssassin hero;

        public PhantomAssassinOrbwalker(PhantomAssassin hero)
            : base(hero)
        {
            this.hero = hero;
        }

        public override async Task ExecuteAsync(CancellationToken token)
        {
            if (!this.hero.Owner.IsAlive || this.hero.IsKillstealing)
            {
                this.CurrentTarget = null;
                await Task.Delay(125, token);
                return;
            }

            var maxRange = 1500.0f;

            var target = this.hero.Ensage.TargetSelector.Active.GetTargets().FirstOrDefault(x => x.Distance2D(this.Owner) <= maxRange);
            this.CurrentTarget = target;
            if (target == null)
            {
                this.hero.Ensage.Orbwalker.Active.OrbwalkTo(null);
                return;
            }

            var dagger = this.hero.Dagger;
            if (dagger.CanBeCasted && dagger.CanHit(target))
            {
                dagger.UseAbility(target);
                await Task.Delay(dagger.GetCastDelay(target), token);
            }

            var blink = this.hero.PhantomStrike;
            if (blink.CanBeCasted && blink.CanHit(target))
            {
                blink.UseAbility(target);
                await Task.Delay(blink.GetCastDelay(target), token);
            }

            if (!target.IsStunned())
            {
                var abysal = this.hero.AbyssalBlade;
                if (abysal != null && abysal.CanBeCasted && abysal.CanHit(target))
                {
                    abysal.UseAbility(target);
                    await Task.Delay(abysal.GetCastDelay(target), token);
                }
            }

            this.hero.Ensage.Orbwalker.Active.OrbwalkTo(target);
        }
    }
}