// <copyright file="AxeComboOrbwalker.cs" company="Ensage">
//    Copyright (c) 2017 Ensage.
// </copyright>

namespace Vaper.OrbwalkingModes
{
    using System.Reflection;
    using System.Threading;
    using System.Threading.Tasks;

    using Ensage.SDK.Extensions;

    using log4net;

    using PlaySharp.Toolkit.Logging;

    using Vaper.Heroes;

    public class AxeComboOrbwalker : VaperOrbwalkingMode
    {
        private static readonly ILog Log = AssemblyLogs.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private readonly Axe hero;

        public AxeComboOrbwalker(Axe hero)
            : base(hero)
        {
            this.hero = hero;
        }

        public override async Task ExecuteAsync(CancellationToken token)
        {
            if (!await this.ShouldExecute(token))
            {
                return;
            }

            var forceStaff = this.hero.ForceStaff;
            var forceStaffReady = (forceStaff != null) && forceStaff.CanBeCasted;

            var blink = this.hero.Blink;
            if (blink != null)
            {
                this.MaxTargetRange = blink.CastRange * 1.5f;
            }

            if ((this.CurrentTarget == null) || !this.CurrentTarget.IsVisible)
            {
                this.hero.Ensage.Orbwalker.Active.OrbwalkTo(null);
                return;
            }

            var cullingBlade = this.hero.CullingBlade;
            var cullingBladeKill = (cullingBlade != null)
                                   && cullingBlade.CanBeCasted
                                   && (cullingBlade.GetDamage(this.CurrentTarget) > this.CurrentTarget.Health)
                                   && (!this.CurrentTarget.IsLinkensProtected() || forceStaffReady);

            var call = this.hero.Call;

           // Log.Debug($"{blink != null} && {blink?.CanBeCasted} && {blink?.CanHit(this.CurrentTarget)}");
           // Log.Debug($"{call != null} && {call.CanBeCasted} && {!call.CanHit(this.CurrentTarget)} || {cullingBlade}");
            if ((blink != null) && blink.CanBeCasted && blink.CanHit(this.CurrentTarget) && !this.CurrentTarget.IsIllusion)
            {
               
                // only blink when we can call or use ult to kill him
                if (((call != null) && call.CanBeCasted && !call.CanHit(this.CurrentTarget)) || (cullingBladeKill && !cullingBlade.CanHit(this.CurrentTarget)))
                {
                    // TODO: get best blink location with prediction to hit target + max other targets
                    var blinkPos = this.CurrentTarget.IsMoving ? this.CurrentTarget.InFront(75) : this.CurrentTarget.Position;
                    blink.UseAbility(blinkPos);
                    await Task.Delay(blink.GetCastDelay(blinkPos), token);
                }
            }

            if (cullingBladeKill && cullingBlade.CanHit(this.CurrentTarget))
            {
                // break linkens with forcestaff
                if (forceStaffReady && this.CurrentTarget.IsLinkensProtected())
                {
                    forceStaff.UseAbility(this.CurrentTarget);
                    await Task.Delay(forceStaff.GetCastDelay(this.CurrentTarget), token);
                }

                cullingBlade.UseAbility(this.CurrentTarget);
                await Task.Delay(cullingBlade.GetCastDelay(this.CurrentTarget), token);
            }
            else
            {
                if ((call != null) && call.CanBeCasted)
                {
                    var canHit = call.CanHit(this.CurrentTarget);
                    if (!canHit && forceStaffReady)
                    {
                        // check if we can move the enemy with forcestaff into our call
                        if (!this.CurrentTarget.IsRotating()
                            && !this.CurrentTarget.IsLinkensProtected()
                            && (this.Owner.Distance2D(this.CurrentTarget.InFront(forceStaff.PushLength)) < call.Radius))
                        {
                            forceStaff.UseAbility(this.CurrentTarget);
                            var travelTime = (int)((forceStaff.PushLength / forceStaff.PushSpeed) * 1000f);
                            await Task.Delay(forceStaff.GetCastDelay(this.CurrentTarget) + travelTime, token);
                        }

                        // check if we can move us with forcestaff to the enemy to call
                        else if (!this.Owner.IsRotating() && (this.CurrentTarget.Distance2D(this.Owner.InFront(forceStaff.PushLength)) < call.Radius))
                        {
                            forceStaff.UseAbility(this.Owner);
                            var travelTime = (int)((forceStaff.PushLength / forceStaff.PushSpeed) * 1000f);
                            await Task.Delay(forceStaff.GetCastDelay() + travelTime, token);
                        }

                        canHit = call.CanHit(this.CurrentTarget);
                    }

                    if (canHit)
                    {
                        var bladeMail = this.hero.BladeMail;
                        if ((bladeMail != null) && bladeMail.CanBeCasted)
                        {
                            bladeMail.UseAbility();
                            await Task.Delay(bladeMail.GetCastDelay(), token);
                        }

                        var lotusOrb = this.hero.LotusOrb;
                        if ((lotusOrb != null) && lotusOrb.CanBeCasted)
                        {
                            lotusOrb.UseAbility(this.Owner);
                            await Task.Delay(lotusOrb.GetCastDelay(), token);
                        }

                        call.UseAbility();
                        await Task.Delay(call.GetCastDelay(), token);
                    }
                }
            }

            this.OrbwalkToTarget();
        }
    }
}