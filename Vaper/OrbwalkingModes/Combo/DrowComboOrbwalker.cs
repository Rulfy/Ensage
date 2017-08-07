// <copyright file="DrowComboOrbwalker.cs" company="Ensage">
//    Copyright (c) 2017 Ensage.
// </copyright>

namespace Vaper.OrbwalkingModes.Combo
{
    using System.Reflection;
    using System.Threading;
    using System.Threading.Tasks;

    using Ensage.SDK.Extensions;

    using log4net;

    using PlaySharp.Toolkit.Logging;

    using Vaper.Heroes;

    public class DrowComboOrbwalker : ComboOrbwalkingMode
    {
        private static readonly ILog Log = AssemblyLogs.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private readonly Drow hero;

        public DrowComboOrbwalker(Drow hero)
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

            if ((this.CurrentTarget == null) || !this.CurrentTarget.IsVisible)
            {
                this.hero.Context.Orbwalker.Active.OrbwalkTo(null);
                return;
            }

            var distance = this.CurrentTarget.Distance2D(this.Owner);
            var hurricanePipe = this.hero.HurricanePipe;
            if (hurricanePipe != null)
            {
                if (this.Owner.HasModifier(hurricanePipe.ModifierName))
                {
                    this.Owner.Attack(this.CurrentTarget);
                    await Task.Delay(125, token);
                    return;
                }

                if (hurricanePipe.CanBeCasted && hurricanePipe.CanHit(this.CurrentTarget) && (this.hero.UltStatus == UltStatus.Deactivated) && (distance <= this.hero.Ult.Radius))
                {
                    hurricanePipe.UseAbility(this.CurrentTarget);
                    await Task.Delay(hurricanePipe.GetCastDelay(this.CurrentTarget), token);
                    return;
                }
            }

            if (this.hero.Silence.CanHit(this.CurrentTarget) && (this.hero.UltStatus == UltStatus.Deactivated))
            {
                var manta = this.hero.Manta;
                var usedManta = false;
                if (this.Owner.IsSilenced() && (manta != null) && manta.CanBeCasted && (this.hero.Silence.Ability.Cooldown == 0))
                {
                    manta.UseAbility();
                    await Task.Delay(manta.GetCastDelay(), token);
                    usedManta = true;
                }

                if (usedManta || this.hero.Silence.CanBeCasted)
                {
                    this.hero.Silence.UseAbility(this.CurrentTarget);
                    await Task.Delay(this.hero.Silence.GetCastDelay(this.CurrentTarget), token);
                    return;
                }
            }

            var mom = this.hero.Mom;
            if ((mom != null) && mom.CanBeCasted && !this.hero.Silence.CanBeCasted)
            {
                mom.UseAbility();
                await Task.Delay(mom.GetCastDelay(), token);
            }

            var satanic = this.hero.Satanic;
            if ((satanic != null) && satanic.CanBeCasted && (this.Owner.HealthPercent() < 0.35f))
            {
                satanic.UseAbility();
                await Task.Delay(satanic.GetCastDelay(), token);
            }

            var mjollnir = this.hero.Mjollnir;
            if (mjollnir != null && mjollnir.CanBeCasted && this.hero.UltStatus >= UltStatus.Danger)
            {
                mjollnir.UseAbility(this.Owner);
                await Task.Delay(mjollnir.GetCastDelay(), token);
            }

            var useArrows = this.hero.ShouldUseFrostArrow(this.CurrentTarget, true);
            if (useArrows)
            {
                await this.hero.UseFrostArrow(this.CurrentTarget, token);
                return;
            }

            this.OrbwalkToTarget();
        }
    }
}