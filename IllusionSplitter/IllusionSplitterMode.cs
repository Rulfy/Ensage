// <copyright file="IllusionSplitterMode.cs" company="Ensage">
//    Copyright (c) 2017 Ensage.
// </copyright>

namespace IllusionSplitter
{
    using System;
    using System.Collections.Specialized;
    using System.Linq;
    using System.Reflection;
    using System.Threading;
    using System.Threading.Tasks;
    using System.Windows.Input;

    using Ensage;
    using Ensage.Common.Enums;
    using Ensage.Common.Extensions;
    using Ensage.Common.Threading;
    using Ensage.Items;
    using Ensage.SDK.Extensions;
    using Ensage.SDK.Handlers;
    using Ensage.SDK.Helpers;
    using Ensage.SDK.Input;
    using Ensage.SDK.Inventory;
    using Ensage.SDK.Orbwalker;
    using Ensage.SDK.Orbwalker.Modes;

    using log4net;

    using PlaySharp.Toolkit.Logging;

    using SharpDX;

    using AbilityId = Ensage.AbilityId;
    using UnitExtensions = Ensage.SDK.Extensions.UnitExtensions;

    public class IllusionSplitterMode : KeyPressOrbwalkingModeAsync
    {
        private static readonly ILog Log = AssemblyLogs.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private readonly IllusionSplitterConfig config;

        private readonly TaskHandler illusionHandler;

        private readonly IInventoryManager inventoryMgr;

        private readonly Unit owner;

        private InventoryItem bottle;

        private Ability conjureImage;

        private Ability doppelWalk;

        private Vector3 heroTargetDirection;

        private InventoryItem manta;

        private Ability mirrorImage;

        public IllusionSplitterMode(IOrbwalker orbwalker, IInputManager input, Key key, IllusionSplitterConfig config, IInventoryManager inventoryMgr)
            : base(orbwalker, input, key)
        {
            this.config = config;
            this.inventoryMgr = inventoryMgr;
            this.illusionHandler = UpdateManager.Run(this.MoveIllusionsAsync, false);

            this.owner = orbwalker.Context.Owner;
        }

        public override async Task ExecuteAsync(CancellationToken token)
        {
            var casted = false;

            // checks for items
            if (this.manta != null && this.config.UseAbilities.Value.IsEnabled("item_manta") && this.manta.Item.CanBeCasted())
            {
                Log.Debug("Used manta");
                this.manta.Item.UseAbility();
                casted = true;

                await Await.Delay(250 + (int)Game.Ping, token);
            }

            if (!casted && this.bottle != null && this.config.UseAbilities.Value.IsEnabled("item_bottle"))
            {
                var bottleItem = this.bottle.Item as Bottle;
                if (bottleItem?.StoredRune == RuneType.Illusion)
                {
                    Log.Debug("Used bottle");
                    bottleItem.UseAbility();
                    casted = true;

                    await Await.Delay(125 + (int)Game.Ping, token);
                }
            }

            // check for spells
            if (!casted)
            {
                if (this.mirrorImage != null && this.config.UseAbilities.Value.IsEnabled("naga_siren_mirror_image") && this.mirrorImage.CanBeCasted())
                {
                    this.mirrorImage.UseAbility();
                    var delay = (int)((this.mirrorImage.GetCastPoint(0) + this.mirrorImage.AbilitySpecialData.First(x => x.Name == "invuln_duration").Value) * 1000.0f) + 250
                                + (int)Game.Ping;
                    Log.Debug($"using mirror image with delay {delay}");
                    await Await.Delay(delay, token);
                    return;
                }

                if (this.conjureImage != null && this.config.UseAbilities.Value.IsEnabled("terrorblade_conjure_image") && this.conjureImage.CanBeCasted())
                {
                    this.conjureImage.UseAbility();
                    var delay = (int)((this.conjureImage.GetCastPoint(0) * 1000.0f) + 250.0f) + (int)Game.Ping;
                    Log.Debug($"using conjure image with delay {delay}");
                    await Await.Delay(delay, token);
                    return;
                }

                if (this.doppelWalk != null && this.config.UseAbilities.Value.IsEnabled("phantom_lancer_doppelwalk") && this.doppelWalk.CanBeCasted())
                {
                    var pos = Game.MousePosition - this.owner.Position;
                    if (pos.Length() > this.doppelWalk.CastRange)
                    {
                        pos.Normalize();
                        pos *= this.doppelWalk.CastRange;
                    }

                    this.doppelWalk.UseAbility(this.owner.Position + pos);
                    var delay = ((int)(this.doppelWalk.GetCastPoint(0) + this.doppelWalk.AbilitySpecialData.First(x => x.Name == "delay").Value) * 1000) + 250 + (int)Game.Ping;
                    Log.Debug($"using doppel walk with delay {delay}");
                    await Await.Delay(delay, token);
                }
            }

            if (this.config.MoveHero)
            {
                // Log.Debug($"Move hero to position {Game.MousePosition}");
                this.owner.Move(Game.MousePosition);
                this.heroTargetDirection = Game.MousePosition - this.owner.Position;
            }
            else
            {
                this.heroTargetDirection = UnitExtensions.InFront(this.owner, 250) - this.owner.Position;

                // Log.Debug($"Hero target dir {heroTargetDirection}");
            }

            this.illusionHandler.RunAsync();
            await Await.Delay(125, token);
        }

