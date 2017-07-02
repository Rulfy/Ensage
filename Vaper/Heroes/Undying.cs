// <copyright file="Undying.cs" company="Ensage">
//    Copyright (c) 2017 Ensage.
// </copyright>
namespace Vaper.Heroes
{
    using Vaper.OrbwalkingModes;

    public class Undying : BaseHero
    {
        protected override VaperOrbwalkingMode GetOrbwalkingMode()
        {
            return new UndyingOrbwalker(this);
        }

        protected override void OnActivate()
        {
            base.OnActivate();
        }
    }
}