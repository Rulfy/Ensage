// <copyright file="AxeComboOrbwalkingMode.cs" company="Ensage">
//    Copyright (c) 2017 Ensage.
// </copyright>

namespace Vaper.OrbwalkingModes
{
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;

    using Ensage.SDK.Extensions;

    using Vaper.Heroes;

    using UnitExtensions = Ensage.SDK.Extensions.UnitExtensions;

    public class AxeComboOrbwalkingMode : VaperOrbwalkingMode
    {
        private readonly Axe hero;

        public AxeComboOrbwalkingMode(Axe hero)
            : base(hero)
        {
            this.hero = hero;
        }

        public override async Task ExecuteAsync(CancellationToken token)
        {
            if (!this.hero.Owner.IsAlive || this.hero.IsKillstealing)
            {
                this.CurrentTarget = null;
                await Task.Delay(125, token);
                return;
            }

            var forceStaff = this.hero.ForceStaff;
            var forceStaffReady = forceStaff != null && forceStaff.CanBeCasted;

            var blink = this.hero.Blink;
            var maxRange = blink?.CastRange * 1.5f ?? 1000.0f;

            var target = this.hero.Ensage.TargetSelector.Active.GetTargets().FirstOrDefault(x => x.Distance2D(this.Owner) <= maxRange);
            this.CurrentTarget = target;
            if (target == null)
            {
                this.hero.Ensage.Orbwalker.Active.OrbwalkTo(null);
                return;
            }
           
            var cullingBlade = this.hero.CullingBlade;
            var cullingBladeKill = cullingBlade.CanBeCasted && cullingBlade.GetDamage(target) > target.Health && (!target.IsLinkensProtected() || forceStaffReady);

            var call = this.hero.Call;

            if (blink != null && blink.CanBeCasted && blink.CanHit(target))
            {
                // only blink when we can call or use ult to kill him
                if (call.CanBeCasted && !call.CanHit(target) || cullingBladeKill && !cullingBlade.CanHit(target))
                {
                    // TODO: get best blink location with prediction to hit target + max other targets
                    var blinkPos = target.IsMoving ? target.InFront(75) : target.Position;
                    blink.UseAbility(blinkPos);
                    await Task.Delay(blink.GetCastDelay(blinkPos), token);
                }
            }

            if (cullingBladeKill && cullingBlade.CanHit(target))
            {
                // break linkens with forcestaff
                if (forceStaffReady && target.IsLinkensProtected())
                {
                    forceStaff.UseAbility(target);
                    await Task.Delay(forceStaff.GetCastDelay(target), token);
                }

                cullingBlade.UseAbility(target);
                await Task.Delay(cullingBlade.GetCastDelay(target), token);
            }
            else
            {
                if (call.CanBeCasted)
                {
                    var canHit = call.CanHit(target);
                    if (!canHit && forceStaffReady)
                    {
                        // check if we can move the enemy with forcestaff into our call
                        if (!target.IsRotating() && !target.IsLinkensProtected() && this.Owner.Distance2D(target.InFront(forceStaff.PushLength)) < call.Radius)
                        {
                            forceStaff.UseAbility(target);
                            var travelTime = (int)((forceStaff.PushLength / forceStaff.PushSpeed) * 1000f);
                            await Task.Delay(forceStaff.GetCastDelay(target) + travelTime, token);
                        }
                        // check if we can move us with forcestaff to the enemy to call
                        else if (!this.Owner.IsRotating() && target.Distance2D(this.Owner.InFront(forceStaff.PushLength)) < call.Radius)
                        {
                            forceStaff.UseAbility(this.Owner);
                            var travelTime = (int)((forceStaff.PushLength / forceStaff.PushSpeed) * 1000f);
                            await Task.Delay(forceStaff.GetCastDelay() + travelTime, token);
                        }

                        canHit = call.CanHit(target);
                    }

                    if (canHit)
                    {
                        var bladeMail = this.hero.BladeMail;
                        if (bladeMail != null && bladeMail.CanBeCasted)
                        {
                            bladeMail.UseAbility();
                            await Task.Delay(bladeMail.GetCastDelay(), token);
                        }

                        var lotusOrb = this.hero.LotusOrb;
                        if (lotusOrb != null && lotusOrb.CanBeCasted)
                        {
                            lotusOrb.UseAbility(this.Owner);
                            await Task.Delay(lotusOrb.GetCastDelay(), token);
                        }

                        call.UseAbility();
                        await Task.Delay(call.GetCastDelay(), token);
                    }
                }
            }

            this.hero.Ensage.Orbwalker.Active.OrbwalkTo(target);
        }
    }
}