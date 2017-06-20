// <copyright file="VaperOrbwalkingMode.cs" company="Ensage">
//    Copyright (c) 2017 Ensage.
// </copyright>

namespace Vaper
{
    using Ensage;
    using Ensage.SDK.Orbwalker.Modes;

    public abstract class VaperOrbwalkingMode : KeyPressOrbwalkingModeAsync
    {
        protected VaperOrbwalkingMode(BaseHero hero)
            : base(hero.Ensage.Orbwalker, hero.Ensage.Input, hero.Menu.General.ComboKey)
        {
        }

        public Unit CurrentTarget { get; protected set; }
    }
}