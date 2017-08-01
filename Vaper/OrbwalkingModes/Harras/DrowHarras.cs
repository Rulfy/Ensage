// <copyright file="DrowHarras.cs" company="Ensage">
//    Copyright (c) 2017 Ensage.
// </copyright>

namespace Vaper.OrbwalkingModes.Harras
{
    using System;
    using System.Linq;
    using System.Reflection;
    using System.Threading;
    using System.Threading.Tasks;

    using Ensage;
    using Ensage.Common.Extensions;
    using Ensage.SDK.Extensions;
    using Ensage.SDK.Helpers;

    using log4net;

    using PlaySharp.Toolkit.Logging;

    using Vaper.Heroes;

    public class DrowHarras : HarrasOrbwalkingMode
    {
        private static readonly ILog Log = AssemblyLogs.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private readonly Drow hero;

        private float lastFrostArrowTime = 0;

        public DrowHarras(Drow hero)
            : base(hero)
        {
            this.hero = hero;
        }

    
        public float ManaNeeded
        {
            get
            {
                var manaNeeded = 0.0f;

                if (this.hero.ShadowBlade != null)
                {
                    manaNeeded += this.hero.ShadowBlade.Ability.ManaCost;
                }
                else if (this.hero.SilverEdge != null)
                {
                    manaNeeded += this.hero.SilverEdge.Ability.ManaCost;
                }

                if (this.hero.HurricanePipe != null)
                {
                    manaNeeded += this.hero.HurricanePipe.Ability.ManaCost;
                }

                if (this.hero.Mom != null)
                {
                    manaNeeded += this.hero.Mom.Ability.ManaCost;
                }

                return manaNeeded;
            }
        }

        public override async Task ExecuteAsync(CancellationToken token)
        {
            var unitTarget = this.GetTarget();
            if (unitTarget != null)
            {
                this.Orbwalker.OrbwalkTo(unitTarget);
                return;
            }

            var harrasTarget = EntityManager<Hero>
                .Entities.Where(x => x.IsVisible && x.IsAlive && !x.IsIllusion && x.IsEnemy(this.Owner) && this.Owner.IsInAttackRange(x, this.BonusAttackRange))
                .OrderBy(x => x.Health)
                .FirstOrDefault();

            if (harrasTarget != null)
            {
                var creepsClose = EntityManager<Creep>.Entities.Any(x => x.IsVisible && x.IsAlive && x.IsSpawned && x.IsEnemy(this.Owner) && (x.Distance2D(this.Owner) <= 500.0f));

                // only use frost arrows if we have enough mana
                if (this.ManaNeeded <= (this.Owner.Mana - this.hero.FrostArrows.Ability.ManaCost))
                {
                    // dont pull aggro
                    var useArrow = creepsClose;
                    if (!useArrow)
                    {
                        useArrow = true;
                        //// check for maximum slow duration
                        //var modifier = harrasTarget.FindModifier(this.hero.FrostArrows.TargetModifierName);
                        //var duration = this.hero.FrostArrows.GetModifierDuration(harrasTarget);

                        //var hitTime = this.hero.FrostArrows.GetHitTime(harrasTarget) / 1000.0f;

                        //var timeDiff = Game.GameTime - this.lastFrostArrowTime;
                        //var arrivalTime = Math.Max(0, this.Owner.GetProjectileArrivalTime(harrasTarget, 0.0f, this.hero.ProjectileSpeed, false) - timeDiff);
                        //Log.Debug($"{this.Owner.GetProjectileArrivalTime(harrasTarget, 0.0f, this.hero.ProjectileSpeed, false)} - {timeDiff}");
                        //if (modifier == null)
                        //{
                        //    useArrow = (duration - arrivalTime) > 0;
                        //    Log.Debug($"1: {duration} - {arrivalTime} = {duration - arrivalTime} <= {0} => {useArrow}");
                        //}
                        //else
                        //{

                        //    //useArrow = (modifier.RemainingTime + timeDiff) <= hitTime;
                        //    //Log.Debug($"2: {modifier.RemainingTime} + {timeDiff} <= {hitTime} => {useArrow}");
                        //}

                    }

                    if (useArrow)
                    {
                        this.hero.FrostArrows.UseAbility(harrasTarget);
                        await Task.Delay(this.hero.FrostArrows.GetCastDelay(harrasTarget), token);
                        this.lastFrostArrowTime = Game.GameTime;
                        return;
                    }
                }

                this.Orbwalker.OrbwalkTo(harrasTarget);
                return;
            }

            this.Orbwalker.OrbwalkTo(null);
        }
    }
}