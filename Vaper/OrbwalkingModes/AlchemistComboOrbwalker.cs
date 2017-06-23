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
            : base(hero)
        {
            this.hero = hero;
        }

        public bool HasValidThrowTarget { get; private set; }

        public override async Task ExecuteAsync(CancellationToken token)
        {
            if (!this.hero.Owner.IsAlive)
            {
                this.CurrentTarget = null;
                await Task.Delay(125, token);
                return;
            }

            var maxRange = 2000;

            var target = this.hero.Ensage.TargetSelector.Active.GetTargets().FirstOrDefault(x => x.Distance2D(this.Owner) <= maxRange);
            this.CurrentTarget = target;
            if (target == null)
            {
                this.hero.Ensage.Orbwalker.Active.OrbwalkTo(null);
                return;
            }

            var manta = this.hero.Manta;
            if (this.Owner.IsSilenced() || this.Owner.IsRooted())
            {
                if (manta != null && manta.CanBeCasted)
                {
                    manta.UseAbility();
                    await Task.Delay(manta.GetCastDelay(), token);
                }
            }

            var rage = this.hero.Rage;
            if (rage != null && rage.CanBeCasted)
            {
                var enemiesClose = EntityManager<Hero>.Entities.Any(x => x.IsVisible && x.IsAlive && !x.IsIllusion && x.Team != this.Owner.Team && x.Distance2D(target) < 800);
                var healthPercent = this.Owner.HealthPercent();
                if (healthPercent < 0.5f || (enemiesClose && healthPercent < 0.85f))
                {
                    rage.UseAbility();
                    await Task.Delay(rage.GetCastDelay(), token);
                }
            }

            if (!target.IsStunned())
            {
                var abysal = this.hero.AbyssalBlade;
                if (abysal != null && abysal.CanBeCasted && abysal.CanHit(target))
                {
                    abysal.UseAbility(target);
                    await Task.Delay(abysal.GetCastDelay(target), token);
                }
            }

            var shadowBlade = this.hero.ShadowBlade;
            var breakBlade = this.hero.BreakBlade;
            var isInvisible = this.Owner.IsInvisible() || this.Owner.InvisiblityLevel > 0;

            var channeling = false;
            var concoction = this.hero.Concoction;
            var acid = this.hero.Acid;
            if (concoction != null)
            {
                var targetDistance = this.Owner.Distance2D(target);
                if (concoction.CanBeCasted)
                {
                    // channel when we are far away enough
                    var useConcoction = targetDistance > 800 && targetDistance < (concoction.Duration * this.Owner.MovementSpeed * 0.75f);
                    if (!useConcoction)
                    {
                        // only throw when target is not almost dead or other hero units are close
                        useConcoction = target.HealthPercent() > 0.25f
                                        || EntityManager<Hero>.Entities.Any(
                                            x => x.IsVisible
                                                 && x.IsAlive
                                                 && x != target
                                                 && x.Team != this.Owner.Team
                                                 && x.Distance2D(target) < concoction.CastRange);
                    }

                    if (useConcoction)
                    {
                        if (targetDistance > 1000 || !this.Owner.IsVisibleToEnemies)
                        {
                            if (shadowBlade != null && shadowBlade.CanBeCasted)
                            {
                                shadowBlade.UseAbility();
                                await Task.Delay(shadowBlade.GetCastDelay(), token);
                                isInvisible = true;
                            }
                            else
                            {
                                if (breakBlade != null && breakBlade.CanBeCasted)
                                {
                                    breakBlade.UseAbility();
                                    await Task.Delay(breakBlade.GetCastDelay(), token);
                                    isInvisible = true;
                                }
                            }
                        }

                        if (isInvisible && rage != null && rage.CanBeCasted)
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
                    if (throwAbility != null && throwAbility.CanBeCasted)
                    {
                        this.HasValidThrowTarget = !target.IsReflectingAbilities() && !target.IsLinkensProtected() && !concoction.CanHit(target);
                        channeling = true;

                        var channelingDuration = this.hero.ConcoctionChannelingDuration;

                        // throw acid before when too close to the enemy
                        if (acid != null && acid.CanBeCasted && acid.CanHit(target))
                        {
                            if (targetDistance < 400 && (channelingDuration / concoction.Duration) > 0.7f)
                            {
                                if (isInvisible && targetDistance <= this.Owner.AttackRange(target))
                                {
                                    this.hero.Ensage.Orbwalker.Active.Attack(target);
                                    await Task.Delay((int)(this.Owner.GetAutoAttackArrivalTime(target) * 1000.0f * 2f), token);
                                }

                                if (acid.UseAbility(target))
                                {
                                    await Task.Delay(acid.GetCastDelay(target), token);
                                }
                            }
                        }

                        if (this.HasValidThrowTarget)
                        {
                            if (concoction.CanHit(target)
                                && (concoction.GetDamage(channelingDuration, target) > target.Health || channelingDuration > (concoction.Duration * 0.85f)))
                            {
                                var waitTime = (int)(((concoction.ExplosionDuration - channelingDuration) * 1000.0f) - Game.Ping) - (throwAbility.GetCastDelay(target) * 2);
                                await Task.Delay(waitTime, token);

                                throwAbility.UseAbility(target);
                                await Task.Delay(throwAbility.GetCastDelay(target), token);
                                channeling = false;
                            }
                        }
                    }
                }
            }

            if (!isInvisible)
            {
                var med = this.hero.Medallion;
                if (med != null && med.CanBeCasted && med.CanHit(target))
                {
                    med.UseAbility(target);
                    await Task.Delay(med.GetCastDelay(target), token);
                }

                var solar = this.hero.SolarCrest;
                if (solar != null && solar.CanBeCasted && solar.CanHit(target))
                {
                    solar.UseAbility(target);
                    await Task.Delay(solar.GetCastDelay(target), token);
                }

                if (!channeling)
                {
                    if (acid != null && acid.CanBeCasted && acid.CanHit(target))
                    {
                        if (acid.UseAbility(target))
                        {
                            await Task.Delay(acid.GetCastDelay(target), token);
                        }
                    }
                }

                var mjollnir = this.hero.Mjollnir;
                if (mjollnir != null && mjollnir.CanBeCasted && mjollnir.CanHit(target))
                {
                    mjollnir.UseAbility(this.Owner);
                    await Task.Delay(mjollnir.GetCastDelay(), token);
                }
            }
            else
            {
                if (channeling && (this.hero.ConcoctionChannelingDuration / concoction.Duration) < 0.8f)
                {
                    this.hero.Ensage.Orbwalker.Active.OrbwalkTo(null);
                    return;
                }
            }

            this.hero.Ensage.Orbwalker.Active.OrbwalkTo(target);
        }
    }
}