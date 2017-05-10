// <copyright file="IllusionSplitterConfig.cs" company="Ensage">
//    Copyright (c) 2017 Ensage.
// </copyright>

namespace IllusionSplitter
{
    using System;
    using System.Collections.Generic;

    using Ensage.Common.Menu;
    using Ensage.SDK.Menu;

    public class IllusionSplitterConfig
    {
        public readonly MenuFactory Factory;

        private bool disposed;

        public IllusionSplitterConfig()
        {
            this.Factory = MenuFactory.Create("Illusion Splitter");

            this.AngleRandomizer = this.Factory.Item("Randomize Split Angle", true);
            this.AngleRandomizer.Item.Tooltip = "Randomizes the split angle for illusions.";

            this.MoveHero = this.Factory.Item("Move Hero", true);
            this.MoveHero.Item.Tooltip = "Moves your hero to your mouse position, while pressing the split hotkey.";

            this.MinMoveRange = this.Factory.Item("Minimum Move Range", new Slider(800, 100, 2000));
            this.MinMoveRange.Item.Tooltip = "Minimum range to move the illusions.";

            var dict = new Dictionary<string, bool>
                           {
                               { "item_bottle", true },
                               { "item_manta", true },
                               { "naga_siren_mirror_image", true },
                               { "terrorblade_conjure_image", true },
                               { "phantom_lancer_doppelwalk", true },
                           };
            this.UseAbilities = this.Factory.Item("Use Abilities", new AbilityToggler(dict));
            this.UseAbilities.Item.Tooltip = "Uses your spells and items to create illusions before splitting them.";

            this.IlluRange = this.Factory.Item("Illusion Range", new Slider(600, 100, 2000));
            this.IlluRange.Item.Tooltip = "The range to find illusions near your hero.";

            this.SplitterHotkey = this.Factory.Item("Hotkey", new KeyBind(0));
        }

        public MenuItem<bool> AngleRandomizer { get; }

        public MenuItem<Slider> IlluRange { get; }

        public MenuItem<Slider> MinMoveRange { get; }

        public MenuItem<bool> MoveHero { get; }

        public MenuItem<KeyBind> SplitterHotkey { get; }

        public MenuItem<AbilityToggler> UseAbilities { get; }

        public void Dispose()
        {
            this.Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (this.disposed)
            {
                return;
            }

            if (disposing)
            {
                this.Factory.Dispose();
            }

            this.disposed = true;
        }
    }
}