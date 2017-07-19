// <copyright file="Axe.cs" company="Ensage">
//    Copyright (c) 2017 Ensage.
// </copyright>

namespace Vaper.Heroes
{
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;

    using Ensage;
    using Ensage.SDK.Abilities.Items;
    using Ensage.SDK.Abilities.npc_dota_hero_axe;
    using Ensage.SDK.Extensions;
    using Ensage.SDK.Helpers;
    using Ensage.SDK.Inventory.Metadata;

    using PlaySharp.Toolkit.Helper.Annotations;

    using Vaper.OrbwalkingModes;
    using Vaper.OrbwalkingModes.Combo;

    [PublicAPI]
    [ExportHero(HeroId.npc_dota_hero_axe)]
    public class Axe : BaseHero
    {
        [ItemBinding]
        public item_blade_mail BladeMail { get; private set; }

        [ItemBinding]
        public item_blink Blink { get; private set; }

        public axe_berserkers_call Call { get; private set; }

        public axe_culling_blade CullingBlade { get; private set; }

        [ItemBinding]
        public item_force_staff ForceStaff { get; private set; }

        [ItemBinding]
        public item_lotus_orb LotusOrb { get; private set; }

        protected override ComboOrbwalkingMode GetComboOrbwalkingMode()
        {
            return new AxeComboOrbwalker(this);
        }

        protected override void OnActivate()
        {
            base.OnActivate();

            this.Call = this.Ensage.AbilityFactory.GetAbility<axe_berserkers_call>();
            this.CullingBlade = this.Ensage.AbilityFactory.GetAbility<axe_culling_blade>();
        }

        protected override async Task OnKillsteal(CancellationToken token)
        {
            if (Game.IsPaused || !this.Owner.IsAlive || !this.CullingBlade.CanBeCasted)
            {
                await Task.Delay(125, token);
                return;
            }

            var forceStaffReady = (this.ForceStaff != null) && this.ForceStaff.CanBeCasted;
            var killstealTarget = EntityManager<Hero>.Entities.FirstOrDefault(
                x => x.IsAlive
                     && (x.Team != this.Owner.Team)
                     && !x.IsIllusion
                     && this.CullingBlade.CanHit(x)
                     && (forceStaffReady || !x.IsLinkensProtected())
                     && (this.CullingBlade.GetDamage(x) > x.Health));

            if (killstealTarget != null)
            {
                if (forceStaffReady && killstealTarget.IsLinkensProtected())
                {
                    if (this.ForceStaff.UseAbility(killstealTarget))
                    {
                        var castDelay = this.ForceStaff.GetCastDelay(killstealTarget);
                        await this.AwaitKillstealDelay(castDelay, token);
                    }
                }

                if (this.CullingBlade.UseAbility(killstealTarget))
                {
                    var castDelay = this.CullingBlade.GetCastDelay(killstealTarget);
                    await this.AwaitKillstealDelay(castDelay, token);
                }
            }

            await Task.Delay(125, token);
        }
    }
}