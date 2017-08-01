// <copyright file="VenoComboOrbwalker.cs" company="Ensage">
//    Copyright (c) 2017 Ensage.
// </copyright>
namespace Vaper.OrbwalkingModes.Combo
{
    using System.Threading;
    using System.Threading.Tasks;

    using Vaper.Heroes;

    public class VenoComboOrbwalker : ComboOrbwalkingMode
    {
        private readonly Venomancer hero;

        public VenoComboOrbwalker(Venomancer hero)
            : base(hero)
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
                this.hero.Context.Orbwalker.Active.OrbwalkTo(null);
                return;
            }

            if (!this.CurrentTarget.IsIllusion)
            {
                var nova = this.hero.Nova;
                var blink = this.hero.Blink;
                var veil = this.hero.Veil;
                if (nova.CanBeCasted)
                {
                    // TODO:
                    //if (blink != null && blink.CanBeCasted)
                    //{

                    //}
                    //else
                    //{

                    //}

                }

                var gale = this.hero.Gale;
                if (gale.CanBeCasted && gale.CanHit(this.CurrentTarget))
                {
                    if (veil != null && veil.CanBeCasted)
                    {
                        veil.UseAbility(this.CurrentTarget.NetworkPosition);
                        await Task.Delay(veil.GetCastDelay(this.CurrentTarget), token);
                    }

                    gale.UseAbility(this.CurrentTarget);
                    await Task.Delay(gale.GetCastDelay(this.CurrentTarget), token);
                }
            }

            this.OrbwalkToTarget();
        }
    }
}