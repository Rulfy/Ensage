// <copyright file="JuggernautOrbwalker.cs" company="Ensage">
//    Copyright (c) 2017 Ensage.
// </copyright>

namespace Vaper.OrbwalkingModes
{
    using System;
    using System.Linq;
    using System.Reflection;
    using System.Threading;
    using System.Threading.Tasks;

    using Ensage;
    using Ensage.SDK.Abilities.Items;
    using Ensage.SDK.Extensions;
    using Ensage.SDK.Helpers;

    using log4net;

    using PlaySharp.Toolkit.Logging;

    using Vaper.Heroes;

    public class JuggernautOrbwalker : VaperOrbwalkingMode
    {
        private static readonly ILog Log = AssemblyLogs.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private readonly Juggernaut hero;

        public JuggernautOrbwalker(Juggernaut hero)
            : base(hero)
        {
            this.hero = hero;
        }

        public override async Task ExecuteAsync(CancellationToken token)
        {
            if (!this.hero.Owner.IsAlive)
            {
                this.CurrentTarget = null;
                await Task.Delay(125, token);
                return;
            }

            if (this.Owner.HasModifier(this.hero.OmniSlash.ModifierName))
            {
                await Task.Delay(125, token);
                return;
            }

            var blink = this.hero.Blink;
            var maxRange = blink?.CastRange * 1.5f ?? 1000.0f;

            var target = this.hero.Ensage.TargetSelector.Active.GetTargets().FirstOrDefault(x => x.Distance2D(this.Owner) <= maxRange);
            this.CurrentTarget = target;
            if (target == null)
            {
                this.hero.Ensage.Orbwalker.Active.OrbwalkTo(null);
                return;
            }

            var omni = this.hero.OmniSlash;
            var healthPercent = this.Owner.HealthPercent();
            var targetDistance = this.Owner.Distance2D(target);
            var attackRange = this.Owner.AttackRange(target);
            if (omni != null && omni.CanBeCasted && omni.CanHit(target))
            {
                // if we're low on health, always try to get ult of
                var useOmni = healthPercent < 0.10;
                if (!useOmni)
                {
                    // check if we can finish of the enemy without problems or need help with omni slash
                   
                    Log.Debug($"{targetDistance > attackRange * 1.5f} && {this.Owner.MovementSpeed < target.MovementSpeed * 1.2f} OR {target.Health > omni.GetTickDamage(target)}");

                    if ((targetDistance > attackRange * 1.5f && this.Owner.MovementSpeed < target.MovementSpeed * 1.2f)
                        || target.Health > omni.GetTickDamage(target))
                    {
                        var unitCount = EntityManager<Unit>.Entities.Count(
                            x => x.IsVisible && x.IsAlive && x.Team != this.Owner.Team && (x.IsIllusion || !(x is Hero)) && !(x is Building) && x.IsRealUnit() && x.Distance2D(this.Owner) < omni.CastRange);

                        useOmni = unitCount <= omni.Ability.Level;
                    }
                }

                if (useOmni)
                {
                    Unit omniTarget;
                    if (target.IsReflectingAbilities())
                    {
                        omniTarget = EntityManager<Unit>.Entities.Where(
                            x => x.IsVisible && x.IsAlive && x.Team != this.Owner.Team && !(x is Building) && x.IsRealUnit() && !x.IsReflectingAbilities() && x.Distance2D(this.Owner) < omni.CastRange)
                            .OrderBy(x => x.Distance2D(target)).FirstOrDefault();    
                    }
                    else
                    {
                        omniTarget = target;
                    }

                    if (omniTarget != null)
                    {
                        omni.UseAbility(omniTarget);
                        await Task.Delay(omni.GetCastDelay(omniTarget), token);
                    }
                }
            }

            var bladeFury = this.hero.BladeFury;
            if (blink != null && blink.CanBeCasted && blink.CanHit(target))
            {
                // only blink when we can call or use ult to kill him
                var useBlink = omni != null && omni.CanBeCasted && !omni.CanHit(target);
                if (!useBlink)
                {
                    if (targetDistance > 600)
                    {
                        var enemyCount = EntityManager<Hero>.Entities.Count(x => x.IsAlive && x.IsVisible && x != target && x.Team != this.Owner.Team && !x.IsIllusion && x.Distance2D(target) < 800);
                        useBlink = enemyCount <= 1 || (bladeFury != null && bladeFury.CanBeCasted);
                    }
                }
                if (useBlink)
                {
                    var blinkPos = target.IsMoving ? target.InFront(75) : target.Position;
                    blink.UseAbility(blinkPos);
                    await Task.Delay(blink.GetCastDelay(blinkPos), token);
                }
            }

            var healingWard = this.hero.HealingWard;
            if (healingWard != null && healingWard.CanBeCasted)
            {
                var recentDmgPercent = (float)this.hero.Owner.RecentDamage / this.hero.Owner.MaximumHealth;
                Log.Debug($"RecentDmgPercent: {recentDmgPercent}");
                if (healthPercent < 0.2f || recentDmgPercent > 0.2)
                {
                    healingWard.UseAbility(this.Owner.Position);
                    await Task.Delay(healingWard.GetCastDelay(this.Owner.Position), token);
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

            var diffusal = this.hero.Diffusal;
            var manta = this.hero.Manta;
            if (manta != null && manta.CanBeCasted && (diffusal != null || this.Owner.IsSilenced() || this.Owner.IsRooted()))
            {
                var isSilenced = this.Owner.IsSilenced();
                manta.UseAbility();
                await Task.Delay(manta.GetCastDelay(), token);

                // so we can immediately check for omni again
                if (isSilenced)
                {
                    return;
                }
            }

            var mjollnir = this.hero.Mjollnir;
            if (mjollnir != null && mjollnir.CanBeCasted && mjollnir.CanHit(target))
            {
                mjollnir.UseAbility(this.Owner);
                await Task.Delay(mjollnir.GetCastDelay(), token);
            }

            if (diffusal != null && !target.IsStunned() && target.IsMoving && diffusal.CanBeCasted && diffusal.CanHit(target))
            {
                if (!target.HasModifier(diffusal.TargetModifierName) && targetDistance > attackRange * 1.2f)
                {
                    diffusal.UseAbility(target);
                    await Task.Delay(diffusal.GetCastDelay(target), token);
                }
            }

            if (bladeFury != null && bladeFury.CanBeCasted && bladeFury.CanHit(target))
            {
                Log.Debug($"bf dmg vs autoattack: {bladeFury.GetTickDamage(target)} > {this.Owner.GetAttackDamage(target) * bladeFury.TickRate}");
                var enemyCount = EntityManager<Hero>.Entities.Count(x => x.IsAlive && x.IsVisible && x != target && x.Team != this.Owner.Team && !x.IsIllusion && x.Distance2D(target) < 800);
                if (enemyCount > 1 || bladeFury.GetTickDamage(target) > this.Owner.GetAttackDamage(target) * bladeFury.TickRate )
                {
                    bladeFury.UseAbility();
                    await Task.Delay(bladeFury.GetCastDelay(), token);
                }
            }

            this.hero.Ensage.Orbwalker.Active.OrbwalkTo(target);
        }
    }
}