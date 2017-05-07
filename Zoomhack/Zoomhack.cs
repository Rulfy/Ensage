// <copyright file="Zoomhack.cs" company="Ensage">
//    Copyright (c) 2017 Ensage.
// </copyright>

namespace Zoomhack
{
    using System;
    using System.ComponentModel.Composition;

    using Ensage;
    using Ensage.Common.Menu;
    using Ensage.SDK.Input;
    using Ensage.SDK.Menu;
    using Ensage.SDK.Service;
    using Ensage.SDK.Service.Metadata;

    [ExportPlugin("Zoomhack", StartupMode.Auto)]
    public class Zoomhack : Plugin
    {
        private const int DefaultZoomValue = 1334;

        private const int MaxZoomValue = 6000;

        private const int MinZoomValue = 0;

        private readonly IInputManager inputManager;

        private ConVar cameraDistanceConVar;

        private MenuItem<KeyBind> key;

        // ReSharper disable once StyleCop.SA1305
        private ConVar rFarZConVar;

        private MenuItem<Slider> zoomSlider;

        [ImportingConstructor]
        public Zoomhack([Import] IInputManager inputManager)
        {
            this.inputManager = inputManager;
        }

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
                this.rFarZConVar.SetValue(value + 1000);
                this.cameraDistanceConVar.SetValue(value);
            }
        }

        protected override void OnActivate()
        {
            this.rFarZConVar = Game.GetConsoleVar("r_farz");
            this.cameraDistanceConVar = Game.GetConsoleVar("dota_camera_distance");
            this.ZoomCheatFlagsActive = false;

            var factory = MenuFactory.Create("ZoomHack");
            this.key = factory.Item("Key", new KeyBind(0x11, KeyBindType.Press));
            this.zoomSlider = factory.Item("Camera Distance", new Slider(DefaultZoomValue, MinZoomValue, MaxZoomValue));
            this.zoomSlider.Item.ValueChanged += this.ItemValueChanged;
            this.inputManager.MouseWheel += this.InputManagerMouseWheel;

            this.ZoomValue = this.zoomSlider;
        }

        protected override void OnDeactivate()
        {
            this.zoomSlider.Item.ValueChanged -= this.ItemValueChanged;
            this.inputManager.MouseWheel -= this.InputManagerMouseWheel;

            // reset zoom
            this.ZoomValue = DefaultZoomValue;
            this.ZoomCheatFlagsActive = true;
            this.rFarZConVar.SetValue(-1);
        }

        private void InputManagerMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (!this.key.Value.Active)
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
            this.zoomSlider.Value.Value = value;
        }

        private void ItemValueChanged(object sender, OnValueChangeEventArgs e)
        {
            this.ZoomValue = e.GetNewValue<Slider>().Value;
        }
    }
}