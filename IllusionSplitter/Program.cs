// <copyright file="Program.cs" company="Ensage">
//    Copyright (c) 2017 Ensage.
// </copyright>

namespace IllusionSplitter
{
    using System.ComponentModel.Composition;
    using System.Windows.Input;

    using Ensage.Common.Menu;
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
            this.config = new IllusionSplitterConfig();

            var key = KeyInterop.KeyFromVirtualKey((int)this.config.SplitterHotkey.Value.Key);
            this.OrbwalkerMode = new IllusionSplitterMode(this.context, key, this.config);

            this.config.SplitterHotkey.Item.ValueChanged += this.HotkeyChanged;

            this.context.Orbwalker.RegisterMode(this.OrbwalkerMode);
        }

        protected override void OnDeactivate()
        {
            this.context.Orbwalker.UnregisterMode(this.OrbwalkerMode);

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