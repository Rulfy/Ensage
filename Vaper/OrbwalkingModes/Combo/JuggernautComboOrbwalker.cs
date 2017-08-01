// <copyright file="JuggernautComboOrbwalker.cs" company="Ensage">
//    Copyright (c) 2017 Ensage.
// </copyright>

namespace Vaper.OrbwalkingModes.Combo
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

    public class JuggernautOrbwalker : ComboOrbwalkingMode
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
            if (!await this.ShouldExecute(token))
            {
                return;
            }

            if (this.Owner.HasModifier(this.hero.OmniSlash.ModifierName))
            {
                if (this.CurrentTarget.IsEthereal())
                {
                    var diff = this.hero.Diffusal;
                    if ((diff != null) && diff.CanBeCasted && diff.CanHit(this.CurrentTarget))
                    {
                        diff.UseAbility(this.CurrentTarget);
                        await Task.Delay(diff.GetCastDelay(), token);
                    }
                }

                await Task.Delay(125, token);
                return;
            }

            var blink = this.hero.Blink;
            if (blink != null)
            {
                this.MaxTargetRange = blink.CastRange * 1.5f;
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

            var omni = this.hero.OmniSlash;
            var healthPercent = this.Owner.HealthPercent();
            var targetDistance = this.Owner.Distance2D(this.CurrentTarget);
            var attackRange = this.Owner.AttackRange(this.CurrentTarget);
            if ((omni != null) && !this.CurrentTarget.IsIllusion && omni.CanBeCasted && omni.CanHit(this.CurrentTarget))
            {
                // if we're low on health, always try to get ult of
                var useOmni = healthPercent < 0.10;
                if (!useOmni)
                {
                    // check if we can finish of the enemy without problems or need help with omni slash

                    // Log.Debug($"{targetDistance > attackRange * 1.5f} && {this.Owner.MovementSpeed < this.CurrentTarget.MovementSpeed * 1.2f} OR {this.CurrentTarget.Health > omni.GetTickDamage(this.CurrentTarget)}");
                    if (((targetDistance > (attackRange * 1.5f)) && (this.Owner.MovementSpeed < (this.CurrentTarget.MovementSpeed * 1.2f)))
                        || (this.CurrentTarget.Health > omni.GetTickDamage(this.CurrentTarget)))
                    {
                        var unitsClose = EntityManager<Unit>.Entities.Where(
                                                                x => x.IsVisible
                                                                     && x.IsAlive
                                                                     && (x != this.CurrentTarget)
                                                                     && this.Owner.IsEnemy(x)
                                                                     && (x.IsIllusion || !(x is Hero))
                                                                     && !(x is Building)
                                                                     && x.IsRealUnit()
                                                                     && (x.Distance2D(this.CurrentTarget) <= omni.Radius))
                                                            .ToList();

                        var abilityLevel = omni.Ability.Level - 1;

                        // Log.Debug($"unitsclose: {unitsClose.Count} ");
                        // Log.Debug($"{EntityManager<Entity>.Entities.Count()} - { EntityManager<Unit>.Entities.Count()} - { EntityManager<Hero>.Entities.Count()}");
                        if ((unitsClose.Count > 0) && (unitsClose.Count <= abilityLevel))
                        {
                            var close = unitsClose;
                            foreach (var unit in close)
                            {
                                var unitsInRadius = EntityManager<Unit>.Entities.Where(
                                    x => !unitsClose.Contains(x)
                                         && x.IsVisible
                                         && x.IsAlive
                                         && (x != this.CurrentTarget)
                                         && this.Owner.IsEnemy(x)
                                         && (x.IsIllusion || !(x is Hero))
                                         && !(x is Building)
                                         && x.IsRealUnit()
                                         && (x.Distance2D(unit) < omni.Radius));
                                unitsClose = unitsClose.Concat(unitsInRadius).ToList();

                                if (unitsClose.Count > abilityLevel)
                                {
                                    break;
                                }
                            }
                        }

                        useOmni = unitsClose.Count <= abilityLevel;
                        if (useOmni)
                        {
                            Log.Debug($"using omni with {unitsClose.Count} around {omni.Radius}");
                        }
                    }
                }

                if (useOmni)
                {
                    Unit omniTarget;
                    if (this.CurrentTarget.IsReflectingAbilities())
                    {
                        omniTarget = EntityManager<Unit>
                            .Entities.Where(
                                x => x.IsVisible
                                     && x.IsAlive
                                     && this.Owner.IsEnemy(x)
                                     && !(x is Building)
                                     && x.IsRealUnit()
                                     && !x.IsReflectingAbilities()
                                     && (x.Distance2D(this.Owner) < omni.CastRange))
                            .OrderBy(x => x.Distance2D(this.CurrentTarget))
                            .FirstOrDefault();
                    }
                    else
                    {
                        omniTarget = this.CurrentTarget;
                    }

                    if (omniTarget != null)
                    {
                        omni.UseAbility(omniTarget);
                        await Task.Delay(omni.GetCastDelay(omniTarget), token);
                    }
                }
            }

            var bladeFury = this.hero.BladeFury;
            if ((blink != null) && !this.CurrentTarget.IsIllusion && blink.CanBeCasted && blink.CanHit(this.CurrentTarget))
            {
                // only blink when we can call or use ult to kill him
                var useBlink = (omni != null) && omni.CanBeCasted && !omni.CanHit(this.CurrentTarget);
                if (!useBlink)
                {
                    if (targetDistance > 600)
                    {
                        var enemyCount = EntityManager<Hero>.Entities.Count(
                            x => x.IsAlive
                                 && x.IsVisible
                                 && (x != this.CurrentTarget)
                                 && this.Owner.IsEnemy(x)
                                 && !x.IsIllusion
                                 && (x.Distance2D(this.CurrentTarget) < 800));
                        useBlink = (enemyCount <= 1) || ((bladeFury != null) && bladeFury.CanBeCasted);
                    }
                }

                if (useBlink)
                {
                    var blinkPos = this.CurrentTarget.IsMoving ? this.CurrentTarget.InFront(75) : this.CurrentTarget.Position;
                    blink.UseAbility(blinkPos);
                    await Task.Delay(blink.GetCastDelay(blinkPos), token);
                }
            }

            var healingWard = this.hero.HealingWard;
            if ((healingWard != null) && healingWard.CanBeCasted)
            {
                var recentDmgPercent = (float)this.hero.Owner.RecentDamage / this.hero.Owner.MaximumHealth;

                // Log.Debug($"RecentDmgPercent: {recentDmgPercent}");
                if ((healthPercent < 0.2f) || (recentDmgPercent > 0.2))
                {
                    healingWard.UseAbility(this.Owner.Position);
                    await Task.Delay(healingWard.GetCastDelay(this.Owner.Position), token);
                }
            }

            if (!this.CurrentTarget.IsStunned() && !this.CurrentTarget.IsIllusion)
            {
                var abysal = this.hero.AbyssalBlade;
                if ((abysal != null) && abysal.CanBeCasted && abysal.CanHit(this.CurrentTarget))
                {
                    abysal.UseAbility(this.CurrentTarget);
                    await Task.Delay(abysal.GetCastDelay(this.CurrentTarget), token);
                }
            }

            var diffusal = this.hero.Diffusal;
            var manta = this.hero.Manta;
            if ((manta != null) && manta.CanBeCasted && (((diffusal != null) && (targetDistance < attackRange)) || this.Owner.IsSilenced() || this.Owner.IsRooted()))
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
            if ((mjollnir != null) && mjollnir.CanBeCasted && mjollnir.CanHit(this.CurrentTarget))
            {
                mjollnir.UseAbility(this.Owner);
                await Task.Delay(mjollnir.GetCastDelay(), token);
            }

            if ((diffusal != null) && !this.CurrentTarget.IsIllusion && diffusal.CanBeCasted && diffusal.CanHit(this.CurrentTarget))
            {
                var useDiffu = this.CurrentTarget.IsEthereal() && !this.Owner.HasModifier(bladeFury.ModifierName);

                // Log.Debug($"{targetDistance} > {attackRange * 1.2f} && {this.CurrentTarget.MovementSpeed} > {this.Owner.MovementSpeed * 1.1f}");
                if (!useDiffu
                    && !this.CurrentTarget.IsStunned()
                    && this.CurrentTarget.IsMoving
                    && !this.CurrentTarget.HasModifier(diffusal.TargetModifierName)
                    && (targetDistance > (attackRange * 1.4f))
                    && (this.CurrentTarget.MovementSpeed > this.Owner.MovementSpeed))
                {
                    useDiffu = true;
                }

                if (useDiffu)
                {
                    diffusal.UseAbility(this.CurrentTarget);
                    await Task.Delay(diffusal.GetCastDelay(this.CurrentTarget), token);
                }
            }

            if ((bladeFury != null) && bladeFury.CanBeCasted && bladeFury.CanHit(this.CurrentTarget))
            {
                // Log.Debug($"bf dmg vs autoattack: {bladeFury.GetTickDamage(this.CurrentTarget)} > {this.Owner.GetAttackDamage(this.CurrentTarget) * bladeFury.TickRate}");
                var enemyCount = EntityManager<Hero>.Entities.Count(
                    x => x.IsAlive
                         && x.IsVisible
                         && (x != this.CurrentTarget)
                         && this.Owner.IsEnemy(x)
                         && !x.IsIllusion
                         && (x.Distance2D(this.CurrentTarget) < 800));
                if ((enemyCount > 1)
                    || (!this.CurrentTarget.IsIllusion
                        && (bladeFury.GetTickDamage(this.CurrentTarget) > (this.Owner.GetAttackDamage(this.CurrentTarget) * bladeFury.TickRate))
                        && (bladeFury.GetTotalDamage(this.CurrentTarget) >= (0.5f * this.CurrentTarget.Health))))
                {
                    bladeFury.UseAbility();
                    await Task.Delay(bladeFury.GetCastDelay(), token);
                }
            }

            if (this.hero.BladeFuryMoveOnly && this.Owner.HasModifier(bladeFury.ModifierName))
            {
                this.hero.Context.Orbwalker.Active.OrbwalkTo(null);
            }
            else
            {
                this.OrbwalkToTarget();
            }
        }
    }
}