// <copyright file="AlchemistComboOrbwalker.cs" company="Ensage">
//    Copyright (c) 2017 Ensage.
// </copyright>

namespace Vaper.OrbwalkingModes
{
    using System.Linq;
    using System.Reflection;
    using System.Threading;
    using System.Threading.Tasks;

    using Ensage;
    using Ensage.SDK.Extensions;
    using Ensage.SDK.Helpers;

    using log4net;

    using PlaySharp.Toolkit.Logging;

    using Vaper.Heroes;

    public class AlchemistComboOrbwalker : VaperOrbwalkingMode
    {
        private static readonly ILog Log = AssemblyLogs.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private readonly Alchemist hero;

        public AlchemistComboOrbwalker(Alchemist hero)
            : base(hero, 2000.0f)
        {
            this.hero = hero;
        }

        public bool HasValidThrowTarget { get; private set; }

        public override async Task ExecuteAsync(CancellationToken token)
        {
            if (!await this.ShouldExecute(token))
            {
                return;
            }

            if ((this.CurrentTarget == null) || !this.CurrentTarget.IsVisible)
            {
                this.hero.Ensage.Orbwalker.Active.OrbwalkTo(null);
                return;
            }

            var manta = this.hero.Manta;
            if (this.Owner.IsSilenced() || this.Owner.IsRooted())
            {
                if ((manta != null) && manta.CanBeCasted)
                {
                    manta.UseAbility();
                    await Task.Delay(manta.GetCastDelay(), token);
                }
            }

            var rage = this.hero.Rage;
            if ((rage != null) && rage.CanBeCasted)
            {
                var enemiesClose = EntityManager<Hero>.Entities.Any(
                    x => x.IsVisible
                         && x.IsAlive
                         && !x.IsIllusion
                         && (x.Team != this.Owner.Team)
                         && (x.Distance2D(this.CurrentTarget) < 800));
                var healthPercent = this.Owner.HealthPercent();
                if ((healthPercent < 0.5f) || (enemiesClose && (healthPercent < 0.85f)))
                {
                    rage.UseAbility();
                    await Task.Delay(rage.GetCastDelay(), token);
                }
            }

            if (!this.CurrentTarget.IsStunned())
            {
                var abysal = this.hero.AbyssalBlade;
                if ((abysal != null) && abysal.CanBeCasted && abysal.CanHit(this.CurrentTarget))
                {
                    abysal.UseAbility(this.CurrentTarget);
                    await Task.Delay(abysal.GetCastDelay(this.CurrentTarget), token);
                }
            }

            var shadowBlade = this.hero.ShadowBlade;
            var breakBlade = this.hero.BreakBlade;
            var isInvisible = this.Owner.IsInvisible() || (this.Owner.InvisiblityLevel > 0);

            var channeling = false;
            var concoction = this.hero.Concoction;
            var acid = this.hero.Acid;
            if (concoction != null)
            {
                var targetDistance = this.Owner.Distance2D(this.CurrentTarget);
                if (concoction.CanBeCasted)
                {
                    // channel when we are far away enough
                    var useConcoction = (targetDistance > 800) && (targetDistance < (concoction.Duration * this.Owner.MovementSpeed * 0.75f));
                    if (!useConcoction)
                    {
                        // only throw when target is not almost dead or other hero units are close
                        useConcoction = (this.CurrentTarget.HealthPercent() > 0.25f)
                                        || EntityManager<Hero>.Entities.Any(
                                            x => x.IsVisible
                                                 && x.IsAlive
                                                 && (x != this.CurrentTarget)
                                                 && (x.Team != this.Owner.Team)
                                                 && (x.Distance2D(this.CurrentTarget) < concoction.CastRange));
                    }

                    if (useConcoction)
                    {
                        if ((targetDistance > 1000) || !this.Owner.IsVisibleToEnemies)
                        {
                            if ((shadowBlade != null) && shadowBlade.CanBeCasted)
                            {
                                shadowBlade.UseAbility();
                                await Task.Delay(shadowBlade.GetCastDelay(), token);
                                isInvisible = true;
                            }
                            else
                            {
                                if ((breakBlade != null) && breakBlade.CanBeCasted)
                                {
                                    breakBlade.UseAbility();
                                    await Task.Delay(breakBlade.GetCastDelay(), token);
                                    isInvisible = true;
                                }
                            }
                        }

                        if (isInvisible && (rage != null) && rage.CanBeCasted)
                        {
                            rage.UseAbility();
                            await Task.Delay(rage.GetCastDelay(), token);
                        }

                        concoction.UseAbility();
                        await Task.Delay(concoction.GetCastDelay(), token);
                        channeling = true;
                    }
                }
                else
                {
                    var throwAbility = concoction.ThrowAbility;
                    if ((throwAbility != null) && throwAbility.CanBeCasted)
                    {
                        this.HasValidThrowTarget = !this.CurrentTarget.IsReflectingAbilities()
                                                   && !this.CurrentTarget.IsLinkensProtected()
                                                   && !concoction.CanHit(this.CurrentTarget);
                        channeling = true;

                        var channelingDuration = this.hero.ConcoctionChannelingDuration;

                        // throw acid before when too close to the enemy
                        if ((acid != null) && acid.CanBeCasted && acid.CanHit(this.CurrentTarget))
                        {
                            if ((targetDistance < 400) && ((channelingDuration / concoction.Duration) > 0.7f))
                            {
                                if (isInvisible && (targetDistance <= this.Owner.AttackRange(this.CurrentTarget)))
                                {
                                    this.hero.Ensage.Orbwalker.Active.Attack(this.CurrentTarget);
                                    await Task.Delay((int)(this.Owner.GetAutoAttackArrivalTime(this.CurrentTarget) * 1000.0f * 2f), token);
                                }

                                if (acid.UseAbility(this.CurrentTarget))
                                {
                                    await Task.Delay(acid.GetCastDelay(this.CurrentTarget), token);
                                }
                            }
                        }

                        if (this.HasValidThrowTarget)
                        {
                            if (concoction.CanHit(this.CurrentTarget)
                                && ((concoction.GetDamage(channelingDuration, this.CurrentTarget) > this.CurrentTarget.Health)
                                    || (channelingDuration > (concoction.Duration * 0.85f))))
                            {
                                var waitTime = (int)(((concoction.ExplosionDuration - channelingDuration) * 1000.0f) - Game.Ping)
                                               - (throwAbility.GetCastDelay(this.CurrentTarget) * 2);
                                await Task.Delay(waitTime, token);

                                throwAbility.UseAbility(this.CurrentTarget);
                                await Task.Delay(throwAbility.GetCastDelay(this.CurrentTarget), token);
                                channeling = false;
                            }
                        }
                    }
                }
            }

            if (!isInvisible)
            {
                var med = this.hero.Medallion;
                if ((med != null) && med.CanBeCasted && med.CanHit(this.CurrentTarget))
                {
                    med.UseAbility(this.CurrentTarget);
                    await Task.Delay(med.GetCastDelay(this.CurrentTarget), token);
                }

                var solar = this.hero.SolarCrest;
                if ((solar != null) && solar.CanBeCasted && solar.CanHit(this.CurrentTarget))
                {
                    solar.UseAbility(this.CurrentTarget);
                    await Task.Delay(solar.GetCastDelay(this.CurrentTarget), token);
                }

                if (!channeling)
                {
                    if ((acid != null) && acid.CanBeCasted && acid.CanHit(this.CurrentTarget))
                    {
                        if (acid.UseAbility(this.CurrentTarget))
                        {
                            await Task.Delay(acid.GetCastDelay(this.CurrentTarget), token);
                        }
                    }
                }

                var mjollnir = this.hero.Mjollnir;
                if ((mjollnir != null) && mjollnir.CanBeCasted && mjollnir.CanHit(this.CurrentTarget))
                {
                    mjollnir.UseAbility(this.Owner);
                    await Task.Delay(mjollnir.GetCastDelay(), token);
                }
            }
            else
            {
                if (channeling && ((this.hero.ConcoctionChannelingDuration / concoction.Duration) < 0.8f))
                {
                    this.hero.Ensage.Orbwalker.Active.OrbwalkTo(null);
                    return;
                }
            }

            this.hero.Ensage.Orbwalker.Active.OrbwalkTo(this.CurrentTarget);
        }
    }
}