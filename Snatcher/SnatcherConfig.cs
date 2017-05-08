// <copyright file="SnatcherConfig.cs" company="Ensage">
//    Copyright (c) 2017 Ensage.
// </copyright>

namespace Snatcher
{
    using System;
    using System.Collections.Generic;
    using System.Windows.Input;

    using Ensage.Common.Menu;
    using Ensage.SDK.Menu;

    public class SnatcherConfig : IDisposable
    {
        private readonly MenuFactory factory;

        private bool disposed;

        public SnatcherConfig()
        {
            this.factory = MenuFactory.Create("Snatcher");
            var dict = new Dictionary<string, bool>
                           {
                               { "item_aegis", true },
                               { "item_cheese", true },
                               { "rune_bounty", true },
                               { "item_gem", true },
                               { "item_rapier", true },
                           };
            this.ToggleHotkey = this.factory.Item("Toggle Hotkey", new KeyBind((uint)KeyInterop.VirtualKeyFromKey(Key.P), KeyBindType.Toggle));
            this.HoldHotkey = this.factory.Item("Hold Hotkey", new KeyBind(0, KeyBindType.Press));
            this.SnatchOptions = this.factory.Item("Snatch Options", new AbilityToggler(dict));

            this.ScanIntervall = this.factory.Item("Scan Intervall", new Slider(125, 0, 1000));
            this.ScanIntervall.Item.Tooltip = "How fast it scans for items to pickup.";

            this.SwapItem = this.factory.Item("Swap Item", false);
            this.SwapItem.Item.Tooltip = "Swaps the lowest cost item to the backpack when inventory full.";
        }

        public MenuItem<KeyBind> HoldHotkey { get; }

        public MenuItem<Slider> ScanIntervall { get; }

        public MenuItem<AbilityToggler> SnatchOptions { get; }

        public MenuItem<bool> SwapItem { get; }

        public MenuItem<KeyBind> ToggleHotkey { get; }

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
                this.factory.Dispose();
            }

            this.disposed = true;
        }
    }
}