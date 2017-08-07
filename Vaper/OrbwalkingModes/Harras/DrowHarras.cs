// <copyright file="DrowHarras.cs" company="Ensage">
//    Copyright (c) 2017 Ensage.
// </copyright>

namespace Vaper.OrbwalkingModes.Harras
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

    public class DrowHarras : HarrasOrbwalkingMode
    {
        private static readonly ILog Log = AssemblyLogs.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private readonly Drow hero;

        public DrowHarras(Drow hero)
            : base(hero)
        {
            this.hero = hero;
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
                var useArrow = this.hero.ShouldUseFrostArrow(harrasTarget);
                if (useArrow)
                {
                    await this.hero.UseFrostArrow(harrasTarget, token);
                    return;
                }

                this.Orbwalker.OrbwalkTo(harrasTarget);
                return;
            }

            this.Orbwalker.OrbwalkTo(null);
        }
    }
}