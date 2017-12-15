// <copyright file="PudgeComboOrbwalker.cs" company="Ensage">
//    Copyright (c) 2017 Ensage.
// </copyright>

namespace Vaper.OrbwalkingModes.Combo
{
    using System;
    using System.Reflection;
    using System.Threading;
    using System.Threading.Tasks;

    using Ensage;
    using Ensage.SDK.Extensions;
    using Ensage.SDK.Geometry;
    using Ensage.SDK.Handlers;
    using Ensage.SDK.Helpers;
    using Ensage.SDK.Prediction;

    using log4net;

    using PlaySharp.Toolkit.Logging;

    using SharpDX;

    using Vaper.Heroes;

    internal class PudgeOrbwalker : ComboOrbwalkingMode
    {
        private static readonly ILog Log = AssemblyLogs.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private readonly Pudge hero;

        private readonly IUpdateHandler hookUpdateHandler;

        private Vector3 hookCastPosition;

        private float hookStartCastTime;

        public PudgeOrbwalker(Pudge hero)
            : base(hero)
        {
            this.hero = hero;

            Entity.OnBoolPropertyChange += this.OnHookCast;
            this.hookUpdateHandler = UpdateManager.Subscribe(this.HookHitCheck, 0, false);
        }

        public override async Task ExecuteAsync(CancellationToken token)
        {
            if (!await this.ShouldExecute(token))
            {
                return;
            }

            var hook = this.hero.Hook;
            var blink = this.hero.Blink;
            var atos = this.hero.Atos;

            this.MaxTargetRange = Math.Max(this.MaxTargetRange, hook.CastRange * 1.1f);
            if (blink != null)
            {
                this.MaxTargetRange = Math.Max(this.MaxTargetRange, blink.CastRange * 1.3f);
            }

            if (atos != null)
            {
                this.MaxTargetRange = Math.Max(this.MaxTargetRange, atos.CastRange * 1.1f);
            }

            if ((this.CurrentTarget == null) || !this.CurrentTarget.IsVisible)
            {
                this.hero.Context.Orbwalker.Active.OrbwalkTo(null);
                return;
            }

            if (this.CurrentTarget.IsIllusion)
            {
                this.OrbwalkToTarget();
                return;
            }

            var items = this.hero.Items.Value;
            var rot = this.hero.Rot;
            var ult = this.hero.Dismember;

            var forceStaff = this.hero.ForceStaff;
            var forceStaffReady = (forceStaff != null) && forceStaff.CanBeCasted; // let it break linkens without menu check
            var blinkReady = (blink != null) && items.IsEnabled(blink.Ability.Name) && blink.CanBeCasted;

            if (blinkReady && this.Owner.Distance2D(this.CurrentTarget) > 600 && !this.hero.HookModifierDetected)
            {
                var distance = this.Owner.Distance2D(this.CurrentTarget);
                var blinkPosition = this.CurrentTarget.NetworkPosition.Extend(this.Owner.NetworkPosition, Math.Max(100, distance - blink.CastRange));
                blink.UseAbility(blinkPosition);

                if (ult.CanBeCasted && blinkPosition.Distance2D(this.CurrentTarget.NetworkPosition) <= ult.CastRange)
                {
                    rot.Enabled = true;

                    var linkens = this.CurrentTarget.IsLinkensProtected();
                    if (forceStaffReady && linkens)
                    {
                        forceStaff.UseAbility(this.CurrentTarget);
                        linkens = false;
                    }

                    if (!linkens)
                    {
                        ult.UseAbility(this.CurrentTarget);
                        await Task.Delay(ult.GetCastDelay(this.CurrentTarget) + 500, token);
                    }
                }
                else
                {
                    await Task.Delay(blink.GetCastDelay(this.CurrentTarget), token);
                }
            }

            if (forceStaffReady && items.IsEnabled(forceStaff.Ability.Name) && this.Owner.Distance2D(this.CurrentTarget) > 500 && !this.CurrentTarget.IsLinkensProtected())
            {
                if (this.Owner.FindRotationAngle(this.CurrentTarget.Position) > 0.3f)
                {
                    var turnPosition = this.Owner.NetworkPosition.Extend(this.CurrentTarget.NetworkPosition, 100);
                    this.Owner.Move(turnPosition);
                    await Task.Delay((int)(this.Owner.TurnTime(turnPosition) * 1000) + 200, token);
                }

                forceStaff.UseAbility(this.Owner);
                await Task.Delay((int)((forceStaff.PushLength / forceStaff.PushSpeed) * 1000), token);
            }

            var vessel = this.hero.Vessel;
            var urn = this.hero.Urn;
            if ((urn?.CanBeCasted == true && items.IsEnabled(urn.Ability.Name) || vessel?.CanBeCasted == true && items.IsEnabled(vessel.Ability.Name))
                && (this.hero.HookModifierDetected || this.Owner.Distance2D(this.CurrentTarget) < 300)
                && !ult.IsChanneling)
            {
                urn?.UseAbility(this.CurrentTarget);
                vessel?.UseAbility(this.CurrentTarget);
            }

            if (rot.CanBeCasted && !rot.Enabled && (this.hero.HookModifierDetected || rot.CanHit(this.CurrentTarget)))
            {
                rot.Enabled = true;
                await Task.Delay(rot.GetCastDelay(), token);
            }

            if (ult.CanBeCasted && (ult.CanHit(this.CurrentTarget) || this.hero.HookModifierDetected))
            {
                var linkens = this.CurrentTarget.IsLinkensProtected();
                if (forceStaffReady && linkens)
                {
                    forceStaff.UseAbility(this.CurrentTarget);
                    linkens = false;
                }

                if (!linkens)
                {
                    ult.UseAbility(this.CurrentTarget);
                    await Task.Delay(ult.GetCastDelay(this.CurrentTarget) + 500, token);
                }
            }

            if (hook.CanBeCasted && hook.CanHit(this.CurrentTarget) && !ult.IsChanneling)
            {
                if (atos?.CanBeCasted == true
                    && items.IsEnabled(atos.Ability.Name)
                    && atos.CanHit(this.CurrentTarget)
                    && !this.CurrentTarget.IsStunned()
                    && !this.CurrentTarget.IsRooted())
                {
                    var hookPreAtosInput = hook.GetPredictionInput(this.CurrentTarget);
                    var hookPreAtosOutput = hook.GetPredictionOutput(hookPreAtosInput);

                    if ((hookPreAtosOutput.HitChance != HitChance.OutOfRange) && (hookPreAtosOutput.HitChance != HitChance.Collision))
                    {
                        atos.UseAbility(this.CurrentTarget);
                        await Task.Delay(atos.GetHitTime(this.CurrentTarget), token);
                    }
                }

                var hookInput = hook.GetPredictionInput(this.CurrentTarget);
                var hookOutput = hook.GetPredictionOutput(hookInput);

                if (this.ShouldCastHook(hookOutput))
                {
                    this.hookCastPosition = hookOutput.UnitPosition;
                    hook.UseAbility(this.hookCastPosition);
                    await Task.Delay(hook.GetHitTime(this.hookCastPosition), token);
                }
            }

            this.OrbwalkToTarget();
        }

