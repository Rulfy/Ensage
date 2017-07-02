// <copyright file="UndyingOrbwalker.cs" company="Ensage">
//    Copyright (c) 2017 Ensage.
// </copyright>

namespace Vaper.OrbwalkingModes
{
    using System;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;

    using Ensage;
    using Ensage.SDK.Extensions;
    using Ensage.SDK.Helpers;

    using Vaper.Heroes;

    public class UndyingOrbwalker : VaperOrbwalkingMode
    {
        private readonly Undying hero;

        public UndyingOrbwalker(Undying hero)
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
                this.hero.Ensage.Orbwalker.Active.OrbwalkTo(null);
                return;
            }

            if (!this.CurrentTarget.IsIllusion)
            {
                var decay = this.hero.Decay;
                if (decay.CanBeCasted && decay.CanHit(this.CurrentTarget))
                {
                    if (decay.UseAbility(this.CurrentTarget))
                    {
                        await Task.Delay(decay.GetCastDelay(this.CurrentTarget), token);
                    }
                }

                var tombstone = this.hero.Tombstone;
                if (tombstone.CanBeCasted && tombstone.CanHit(this.CurrentTarget))
                {
                    var enemyClose = EntityManager<Hero>.Entities.Any(
                        x => x.IsAlive
                             && x.IsVisible
                             && (x != this.CurrentTarget)
                             && this.Owner.IsEnemy(x)
                             && !x.IsIllusion
                             && (x.Distance2D(this.CurrentTarget) < tombstone.Radius));

                    if (enemyClose)
                    {
                        var pos = (this.Owner.Position - this.CurrentTarget.Position).Normalized();
                        pos = this.CurrentTarget.Position + (pos * Math.Max(300.0f, tombstone.Radius / 3));

                        tombstone.UseAbility(pos);
                        await Task.Delay(tombstone.GetCastDelay(pos), token);
                    }
                }
            }

            var soulRip = this.hero.SoulRip;
            if (soulRip.CanBeCasted)
            {
                if (!this.CurrentTarget.IsIllusion && soulRip.CanHit(this.CurrentTarget) && (soulRip.GetDamage(this.CurrentTarget) > this.CurrentTarget.Health))
                {
                    soulRip.UseAbility(this.CurrentTarget);
                    await Task.Delay(soulRip.GetCastDelay(this.CurrentTarget), token);
                }
                else
                {
                    var stone = EntityManager<Unit>.Entities.FirstOrDefault(x => x.IsAlive && x.IsVisible && (x.ClassId == ClassId.CDOTA_Unit_Undying_Tombstone));
                    if ((stone != null) && (((float)stone.Health / stone.MaximumHealth) <= 0.34f) && soulRip.CanHit(stone))
                    {
                        soulRip.UseAbility(stone);
                        await Task.Delay(soulRip.GetCastDelay(stone), token);
                    }
                    else if ((this.Owner.HealthPercent() < 0.5f) && (((float)soulRip.GetAffectedUnitCount(this.Owner) / soulRip.MaxUnits) >= 0.25f))
                    {
                        soulRip.UseAbility(this.Owner);
                        await Task.Delay(soulRip.GetCastDelay(), token);
                    }
                }
            }

            var ult = this.hero.Golem;
            if (ult.CanBeCasted && ult.CanHit(this.CurrentTarget))
            {
                var useUlt = this.Owner.HealthPercent() <= 0.6f;
                if (!useUlt)
                {
                    useUlt = EntityManager<Hero>.Entities.Any(
                        x => x.IsAlive
                             && x.IsVisible
                             && (x != this.CurrentTarget)
                             && this.Owner.IsEnemy(x)
                             && !x.IsIllusion
                             && (x.Distance2D(this.CurrentTarget) < ult.Radius));
                }

                if (useUlt)
                {
                    ult.UseAbility();
                    await Task.Delay(ult.GetCastDelay(), token);
                }
            }

            this.OrbwalkToTarget();
        }
    }
}