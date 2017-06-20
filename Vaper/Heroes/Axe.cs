// <copyright file="Axe.cs" company="Ensage">
//    Copyright (c) 2017 Ensage.
// </copyright>

namespace Vaper.Heroes
{
    using System.Collections.Specialized;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;

    using Ensage;
    using Ensage.Common.Extensions;
    using Ensage.SDK.Abilities.Items;
    using Ensage.SDK.Abilities.npc_dota_hero_axe;
    using Ensage.SDK.Helpers;
    using Ensage.SDK.Inventory;

    using Vaper.OrbwalkingModes;

    [ExportHero(HeroId.npc_dota_hero_axe)]
    public class Axe : BaseHero
    {
        internal item_blade_mail BladeMail { get; private set; }

        internal item_blink Blink { get; private set; }

        internal axe_berserkers_call Call { get; private set; }

        internal axe_culling_blade CullingBlade { get; private set; }

        internal item_force_staff ForceStaff { get; private set; }

        internal item_lotus_orb LotusOrb { get; private set; }

        protected override VaperOrbwalkingMode GetOrbwalkingMode()
        {
            return new AxeComboOrbwalkingMode(this);
        }

        protected override void InventoryChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.Action == NotifyCollectionChangedAction.Add)
            {
                foreach (var newItem in e.NewItems.OfType<InventoryItem>())
                {
                    switch (newItem.Id)
                    {
                        case AbilityId.item_blade_mail:
                            this.BladeMail = this.Ensage.AbilityFactory.GetAbility<item_blade_mail>(newItem.Item);
                            break;
                        case AbilityId.item_blink:
                            this.Blink = this.Ensage.AbilityFactory.GetAbility<item_blink>(newItem.Item);
                            break;
                        case AbilityId.item_lotus_orb:
                            this.LotusOrb = this.Ensage.AbilityFactory.GetAbility<item_lotus_orb>(newItem.Item);
                            break;
                        case AbilityId.item_force_staff:
                            this.ForceStaff = this.Ensage.AbilityFactory.GetAbility<item_force_staff>(newItem.Item);
                            break;
                    }
                }
            }
            else if (e.Action == NotifyCollectionChangedAction.Remove)
            {
                foreach (var oldItem in e.OldItems.OfType<InventoryItem>())
                {
                    switch (oldItem.Id)
                    {
                        case AbilityId.item_blade_mail:
                            this.BladeMail = null;
                            break;
                        case AbilityId.item_blink:
                            this.Blink = null;
                            break;
                        case AbilityId.item_lotus_orb:
                            this.LotusOrb = null;
                            break;
                        case AbilityId.item_force_staff:
                            this.ForceStaff = null;
                            break;
                    }
                }
            }
        }

        protected override void OnActivate()
        {
            base.OnActivate();

            this.Call = this.Ensage.AbilityFactory.GetAbility<axe_berserkers_call>();
            this.CullingBlade = this.Ensage.AbilityFactory.GetAbility<axe_culling_blade>();

            this.BladeMail = this.Ensage.AbilityFactory.GetItem<item_blade_mail>();
            this.Blink = this.Ensage.AbilityFactory.GetItem<item_blink>();
            this.LotusOrb = this.Ensage.AbilityFactory.GetItem<item_lotus_orb>();
            this.ForceStaff = this.Ensage.AbilityFactory.GetItem<item_force_staff>();
        }

        protected override async Task OnKillsteal(CancellationToken token)
        {
            if (!this.Owner.IsAlive || !this.CullingBlade.CanBeCasted)
            {
                await Task.Delay(125, token);
                return;
            }

            var forceStaffReady = this.ForceStaff != null && this.ForceStaff.CanBeCasted;
            var killstealTarget = EntityManager<Hero>.Entities.FirstOrDefault(
                x => x.IsAlive
                     && x.Team != this.Owner.Team
                     && !x.IsIllusion
                     && this.CullingBlade.CanHit(x)
                     && (forceStaffReady || !x.IsLinkensProtected())
                     && this.CullingBlade.GetDamage(x) > x.Health);

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