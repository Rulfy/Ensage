// <copyright file="Program.cs" company="Ensage">
//    Copyright (c) 2017 Ensage.
// </copyright>

namespace Snatcher
{
    using System;
    using System.ComponentModel.Composition;
    using System.Linq;
    using System.Threading.Tasks;

    using Ensage;
    using Ensage.SDK.Extensions;
    using Ensage.SDK.Helpers;
    using Ensage.SDK.Service;
    using Ensage.SDK.Service.Metadata;

    [ExportPlugin("Snatcher")]
    public class Program : Plugin
    {
        private const float PickUpDistance = 150.0f;

        private readonly Unit owner;

        private SnatcherConfig config;

        [ImportingConstructor]
        public Program([Import] IServiceContext context)
        {
            this.owner = context.Owner;
        }

        protected override void OnActivate()
        {
            this.config = new SnatcherConfig();
            UpdateManager.BeginInvoke(this.OnUpdate);
        }

        protected override void OnDeactivate()
        {
            this.config?.Dispose();
        }

        private async Task ApproachEntity(Ability blinkAbility, Entity entity, float checkRange)
        {
            if (blinkAbility != null)
            {
                var distance = this.owner.Distance2D(entity);
                if (distance >= PickUpDistance)
                {
                    var dir = (entity.Position - this.owner.Position).Normalized();
                    dir *= distance >= checkRange ? checkRange : distance;
                    blinkAbility.UseAbility(this.owner.Position + dir);
                    await Task.Delay(125);
                }
            }
        }

        private async void OnUpdate()
        {
            while (this.IsActive)
            {
                await Task.Delay(this.config.ScanIntervall);

                if (!this.config.HoldHotkey.Value.Active && !this.config.ToggleHotkey.Value.Active)
                {
                    continue;
                }

                if (Game.IsPaused || !this.owner.IsAlive)
                {
                    continue;
                }

                Ability blinkAbility = null;
                float checkRange = 300;

                // if (this.config.GreedMode.Value.Dictionary.Any(x => x.Value))
                if (this.config.GreedMode)
                {
                    var blinkItem = this.owner.GetItemById(AbilityId.item_blink);
                    if (blinkItem?.Cooldown <= 0)
                    {
                        checkRange = blinkItem.GetAbilitySpecialData("blink_range");
                        blinkAbility = blinkItem;
                    }

                    // else
                    // {
                    // var blink = this.owner.GetAbilityById(AbilityId.antimage_blink);
                    // blink = this.owner.GetAbilityById(AbilityId.queenofpain_blink);
                    // }
                }

                var hasFreeSlots = this.owner.Inventory.FreeInventorySlots.Any();
                var freeBackbackSlots = this.owner.Inventory.FreeBackpackSlots.ToList();

                bool swapItem = this.config.SwapItem;
                if (hasFreeSlots || (swapItem && freeBackbackSlots.Any()))
                {
                    var grabAegis = this.config.SnatchOptions.Value.IsEnabled("item_aegis");
                    var grabRapier = this.config.SnatchOptions.Value.IsEnabled("item_rapier");
                    var grabCheese = this.config.SnatchOptions.Value.IsEnabled("item_cheese");
                    var grabGem = this.config.SnatchOptions.Value.IsEnabled("item_gem");

                    if (grabAegis || grabRapier || grabCheese || grabGem)
                    {
                        var query = EntityManager<PhysicalItem>.Entities.Where(x => x.IsVisible && x.Distance2D(this.owner) < (checkRange + PickUpDistance));
                        query = query.Where(
                            x => (grabAegis && x.Item.Id == AbilityId.item_aegis) || (grabCheese && x.Item.Id == AbilityId.item_cheese)
                                 || (grabRapier && x.Item.Id == AbilityId.item_rapier) || (grabGem && x.Item.Id == AbilityId.item_gem));

                        var physicalItem = query.FirstOrDefault();
                        if (physicalItem != null)
                        {
                            if (!hasFreeSlots)
                            {
                                // check if hero already has a cheese
                                if (physicalItem.Item.Id != AbilityId.item_cheese || this.owner.GetItemById(AbilityId.item_cheese) != null)
                                {
                                    // swap lowest cost item
                                    var item = this.owner.Inventory.Items.Where(x => x.IsKillable && x.Id != AbilityId.item_aegis).OrderBy(x => x.Cost).FirstOrDefault();
                                    if (item == null)
                                    {
                                        continue;
                                    }

                                    item.MoveItem(freeBackbackSlots.First());
                                }
                            }

                            // if (this.config.GreedMode.Value.IsEnabled(physicalItem.Item.Name))
                            if (blinkAbility != null)
                            {
                                await this.ApproachEntity(blinkAbility, physicalItem, checkRange);
                            }
                            
                            this.owner.PickUpItem(physicalItem);
                            continue;
                        }
                    }
                }

                if (this.config.SnatchOptions.Value.IsEnabled("rune_bounty"))
                {
                    var runes = EntityManager<Rune>.Entities.Where(x => x.IsVisible && x.Distance2D(this.owner) < (checkRange + PickUpDistance)).ToList();
                    if (runes.Any())
                    {
                        var rune = runes.First();

                        // if (this.config.GreedMode.Value.IsEnabled("rune_bounty"))
                        if (blinkAbility != null)
                        {
                            await this.ApproachEntity(blinkAbility, rune, checkRange);
                        }

                        this.owner.PickUpRune(rune);
                        continue;
                    }
                }
            }
        }
    }
}