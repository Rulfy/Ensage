// <copyright file="Program.cs" company="Ensage">
//    Copyright (c) 2017 Ensage.
// </copyright>

namespace Snatcher
{
    using System;
    using System.ComponentModel.Composition;
    using System.Linq;

    using Ensage;
    using Ensage.Common.Menu;
    using Ensage.SDK.Extensions;
    using Ensage.SDK.Helpers;
    using Ensage.SDK.Service;
    using Ensage.SDK.Service.Metadata;

    [ExportPlugin("Snatcher", StartupMode.Auto)]
    public class Program : Plugin
    {
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
            this.config.ScanIntervall.Item.ValueChanged += this.ItemValueChanged;
            UpdateManager.Subscribe(this.OnUpdate, this.config.ScanIntervall);
        }

        protected override void OnDeactivate()
        {
            UpdateManager.Unsubscribe(this.OnUpdate);
            this.config.ScanIntervall.Item.ValueChanged -= this.ItemValueChanged;
            this.config?.Dispose();
        }

        private void ItemValueChanged(object sender, OnValueChangeEventArgs e)
        {
            UpdateManager.Unsubscribe(this.OnUpdate);
            UpdateManager.Subscribe(this.OnUpdate, e.GetNewValue<Slider>().Value);
        }

        private void OnUpdate()
        {
            if (!this.config.HoldHotkey.Value.Active && !this.config.ToggleHotkey.Value.Active)
            {
                return;
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
                    var query = ObjectManager.GetEntities<PhysicalItem>().Where(x => x.IsVisible && x.Distance2D(this.owner) < 400);
                    query = query.Where(
                        x => (grabAegis && x.Item.Id == AbilityId.item_aegis) || (grabCheese && x.Item.Id == AbilityId.item_cheese)
                             || (grabRapier && x.Item.Id == AbilityId.item_rapier) || (grabGem && x.Item.Id == AbilityId.item_gem));

                    var physicalItem = query.FirstOrDefault();
                    if (physicalItem != null)
                    {
                        if (!hasFreeSlots)
                        {
                            // swap lowest cost item
                            var item = this.owner.Inventory.Items.Where(x => x.IsKillable && x.Id != AbilityId.item_aegis).OrderBy(x => x.Cost).FirstOrDefault();
                            if (item == null)
                            {
                                return;
                            }
                            item.MoveItem(freeBackbackSlots.First());
                        }
                        this.owner.PickUpItem(physicalItem);
                        return;
                    }
                }
            }

            if (this.config.SnatchOptions.Value.IsEnabled("rune_bounty"))
            {
                var runes = ObjectManager.GetEntities<Rune>().Where(x => x.IsVisible && x.Distance2D(this.owner) < 400).ToList();
                if (runes.Any())
                {
                    this.owner.PickUpRune(runes.First());
                    return;
                }
            }
        }
    }
}