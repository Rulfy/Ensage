// <copyright file="CrystalMaidenOrbwalker.cs" company="Ensage">
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

    public class CrystalMaidenOrbwalker : VaperOrbwalkingMode
    {
        private static readonly ILog Log = AssemblyLogs.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private readonly CrystalMaiden hero;

        public CrystalMaidenOrbwalker(CrystalMaiden hero)
            : base(hero, 1500.0f)
        {
            this.hero = hero;
        }

        public override async Task ExecuteAsync(CancellationToken token)
        {
            if (!await this.ShouldExecute(token))
            {
                return;
            }

            if (this.Owner.IsChanneling())
            {
                await Task.Delay(125, token);
                return;
            }

            var blink = this.hero.Blink;
            if (blink != null)
            {
                this.MaxTargetRange = blink.CastRange * 1.3f;
            }

            if ((this.CurrentTarget == null) || !this.CurrentTarget.IsVisible)
            {
                this.hero.Ensage.Orbwalker.Active.OrbwalkTo(null);
                return;
            }

            var ult = this.hero.FreezingField;
            var veil = this.hero.Veil;
            var frostbite = this.hero.Frostbite;

            if (!this.CurrentTarget.IsIllusion)
            {
                // blink ult combo
                if ((blink != null) && blink.CanBeCasted && blink.CanHit(this.CurrentTarget))
                {
                    var useBlink = false;
                    if ((ult != null) && ult.CanBeCasted)
                    {
                        var enemyCount = EntityManager<Hero>.Entities.Count(
                            x => x.IsAlive
                                 && x.IsVisible
                                 && (x != this.CurrentTarget)
                                 && this.Owner.IsEnemy(x)
                                 && !x.IsIllusion
                                 && (x.Distance2D(this.CurrentTarget) <= ult.Radius));

                        useBlink = ((this.CurrentTarget.Health * 3) > ult.GetDamage(this.CurrentTarget)) && (enemyCount == 0);

                        // Log.Debug($"{useBlink} - {this.CurrentTarget.Health*3} > {ult.GetDamage(this.CurrentTarget)} && {enemyCount}");
                        if (!useBlink)
                        {
                            var allyCount = EntityManager<Hero>.Entities.Count(
                                x => x.IsAlive
                                     && x.IsVisible
                                     && (x != this.Owner)
                                     && (this.Owner.Team == x.Team)
                                     && !x.IsIllusion
                                     && (x.Distance2D(this.CurrentTarget) <= ult.Radius));

                            useBlink = (enemyCount >= 1) && (enemyCount <= allyCount);
                        }
                    }

                    if (useBlink)
                    {
                        var bkb = this.hero.Bkb;
                        if ((bkb != null) && bkb.CanBeCasted)
                        {
                            bkb.UseAbility();
                            await Task.Delay(bkb.GetCastDelay(), token);
                        }

                        var glimmer = this.hero.GlimmerCape;
                        if ((glimmer != null) && this.Owner.CanCastAbilities(glimmer, ult))
                        {
                            glimmer.UseAbility(this.Owner);
                            await Task.Delay(glimmer.GetCastDelay(), token);
                        }

                        var blinkPos = (this.Owner.Position - this.CurrentTarget.Position).Normalized();
                        blinkPos = this.CurrentTarget.Position + (blinkPos * ult.MinimumExplosionDistance * 2.0f);
                        blink.UseAbility(blinkPos);
                        await Task.Delay(blink.GetCastDelay(blinkPos), token);

                        if ((veil != null) && this.Owner.CanCastAbilities(veil, ult) && veil.CanHit(this.CurrentTarget))
                        {
                            veil.UseAbility(this.CurrentTarget.Position);
                            await Task.Delay(veil.GetCastDelay(this.CurrentTarget.Position), token);
                        }

                        var lotus = this.hero.Lotus;
                        if ((lotus != null) && this.Owner.CanCastAbilities(lotus, ult) && lotus.CanHit(this.CurrentTarget))
                        {
                            lotus.UseAbility(this.Owner);
                            await Task.Delay(lotus.GetCastDelay(), token);
                        }

                        if ((frostbite != null) && this.Owner.CanCastAbilities(frostbite, ult) && frostbite.CanHit(this.CurrentTarget))
                        {
                            frostbite.UseAbility(this.CurrentTarget);
                            await Task.Delay(frostbite.GetCastDelay(this.CurrentTarget) + 500, token); // bro science
                        }

                        ult.UseAbility();
                        await Task.Delay(ult.GetCastDelay() + 500, token);
                        return;
                    }
                }
            }

            var notDisabled = !this.CurrentTarget.IsStunned() && !this.CurrentTarget.IsRooted();
            if (notDisabled)
            {
                if ((veil != null) && this.Owner.CanCastAbilities(veil, ult) && veil.CanHit(this.CurrentTarget))
                {
                    veil.UseAbility(this.CurrentTarget.Position);
                    await Task.Delay(veil.GetCastDelay(this.CurrentTarget.Position), token);
                }

                if ((frostbite != null) && frostbite.CanBeCasted && frostbite.CanHit(this.CurrentTarget))
                {
                    frostbite.UseAbility(this.CurrentTarget);
                    await Task.Delay(frostbite.GetCastDelay(this.CurrentTarget) + 250, token); // bro science 
                    return;
                }

                var nova = this.hero.Nova;
                if ((nova != null) && nova.CanBeCasted && nova.CanHit(this.CurrentTarget))
                {
                    if (nova.UseAbility(this.CurrentTarget))
                    {
                        await Task.Delay(nova.GetCastDelay(this.CurrentTarget), token);
                    }
                }
            }

            if (!this.CurrentTarget.IsIllusion)
            {
                // Log.Debug($"disabled {ult != null} && {ult.CanBeCasted} && {ult.CanHit(this.CurrentTarget)}");
                if ((ult != null) && ult.CanBeCasted && ult.CanHit(this.CurrentTarget))
                {
                    var enemyCount = EntityManager<Hero>.Entities.Count(
                        x => x.IsAlive
                             && x.IsVisible
                             && (x != this.CurrentTarget)
                             && this.Owner.IsEnemy(x)
                             && !x.IsIllusion
                             && (x.Distance2D(this.Owner) <= ult.Radius));

                    if (enemyCount >= 1 || (!notDisabled && (this.CurrentTarget.Health * 3) > ult.GetDamage(this.CurrentTarget)))
                    {
                        var bkb = this.hero.Bkb;
                        if ((bkb != null) && bkb.CanBeCasted)
                        {
                            bkb.UseAbility();
                            await Task.Delay(bkb.GetCastDelay(), token);
                        }

                        var glimmer = this.hero.GlimmerCape;
                        if ((glimmer != null) && this.Owner.CanCastAbilities(glimmer, ult))
                        {
                            glimmer.UseAbility(this.Owner);
                            await Task.Delay(glimmer.GetCastDelay(), token);
                        }

                        if ((veil != null) && this.Owner.CanCastAbilities(veil, ult) && veil.CanHit(this.CurrentTarget))
                        {
                            veil.UseAbility(this.CurrentTarget.Position);
                            await Task.Delay(veil.GetCastDelay(this.CurrentTarget.Position), token);
                        }

                        var lotus = this.hero.Lotus;
                        if ((lotus != null) && this.Owner.CanCastAbilities(lotus, ult) && lotus.CanHit(this.CurrentTarget))
                        {
                            lotus.UseAbility(this.Owner);
                            await Task.Delay(lotus.GetCastDelay(), token);
                        }

                        if ((frostbite != null) && this.Owner.CanCastAbilities(frostbite, ult) && frostbite.CanHit(this.CurrentTarget))
                        {
                            frostbite.UseAbility(this.CurrentTarget);
                            await Task.Delay(frostbite.GetCastDelay(this.CurrentTarget) + 500, token); // bro science
                        }

                        ult.UseAbility();
                        await Task.Delay(ult.GetCastDelay() + 500, token);
                        return;
                    }
                }
            }

            this.OrbwalkToTarget();
        }
    }
}