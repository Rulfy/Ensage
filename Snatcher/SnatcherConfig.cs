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

        //private readonly Dictionary<string, bool> greedDictionary = new Dictionary<string, bool>
        //                                                                {
        //                                                                    ["item_aegis"] = true,
        //                                                                    ["item_cheese"] = true,
        //                                                                    ["rune_bounty"] = false,
        //                                                                    ["item_gem"] = true,
        //                                                                    ["item_rapier"] = true,
        //                                                                };

        private readonly Dictionary<string, bool> snatchDictionary = new Dictionary<string, bool>
                                                                         {
                                                                             ["item_aegis"] = true,
                                                                             ["item_cheese"] = true,
                                                                             ["rune_bounty"] = true,
                                                                             ["item_gem"] = true,
                                                                             ["item_rapier"] = true,
                                                                         };

        private bool disposed;

        public SnatcherConfig()
        {
            this.factory = MenuFactory.Create("Snatcher");

            this.ToggleHotkey = this.factory.Item("Toggle Hotkey", new KeyBind((uint)KeyInterop.VirtualKeyFromKey(Key.P), KeyBindType.Toggle));
            this.HoldHotkey = this.factory.Item("Hold Hotkey", new KeyBind(0));
            this.SnatchOptions = this.factory.Item("Snatch Options", new AbilityToggler(this.snatchDictionary));

            this.ScanIntervall = this.factory.Item("Scan Intervall", new Slider(125, 1, 1000));
            this.ScanIntervall.Item.Tooltip = "How fast it scans for items to pickup.";

            this.SwapItem = this.factory.Item("Swap Item", false);
            this.SwapItem.Item.Tooltip = "Swaps the lowest cost item to the backpack when inventory full.";

            // this.GreedMode = this.factory.Item("Greed Mode", new AbilityToggler(this.greedDictionary));
            this.GreedMode = this.factory.Item("Greed Mode", false);
            this.GreedMode.Item.Tooltip = "Uses blink dagger to reach items.";
        }

        public MenuItem<bool> GreedMode { get; set; }

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