        protected override void OnDeactivate()
        {
            UpdateManager.Unsubscribe(this.HookHitCheck);
            Entity.OnBoolPropertyChange -= this.OnHookCast;
            base.OnDeactivate();
        }

        private void HookHitCheck()
        {
            if (this.CurrentTarget == null || !this.CurrentTarget.IsVisible)
            {
                return;
            }

            var hook = this.hero.Hook;
            var input = hook.GetPredictionInput(this.CurrentTarget);
            input.Delay = Math.Max((this.hookStartCastTime - Game.RawGameTime) + hook.CastPoint, 0);
            var output = hook.GetPredictionOutput(input);

            if (this.hookCastPosition.Distance2D(output.UnitPosition) > hook.Radius || !this.ShouldCastHook(output))
            {
                this.Owner.Stop();
                this.Cancel();
                this.hookUpdateHandler.IsEnabled = false;
            }
        }

        private void OnHookCast(Entity sender, BoolPropertyChangeEventArgs args)
        {
            if (args.NewValue == args.OldValue || sender != this.hero.Hook || args.PropertyName != "m_bInAbilityPhase")
            {
                return;
            }

            if (args.NewValue)
            {
                this.hookStartCastTime = Game.RawGameTime;
                this.hookUpdateHandler.IsEnabled = true;
            }
            else
            {
                this.hookUpdateHandler.IsEnabled = false;
            }
        }

        private bool ShouldCastHook(PredictionOutput output)
        {
            if (output.HitChance == HitChance.OutOfRange || output.HitChance == HitChance.Impossible)
            {
                return false;
            }

            if (output.HitChance == HitChance.Collision)
            {
                return false;
            }

            if (output.HitChance < this.hero.MinimumHookChance)
            {
                return false;
            }

            return true;
        }
    }
}