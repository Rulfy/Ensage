// <copyright file="UndyingOrbwalker.cs" company="Ensage">
//    Copyright (c) 2017 Ensage.
// </copyright>
namespace Vaper.OrbwalkingModes
{
    using System.Threading;
    using System.Threading.Tasks;

    using Vaper.Heroes;

    public class UndyingOrbwalker : VaperOrbwalkingMode
    {
        private readonly Undying hero;

        public UndyingOrbwalker(Undying hero)
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
                this.hero.Ensage.Orbwalker.Active.OrbwalkTo(null);
                return;
            }

            this.OrbwalkToTarget();
        }
    }
}