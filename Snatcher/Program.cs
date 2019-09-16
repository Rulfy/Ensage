// <copyright file="Program.cs" company="Ensage">
//    Copyright (c) 2018 Ensage.
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
    using Ensage.SDK.Inventory.Metadata;
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

        private readonly IServiceContext context;

        private readonly Unit owner;

        private RangedAbility blinkAbility;

        private SnatcherConfig config;

        private TaskHandler onUpdateHandler;

        [ImportingConstructor]
        public Program([Import] IServiceContext context, [Import] Lazy<AbilityFactory> abilityFactory)
        {
            this.context = context;
            this.owner = context.Owner;
            this.abilityFactory = abilityFactory;
        }

        [ItemBinding]
        public item_blink Blink { get; set; }

        protected override void OnActivate()
        {
            this.context.Inventory.Attach(this);

            this.config = new SnatcherConfig(this.context.RenderManager);
            this.context.MenuManager.RegisterMenu(this.config);

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
            this.context.MenuManager.DeregisterMenu(this.config);
            this.onUpdateHandler?.Cancel();

            this.context.Inventory.Detach(this);
        }

        private async Task ApproachEntity(RangedAbility blink, Entity entity, CancellationToken token = default(CancellationToken))
        {
            if (blink != null)
            {
                var distance = this.owner.Distance2D(entity);
                if (distance >= PickUpDistance)
                {
                    var dir = (entity.Position - this.owner.Position).Normalized();
                    dir *= distance >= blink.CastRange ? blink.CastRange : distance;
                    var blinkPos = this.owner.Position + dir;

                    blink.UseAbility(blinkPos);
                    await Task.Delay(blink.GetCastDelay(blinkPos), token);
                }
            }
        }

        private async Task OnUpdate(CancellationToken token)
        {
            if (!this.config.IsActive && !this.config.IsActiveHold)
            {
                await Task.Delay(this.config.ScanIntervall.Value, token);
                return;
            }

            if (Game.IsPaused || !this.owner.IsAlive)
            {
                await Task.Delay(this.config.ScanIntervall.Value, token);
                return;
            }

            float checkRange = this.config.CheckRange.Value;

            RangedAbility blink = null;
            if (this.config.GreedMode && this.config.GreedOptions.PictureStates.Any(x => x.Value))
            {
                if (this.Blink != null && this.Blink.CanBeCasted)
                {
                    blink = this.Blink;
                }
                else if (this.blinkAbility != null && this.blinkAbility.CanBeCasted)
                {
                    blink = this.blinkAbility;
                }
            }

            var hasFreeSlots = this.owner.Inventory.FreeInventorySlots.Any();
            var freeBackbackSlots = this.owner.Inventory.FreeBackpackSlots.ToList();

            bool swapItem = this.config.SwapItem;
            if (hasFreeSlots || (swapItem && freeBackbackSlots.Any()))
            {
                var items = EntityManager<PhysicalItem>.Entities.Where(
                    x => x.IsVisible
                         && (x.Item.Id == AbilityId.item_aegis
                             || x.Item.Id == AbilityId.item_cheese
                             || x.Item.Id == AbilityId.item_rapier
                             || x.Item.Id == AbilityId.item_gem));

                foreach (var physicalItem in items)
                {
                    var name = physicalItem.Item.Name;
                    if (!this.config.SnatchOptions[name])
                    {
                        continue;
                    }

                    var range = blink != null && this.config.GreedOptions[name] ? blink.CastRange : this.config.CheckRange.Value;
                    if (physicalItem.Distance2D(this.owner) > (range + PickUpDistance))
                    {
                        continue;
                    }

                    if (!hasFreeSlots && !physicalItem.Item.IsStackable)
                    {
                        // swap lowest cost item
                        var item = this.owner.Inventory.Items.Where(x => x.IsKillable && x.Id != AbilityId.item_aegis && x.Id != AbilityId.item_blink)
                                       .OrderBy(x => x.Cost)
                                       .FirstOrDefault();
                        if (item == null)
                        {
                            await Task.Delay(this.config.ScanIntervall.Value, token);
                            return;
                        }

                        item.MoveItem(freeBackbackSlots.First());
                    }

                    if (blink != null && this.config.GreedOptions[physicalItem.Item.Name])
                    {
                        await this.ApproachEntity(blink, physicalItem, token);
                    }

                    this.owner.PickUpItem(physicalItem);
                    await Task.Delay(this.config.ScanIntervall.Value, token);
                    return;
                }
            }

            if (this.config.SnatchOptions["rune_doubledamage"])
            {
                var range = blink != null && this.config.GreedOptions["rune_doubledamage"] ? blink.CastRange : this.config.CheckRange.Value;
                var rune = EntityManager<Rune>.Entities.FirstOrDefault(x => x.IsVisible && x.Distance2D(this.owner) <= (range + PickUpDistance));
                if (rune != null)
                {
                    if (blink != null && this.config.GreedOptions["rune_doubledamage"])
                    {
                        await this.ApproachEntity(blink, rune, token);
                    }

                    this.owner.PickUpRune(rune);
                }
            }

            await Task.Delay(this.config.ScanIntervall.Value, token);
        }
    }
}