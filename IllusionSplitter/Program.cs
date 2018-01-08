// <copyright file="Program.cs" company="Ensage">
//    Copyright (c) 2018 Ensage.
// </copyright>

namespace IllusionSplitter
{
    using System;
    using System.ComponentModel.Composition;

    using Ensage.SDK.Menu.Items;
    using Ensage.SDK.Menu.ValueBinding;
    using Ensage.SDK.Service;
    using Ensage.SDK.Service.Metadata;

    [ExportPlugin("IllusionSplitter")]
    public class Program : Plugin
    {
        private readonly IServiceContext context;

        private IllusionSplitterConfig config;

        [ImportingConstructor]
        public Program([Import] IServiceContext context)
        {
            this.context = context;
        }

        public IllusionSplitterMode OrbwalkerMode { get; private set; }

        protected override void OnActivate()
        {
            this.config = new IllusionSplitterConfig(this.context.Renderer);
            this.context.MenuManager.RegisterMenu(this.config);

            this.OrbwalkerMode = new IllusionSplitterMode(this.context, this.config.SplitterHotkey.Hotkey.Key, this.config);

            this.config.SplitterHotkey.Hotkey.ValueChanging += this.HotkeyChanged;

            this.context.Orbwalker.RegisterMode(this.OrbwalkerMode);
        }

        protected override void OnDeactivate()
        {
            this.context.Orbwalker.UnregisterMode(this.OrbwalkerMode);

            this.config.SplitterHotkey.Hotkey.ValueChanging -= this.HotkeyChanged;
            this.context.MenuManager.DeregisterMenu(this.config);
        }

        private void HotkeyChanged(object sender, ValueChangingEventArgs<KeyOrMouseButton> e)
        {
            this.OrbwalkerMode.Key = e.Value.Key;
        }
    }
}