        protected override void OnActivate()
        {
            base.OnActivate();

            this.conjureImage = UnitExtensions.GetAbilityById(this.owner, AbilityId.terrorblade_conjure_image);
            this.mirrorImage = UnitExtensions.GetAbilityById(this.owner, AbilityId.naga_siren_mirror_image);
            this.doppelWalk = UnitExtensions.GetAbilityById(this.owner, AbilityId.phantom_lancer_doppelwalk);

            this.inventoryMgr.CollectionChanged += this.InventoryChanged;
        }

        protected override void OnDeactivate()
        {
            this.inventoryMgr.CollectionChanged -= this.InventoryChanged;

            base.OnDeactivate();
        }

        private void InventoryChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.Action == NotifyCollectionChangedAction.Add)
            {
                foreach (var item in e.NewItems.OfType<InventoryItem>())
                {
                    if (item.Id == AbilityId.item_manta)
                    {
                        this.manta = item;
                    }
                    else if (item.Id == AbilityId.item_bottle)
                    {
                        if (item.Item is Bottle)
                        {
                            this.bottle = item;
                        }
                    }
                }
            }
            else if (e.Action == NotifyCollectionChangedAction.Remove)
            {
                foreach (var item in e.OldItems.OfType<InventoryItem>())
                {
                    if (item.Id == AbilityId.item_manta)
                    {
                        this.manta = null;
                    }
                    else if (item.Id == AbilityId.item_bottle)
                    {
                        this.bottle = null;
                    }
                }
            }
        }

        private async Task MoveIllusionsAsync(CancellationToken arg)
        {
            var illusions = EntityManager<Hero>.Entities.Where(x => x.IsIllusion && x.IsAlive && x.IsControllable && x.Distance2D(this.owner) < this.config.IlluRange.Value)
                                               .ToList();
            if (!illusions.Any())
            {
                return;
            }

            var middlePosition = illusions.Aggregate(this.owner.Position, (current, illusion) => current + illusion.Position);
            var unitCount = illusions.Count + 1;

            middlePosition /= unitCount;
            var illuAngle = 360.0f / unitCount;

            Random random = null;
            if (this.config.AngleRandomizer)
            {
                random = new Random();
            }

            // Log.Debug($"Moving illusions {illusions.Count}");
            var currentHeroDir = this.heroTargetDirection;
            foreach (var illusion in illusions)
            {
                if (random != null)
                {
                    var randomAngle = random.NextFloat(1, illuAngle / unitCount);
                    currentHeroDir = currentHeroDir.Rotated(MathUtil.DegreesToRadians(illuAngle + randomAngle));
                }
                else
                {
                    currentHeroDir = currentHeroDir.Rotated(MathUtil.DegreesToRadians(illuAngle));
                }

                var dir = currentHeroDir.Normalized();
                dir *= this.config.MinMoveRange;
                var movePos = middlePosition + dir;

                illusion.Move(movePos);
                await Await.Delay(125, arg);
            }

            await Await.Delay(250, arg);
        }
    }
}