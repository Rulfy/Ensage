// <copyright file="Program.cs" company="Ensage">
//    Copyright (c) 2017 Ensage.
// </copyright>

namespace Snatcher
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel.Composition;
    using System.Linq;
    using System.Reflection;
    using System.Threading;
    using System.Threading.Tasks;

    using Ensage;
    using Ensage.SDK.Abilities;
    using Ensage.SDK.Abilities.Items;
    using Ensage.SDK.Extensions;
    using Ensage.SDK.Handlers;
    using Ensage.SDK.Helpers;
    using Ensage.SDK.Service;
    using Ensage.SDK.Service.Metadata;

    using log4net;

    using PlaySharp.Toolkit.Logging;

    [ExportPlugin("Snatcher")]
    public class Program : Plugin
    {
        private const float PickUpDistance = 150.0f;

        private static readonly HashSet<AbilityId> BlinkAbilityIds = new HashSet<AbilityId>()
                                                                         {
                                                                             AbilityId.antimage_blink,
                                                                             AbilityId.queenofpain_blink,
                                                                             AbilityId.sandking_burrowstrike,
                                                                             AbilityId.faceless_void_time_walk
                                                                         };

        private static readonly ILog Log = AssemblyLogs.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private readonly Lazy<AbilityFactory> abilityFactory;

        private readonly Unit owner;

        private RangedAbility blinkAbility;

        private SnatcherConfig config;

        private TaskHandler onUpdateHandler;

        [ImportingConstructor]
        public Program([Import] IServiceContext context, [Import] Lazy<AbilityFactory> abilityFactory)
        {
            this.owner = context.Owner;
            this.abilityFactory = abilityFactory;
        }

        protected override void OnActivate()
        {
            this.config = new SnatcherConfig();

            foreach (var blinkAbilityId in BlinkAbilityIds)
            {
                var ability = this.owner.GetAbilityById(blinkAbilityId);
                if (ability != null)
                {
                    try
                    {
                        this.blinkAbility = this.abilityFactory.Value.GetAbility<RangedAbility>(ability);
                        Log.Debug($"Snatcher: found ability for greed mode {this.blinkAbility}");
                    }
                    catch (AbilityNotImplementedException)
                    {
                        // not added to sdk yet
                    }
                }
            }

            this.onUpdateHandler = UpdateManager.Run(this.OnUpdate);
        }

        protected override void OnDeactivate()
        {
            this.onUpdateHandler?.Cancel();
            this.config?.Dispose();
        }

        private async Task ApproachEntity(RangedAbility blink, Entity entity, float checkRange, CancellationToken token = default(CancellationToken))
        {
            if (blink != null)
            {
                var distance = this.owner.Distance2D(entity);
                if (distance >= PickUpDistance)
                {
                    var dir = (entity.Position - this.owner.Position).Normalized();
                    dir *= distance >= checkRange ? checkRange : distance;
                    var blinkPos = this.owner.Position + dir;

                    blink.UseAbility(blinkPos);
                    await Task.Delay(blink.GetCastDelay(blinkPos), token);
                }
            }
        }

        private async Task OnUpdate(CancellationToken token)
        {
            if (!this.config.HoldHotkey.Value.Active && !this.config.ToggleHotkey.Value.Active)
            {
                await Task.Delay(this.config.ScanIntervall, token);
                return;
            }

            if (Game.IsPaused || !this.owner.IsAlive)
            {
                await Task.Delay(this.config.ScanIntervall, token);
                return;
            }

            float checkRange = 300;

            // if (this.config.GreedMode.Value.Dictionary.Any(x => x.Value))
            RangedAbility blink = null;
            if (this.config.GreedMode)
            {
                var blinkItem = this.abilityFactory.Value.GetItem<item_blink>();
                if ((blinkItem != null) && blinkItem.CanBeCasted)
                {
                    blink = blinkItem;
                    checkRange = blinkItem.CastRange;
                }
                else if ((this.blinkAbility != null) && this.blinkAbility.CanBeCasted)
                {
                    blink = this.blinkAbility;
                    checkRange = this.blinkAbility.CastRange;
                }
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
                    var query = EntityManager<PhysicalItem>.Entities.Where(x => x.IsVisible && (x.Distance2D(this.owner) < (checkRange + PickUpDistance)));
                    query = query.Where(
                        x => (grabAegis && (x.Item.Id == AbilityId.item_aegis))
                             || (grabCheese && (x.Item.Id == AbilityId.item_cheese))
                             || (grabRapier && (x.Item.Id == AbilityId.item_rapier))
                             || (grabGem && (x.Item.Id == AbilityId.item_gem)));

                    var physicalItem = query.FirstOrDefault();
                    if (physicalItem != null)
                    {
                        if (!hasFreeSlots)
                        {
                            // check if hero already has a cheese
                            if ((physicalItem.Item.Id != AbilityId.item_cheese) || (this.owner.GetItemById(AbilityId.item_cheese) != null))
                            {
                                // swap lowest cost item
                                var item = this.owner.Inventory.Items.Where(x => x.IsKillable && (x.Id != AbilityId.item_aegis)).OrderBy(x => x.Cost).FirstOrDefault();
                                if (item == null)
                                {
                                    await Task.Delay(this.config.ScanIntervall, token);
                                    return;
                                }

                                item.MoveItem(freeBackbackSlots.First());
                            }
                        }

                        // if (this.config.GreedMode.Value.IsEnabled(physicalItem.Item.Name))
                        if (blink != null)
                        {
                            await this.ApproachEntity(blink, physicalItem, checkRange, token);
                        }

                        this.owner.PickUpItem(physicalItem);
                        await Task.Delay(this.config.ScanIntervall, token);
                        return;
                    }
                }
            }

            if (this.config.SnatchOptions.Value.IsEnabled("rune_bounty"))
            {
                var runes = EntityManager<Rune>.Entities.Where(x => x.IsVisible && (x.Distance2D(this.owner) < (checkRange + PickUpDistance))).ToList();
                if (runes.Any())
                {
                    var rune = runes.First();

                    // if (this.config.GreedMode.Value.IsEnabled("rune_bounty"))
                    if (blink != null)
                    {
                        await this.ApproachEntity(blink, rune, checkRange, token);
                    }

                    this.owner.PickUpRune(rune);
                }
            }

            await Task.Delay(this.config.ScanIntervall, token);
        }
    }
}