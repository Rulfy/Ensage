// <copyright file="Program.cs" company="Ensage">
//    Copyright (c) 2017 Ensage.
// </copyright>

namespace IllusionSplitter
{
    using System;
    using System.ComponentModel.Composition;
    using System.Windows.Input;

    using Ensage.Common.Menu;
    using Ensage.SDK.Input;
    using Ensage.SDK.Inventory;
    using Ensage.SDK.Orbwalker;
    using Ensage.SDK.Service;
    using Ensage.SDK.Service.Metadata;

    [ExportPlugin("IllusionSplitter")]
    public class Program : Plugin
    {
        private readonly Lazy<IInputManager> input;

        private readonly Lazy<IInventoryManager> inventoryMgr;

        private readonly Lazy<IOrbwalkerManager> orbwalkerManager;

        private IllusionSplitterConfig config;

        [ImportingConstructor]
        public Program([Import] Lazy<IInventoryManager> inventoryMgr, [Import] Lazy<IInputManager> input, [Import] Lazy<IOrbwalkerManager> orbwalkerManager)
        {
            this.inventoryMgr = inventoryMgr;
            this.input = input;
            this.orbwalkerManager = orbwalkerManager;
        }

        public IllusionSplitterMode OrbwalkerMode { get; private set; }

        private IOrbwalker Orbwalker => this.orbwalkerManager.Value.Active;

        protected override void OnActivate()
        {
            this.config = new IllusionSplitterConfig();

            var key = KeyInterop.KeyFromVirtualKey((int)this.config.SplitterHotkey.Value.Key);
            this.OrbwalkerMode = new IllusionSplitterMode(this.Orbwalker, this.input.Value, key, this.config, this.inventoryMgr.Value);

            this.config.SplitterHotkey.Item.ValueChanged += this.HotkeyChanged;

            this.orbwalkerManager.Value.RegisterMode(this.OrbwalkerMode);
        }

        protected override void OnDeactivate()
        {
            this.orbwalkerManager.Value.UnregisterMode(this.OrbwalkerMode);

            this.config.SplitterHotkey.Item.ValueChanged -= this.HotkeyChanged;

            this.config?.Dispose();
        }

        private void HotkeyChanged(object sender, OnValueChangeEventArgs e)
        {
            var keyCode = e.GetNewValue<KeyBind>().Key;
            if (keyCode == e.GetOldValue<KeyBind>().Key)
            {
                return;
            }

            var key = KeyInterop.KeyFromVirtualKey((int)keyCode);
            this.OrbwalkerMode.Key = key;
        }
    }
}