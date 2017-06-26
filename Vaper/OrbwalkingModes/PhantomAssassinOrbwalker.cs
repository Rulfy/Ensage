// <copyright file="PhantomAssassinOrbwalker.cs" company="Ensage">
//    Copyright (c) 2017 Ensage.
// </copyright>

namespace Vaper.OrbwalkingModes
{
    using System.Threading;
    using System.Threading.Tasks;

    using Ensage.SDK.Extensions;

    using Vaper.Heroes;

    public class PhantomAssassinOrbwalker : VaperOrbwalkingMode
    {
        private readonly PhantomAssassin hero;

        public PhantomAssassinOrbwalker(PhantomAssassin hero)
            : base(hero, 1500.0f)
        {
            this.hero = hero;
        }

        public override async Task ExecuteAsync(CancellationToken token)
        {
            if (!await this.ShouldExecute(token))
            {
                return;
            }

            if ((this.CurrentTarget == null) || !this.CurrentTarget.IsVisible)
            {
                this.hero.Ensage.Orbwalker.Active.OrbwalkTo(null);
                return;
            }

            var dagger = this.hero.Dagger;
            if ((dagger != null) && dagger.CanBeCasted && dagger.CanHit(this.CurrentTarget))
            {
                dagger.UseAbility(this.CurrentTarget);
                await Task.Delay(dagger.GetCastDelay(this.CurrentTarget), token);
            }

            var blink = this.hero.PhantomStrike;
            if ((blink != null) && blink.CanBeCasted && blink.CanHit(this.CurrentTarget))
            {
                blink.UseAbility(this.CurrentTarget);
                await Task.Delay(blink.GetCastDelay(this.CurrentTarget), token);
            }

            if (!this.CurrentTarget.IsStunned())
            {
                var abysal = this.hero.AbyssalBlade;
                if ((abysal != null) && abysal.CanBeCasted && abysal.CanHit(this.CurrentTarget))
                {
                    abysal.UseAbility(this.CurrentTarget);
                    await Task.Delay(abysal.GetCastDelay(this.CurrentTarget), token);
                }
            }

            this.hero.Ensage.Orbwalker.Active.OrbwalkTo(this.CurrentTarget);
        }
    }
}