// <copyright file="ControllerSharp.cs" company="Ensage">
//    Copyright (c) 2017 Ensage.
// </copyright>

namespace ControllerSharp
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel.Composition;
    using System.Linq;
    using System.Reflection;
    using System.Threading.Tasks;

    using Ensage;
    using Ensage.SDK.Helpers;
    using Ensage.SDK.Orbwalker;
    using Ensage.SDK.Service;
    using Ensage.SDK.Service.Metadata;

    using log4net;
    using log4net.Core;

    using PlaySharp.Toolkit.Logging;

    using SharpDX;
    using SharpDX.XInput;

    [ExportPlugin("ControllerSharp", priority: 1000)]
    public class ControllerSharp : Plugin
    {
        private static readonly ILog Log = AssemblyLogs.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private readonly Lazy<IOrbwalkerManager> orbwalker;

        private readonly Unit owner;

        private ControllerConfig config;

        private Controller controller;

        private int customOrbwalkerIndex;

        private bool damageVibration;

        private IOrbwalkingMode orbwalkerCombo;

        private IOrbwalkingMode orbwalkerFarm;

        private IOrbwalkingMode orbwalkerPush;

        private IOrbwalkingMode orbwalkerSupport;

        private float vibrationDuration;

        private float vibrationStartTime;

        [ImportingConstructor]
        public ControllerSharp([Import] IServiceContext context, [Import] Lazy<IOrbwalkerManager> orbwalker)
        {
            AssemblyLogs.ThresholdLocal = Level.Warn;

            this.owner = context.Owner;
            this.orbwalker = orbwalker;
        }

        protected override void OnActivate()
        {
            this.config = new ControllerConfig();

            this.controller = new Controller((UserIndex)this.config.ControllerIndex);

            this.orbwalkerFarm = this.orbwalker.Value.OrbwalkingModes.First(x => x.Value.ToString().EndsWith("Farm")).Value;
            this.orbwalkerPush = this.orbwalker.Value.OrbwalkingModes.First(x => x.Value.ToString().EndsWith("Push")).Value;
            this.orbwalkerSupport = this.orbwalker.Value.OrbwalkingModes.First(x => x.Value.ToString().EndsWith("Support")).Value;
            this.orbwalkerCombo = this.orbwalker.Value.CustomOrbwalkingModes.FirstOrDefault();

            UpdateManager.Subscribe(this.OnVibrationUpdate, 125);
            UpdateManager.BeginInvoke(this.OnUpdate);
            this.config.ControllerIndexChanged += this.ControllerIndexChanged;

            Entity.OnInt32PropertyChange += this.OnVibrationCheck;

            Game.ExecuteCommand("dota_camera_lock 1");
        }

        protected override void OnDeactivate()
        {
            UpdateManager.Unsubscribe(this.OnVibrationUpdate);
            this.StopVibration();

            Entity.OnInt32PropertyChange -= this.OnVibrationCheck;

            this.config.ControllerIndexChanged -= this.ControllerIndexChanged;
            this.config.Dispose();

            Game.ExecuteCommand("dota_camera_lock 0");
        }

        private void ControllerIndexChanged(object sender, int e)
        {
            this.controller = new Controller((UserIndex)e);
        }

        private void OnVibrationCheck(Entity sender, Int32PropertyChangeEventArgs args)
        {
            if (sender != this.owner)
            {
                return;
            }

            if (args.PropertyName == "m_iRecentDamage")
            {
                if (args.NewValue > 0)
                {
                    if (!this.damageVibration)
                    {
                        this.StartVibration(1.0f);
                        this.damageVibration = true;
                    }
                }
                else
                {
                    this.StopVibration();
                    this.damageVibration = false;
                }
            }

            if (args.PropertyName == "m_iHealth")
            {
                if (args.NewValue <= 0)
                {
                    this.StartVibration(1.0f);
                }
            }

            Log.Debug($"{args.PropertyName}: {args.OldValue} => {args.NewValue}");
        }

        private async void OnUpdate()
        {
            var movedLastTick = false;
            while (this.IsActive)
            {
                if (!this.controller.IsConnected)
                {
                    this.orbwalker.Value.OrbwalkingPoint = Vector3.Zero;
                    await Task.Delay(500);
                    continue;
                }

                State state;
                if (!this.controller.GetState(out state))
                {
                    this.orbwalker.Value.OrbwalkingPoint = Vector3.Zero;
                    await Task.Delay(250);
                    continue;
                }

                var orbwalkingModes = this.orbwalker.Value.CustomOrbwalkingModes.ToList();
                if (orbwalkingModes.Any())
                {
                    if (state.Gamepad.Buttons.HasFlag(GamepadButtonFlags.DPadLeft))
                    {
                        this.customOrbwalkerIndex--;
                        if (this.customOrbwalkerIndex < 0)
                        {
                            this.customOrbwalkerIndex = orbwalkingModes.Count - 1;
                        }

                        this.SetActiveCustomOrbwalker(orbwalkingModes);
                    }
                    else if (state.Gamepad.Buttons.HasFlag(GamepadButtonFlags.DPadRight))
                    {
                        this.customOrbwalkerIndex++;
                        if (this.customOrbwalkerIndex >= orbwalkingModes.Count)
                        {
                            this.customOrbwalkerIndex = 0;
                        }

                        this.SetActiveCustomOrbwalker(orbwalkingModes);
                    }
                }

                var tickRate = this.orbwalker.Value.Config.TickRate.Value;
                if (Game.IsPaused || !this.owner.IsAlive)
                {
                    await Task.Delay(tickRate);
                    continue;
                }

                var buttonX = state.Gamepad.Buttons.HasFlag(GamepadButtonFlags.X);
                var buttonY = state.Gamepad.Buttons.HasFlag(GamepadButtonFlags.Y);
                var buttonB = state.Gamepad.Buttons.HasFlag(GamepadButtonFlags.B);
                var buttonA = state.Gamepad.Buttons.HasFlag(GamepadButtonFlags.A);

                var targetPosition = this.owner.NetworkPosition;

                var leftStickPosition = new Vector3((float)state.Gamepad.LeftThumbX / short.MaxValue, (float)state.Gamepad.LeftThumbY / short.MaxValue, 0);
                if (leftStickPosition.Length() > this.config.Deadzone)
                {
                    leftStickPosition.Normalize();
                    targetPosition += leftStickPosition * Math.Max(this.orbwalker.Value.Settings.HoldRange + 50.0f, 200.0f);
                }
                else if (!buttonX && !buttonY && !buttonB && !buttonA)
                {
                    if (movedLastTick)
                    {
                        this.owner.Stop();
                        movedLastTick = false;
                    }

                    await Task.Delay(tickRate);
                    continue;
                }

                if (buttonX)
                {
                    this.orbwalker.Value.OrbwalkingPoint = targetPosition;
                    this.orbwalkerFarm.Execute();
                    movedLastTick = true;
                    await Task.Delay(tickRate);
                    continue;
                }

                if (buttonY)
                {
                    this.orbwalker.Value.OrbwalkingPoint = targetPosition;
                    this.orbwalkerPush.Execute();
                    movedLastTick = true;
                    await Task.Delay(tickRate);
                    continue;
                }

                if (buttonB)
                {
                    this.orbwalker.Value.OrbwalkingPoint = targetPosition;
                    this.orbwalkerSupport.Execute();
                    movedLastTick = true;
                    await Task.Delay(tickRate);
                    continue;
                }

                if (buttonA)
                {
                    this.orbwalker.Value.OrbwalkingPoint = targetPosition;
                    this.orbwalkerCombo?.Execute();
                    movedLastTick = true;
                    await Task.Delay(tickRate);
                    continue;
                }

                this.orbwalker.Value.Move(targetPosition);
                movedLastTick = true;
                await Task.Delay(tickRate);
            }
        }

        private void SetActiveCustomOrbwalker(List<IOrbwalkingMode> orbwalkingModes)
        {
            this.orbwalkerCombo = orbwalkingModes.Skip(this.customOrbwalkerIndex).FirstOrDefault();
            if (this.orbwalkerCombo != null)
            {
                Game.PrintMessage($"Selected <font color='#FF5050'>{this.orbwalkerCombo}</font> orbwalker");
            }
        }

        private void OnVibrationUpdate()
        {
            if (this.vibrationStartTime <= 0)
            {
                return;
            }

            var gameTime = Game.RawGameTime;
            if ((gameTime - this.vibrationStartTime) >= this.vibrationDuration)
            {
                this.StopVibration();
            }
        }

        private void StartVibration(float duration)
        {
            var intensity = this.config.VibrationIntensity;
            if (intensity <= 0)
            {
                return;
            }

            this.vibrationStartTime = Game.RawGameTime;
            this.vibrationDuration = duration;

            var value = (ushort)(intensity * ushort.MaxValue);
            this.controller.SetVibration(
                new Vibration
                    {
                        LeftMotorSpeed = value,
                        RightMotorSpeed = value
                    });
        }

        private void StopVibration()
        {
            this.vibrationStartTime = 0;
            this.vibrationDuration = 0;

            this.controller.SetVibration(
                new Vibration
                    {
                        LeftMotorSpeed = 0,
                        RightMotorSpeed = 0
                    });
        }
    }
}