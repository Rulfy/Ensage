// <copyright file="VenoComboOrbwalker.cs" company="Ensage">
//    Copyright (c) 2017 Ensage.
// </copyright>
namespace Vaper.OrbwalkingModes.Combo
{
    using System.Threading;
    using System.Threading.Tasks;

    using Vaper.Heroes;

    public class DrowComboOrbwalker : ComboOrbwalkingMode
    {
        private readonly Drow hero;

        public DrowComboOrbwalker(Drow hero)
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
        }
    }
}