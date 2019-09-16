// <copyright file="SnatcherConfig.cs" company="Ensage">
//    Copyright (c) 2018 Ensage.
// </copyright>

namespace Snatcher
{
    using System.Collections.Generic;
    using System.ComponentModel;
    using System.Windows.Input;

    using Ensage.SDK.Menu;
    using Ensage.SDK.Menu.Attributes;
    using Ensage.SDK.Menu.Items;
    using Ensage.SDK.Renderer;

    [Menu("Snatcher")]
    public class SnatcherConfig
    {
        public SnatcherConfig(IRenderManager renderer)
        {
            renderer.TextureManager.LoadAbilityFromDota("item_aegis");
            renderer.TextureManager.LoadAbilityFromDota("item_cheese");
            renderer.TextureManager.LoadAbilityFromDota("item_gem");
            renderer.TextureManager.LoadAbilityFromDota("item_rapier");
            renderer.TextureManager.LoadAbilityFromDota("rune_doubledamage");

            // this.GreedMode = this.factory.Item("Greed Mode", new AbilityToggler(this.greedDictionary));
            this.ToggleHotkey = new HotkeySelector(Key.None, this.ToggleActive, HotkeyFlags.Press);
            this.HoldHotkey = new HotkeySelector(Key.None, this.HoldActive, HotkeyFlags.Down | HotkeyFlags.Up);
        }

        [Item("Active")]
        [PermaShow]
        [DefaultValue(true)]
        public bool IsActive { get; set; }

        [Item("Greed Mode")]
        [Tooltip("Uses blink dagger to reach items.")]
        public bool GreedMode { get; set; }

        [Item("Swap Item")]
        [Tooltip("Swaps the lowest cost item to the backpack when inventory full.")]
        [DefaultValue(true)]
        public bool SwapItem { get; set; }

        [Item("Scan Intervall")]
        [Tooltip("How fast it scans for items to pickup.")]
        public Slider<int> ScanIntervall { get; set; } = new Slider<int>(125, 1, 1000);

        [Item("Snatch Options")]
        public ImageToggler SnatchOptions { get; set; } = new ImageToggler(true, "item_aegis", "item_cheese", "rune_doubledamage", "item_gem", "item_rapier");

        [Item("Check Range")]
        [Tooltip("How far to scan for items exceeding the pick-up range.")]
        public Slider<float> CheckRange { get; set; } = new Slider<float>(300, 0, 1000);

        [Item("Greed Options")]
        public ImageToggler GreedOptions { get; set; } = new ImageToggler(
            new KeyValuePair<string, bool>("item_aegis", true), 
            new KeyValuePair<string, bool>("item_cheese", false), 
            new KeyValuePair<string, bool>("rune_doubledamage", false), 
            new KeyValuePair<string, bool>("item_gem", false), 
            new KeyValuePair<string, bool>("item_rapier", true));

        [Item("Toggle Hotkey")]
        public HotkeySelector ToggleHotkey { get; set; }

        [Item("Hold Hotkey")]
        public HotkeySelector HoldHotkey { get; set; }

        public bool IsActiveHold { get; private set; }

        private void ToggleActive(MenuInputEventArgs obj)
        {
            this.IsActive = !this.IsActive;
        }

        private void HoldActive(MenuInputEventArgs obj)
        {
            this.IsActiveHold = obj.Flag == HotkeyFlags.Down;
        }
    }
}