// <copyright file="Zoomhack.cs" company="Ensage">
//    Copyright (c) 2018 Ensage.
// </copyright>

namespace Zoomhack
{
    using System;
    using System.ComponentModel.Composition;
    using System.Windows.Input;

    using Ensage;
    using Ensage.SDK.Input;
    using Ensage.SDK.Menu;
    using Ensage.SDK.Menu.Items;
    using Ensage.SDK.Service;
    using Ensage.SDK.Service.Metadata;

    [Menu("ZoomHack")]
    [ExportPlugin("Zoomhack", StartupMode.Auto)]
    public class Zoomhack : Plugin
    {
        private const int DefaultZoomValue = 1334;

        private const int MaxZoomValue = 6000;

        private const int MinZoomValue = 0;

        private readonly Lazy<IInputManager> inputManager;

        private readonly Lazy<MenuManager> menuManager;

        private ConVar cameraDistanceConVar;

        private bool keyDown;

        // ReSharper disable once StyleCop.SA1305
        private ConVar rFarZConVar;

        [ImportingConstructor]
        public Zoomhack([Import] Lazy<IInputManager> inputManager, [Import] Lazy<MenuManager> menuManager)
        {
            this.inputManager = inputManager;
            this.menuManager = menuManager;
        }

        [Item("Hotkey")]
        public HotkeySelector Hotkey { get; set; }

        [Item("Zoom")]
        public Slider ZoomSlider { get; set; } = new Slider(DefaultZoomValue, MinZoomValue, MaxZoomValue);

        private bool ZoomCheatFlagsActive
        {
            set
            {
                if (value)
                {
                    this.cameraDistanceConVar.AddFlags(ConVarFlags.Cheat);
                }
                else
                {
                    this.cameraDistanceConVar.RemoveFlags(ConVarFlags.Cheat);
                }
            }
        }

        private int ZoomValue
        {
            get
            {
                return this.cameraDistanceConVar.GetInt();
            }

            set
            {
                this.rFarZConVar.SetValue(value * 2);
                this.cameraDistanceConVar.SetValue(value);
            }
        }

        protected override void OnActivate()
        {
            this.Hotkey = new HotkeySelector(Key.LeftCtrl, this.KeyPressed, HotkeyFlags.Down | HotkeyFlags.Up);

            this.menuManager.Value.RegisterMenu(this);

            this.rFarZConVar = Game.GetConsoleVar("r_farz");
            this.cameraDistanceConVar = Game.GetConsoleVar("dota_camera_distance");
            this.ZoomCheatFlagsActive = false;

            this.ZoomSlider.ValueChanging += this.ZoomSliderValueChanging;
            this.inputManager.Value.MouseWheel += this.InputManagerMouseWheel;
            this.ZoomValue = this.ZoomSlider.Value;
        }

        protected override void OnDeactivate()
        {
            this.menuManager.Value.DeregisterMenu(this);

            this.ZoomSlider.ValueChanging -= this.ZoomSliderValueChanging;
            this.inputManager.Value.MouseWheel -= this.InputManagerMouseWheel;

            // reset zoom
            this.ZoomValue = DefaultZoomValue;
            this.ZoomCheatFlagsActive = true;
            this.rFarZConVar.SetValue(-1);
        }

        private void InputManagerMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (!this.keyDown)
            {
                return;
            }

            var value = this.ZoomValue;
            if (e.Delta < 0)
            {
                value += 50;
                value = Math.Min(value, MaxZoomValue);
            }
            else
            {
                value -= 50;
                value = Math.Max(value, MinZoomValue);
            }

            this.ZoomValue = value;
            this.ZoomSlider.Value = value;

            e.Process = false;
        }

        private void KeyPressed(MenuInputEventArgs obj)
        {
            this.keyDown = obj.Flag == HotkeyFlags.Down;
        }

        private void ZoomSliderValueChanging(object sender, ValueChangedEventArgs<int> e)
        {
            this.ZoomValue = e.Value;
        }
    }
}