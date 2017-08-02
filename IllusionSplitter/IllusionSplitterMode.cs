// <copyright file="IllusionSplitterMode.cs" company="Ensage">
//    Copyright (c) 2017 Ensage.
// </copyright>

namespace IllusionSplitter
{
    using System;
    using System.Linq;
    using System.Reflection;
    using System.Threading;
    using System.Threading.Tasks;
    using System.Windows.Input;

    using Ensage;
    using Ensage.Common.Extensions;
    using Ensage.Items;
    using Ensage.SDK.Abilities.Items;
    using Ensage.SDK.Extensions;
    using Ensage.SDK.Handlers;
    using Ensage.SDK.Helpers;
    using Ensage.SDK.Inventory.Metadata;
    using Ensage.SDK.Orbwalker.Modes;
    using Ensage.SDK.Service;

    using log4net;

    using PlaySharp.Toolkit.Helper.Annotations;
    using PlaySharp.Toolkit.Logging;

    using SharpDX;

    using UnitExtensions = Ensage.SDK.Extensions.UnitExtensions;

    [PublicAPI]
    public class IllusionSplitterMode : KeyPressOrbwalkingModeAsync
    {
        private static readonly ILog Log = AssemblyLogs.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private readonly IllusionSplitterConfig config;

        private readonly IServiceContext context;

        private readonly TaskHandler illusionHandler;

        private Ability conjureImage;

        private Ability doppelWalk;

        private Vector3 heroTargetDirection;

        private Ability mirrorImage;

        public IllusionSplitterMode(IServiceContext context, Key key, IllusionSplitterConfig config)
            : base(context, key)
        {
            this.config = config;
            this.illusionHandler = UpdateManager.Run(this.MoveIllusionsAsync, false);
            this.context = context;
        }

        [ItemBinding]
        public item_bottle Bottle { get; private set; }

        [ItemBinding]
        public item_manta Manta { get; private set; }

        public override async Task ExecuteAsync(CancellationToken token)
        {
            var casted = false;

            // checks for items
            if ((this.Manta != null) && this.config.UseAbilities.Value.IsEnabled("item_manta") && this.Manta.Item.CanBeCasted())
            {
                Log.Debug("Used manta");
                this.Manta.Item.UseAbility();
                casted = true;

                await Task.Delay(250 + (int)Game.Ping, token);
            }

            if (!casted && (this.Bottle != null) && this.config.UseAbilities.Value.IsEnabled("item_bottle"))
            {
                var bottleItem = this.Bottle.Item as Bottle;
                if (bottleItem?.StoredRune == RuneType.Illusion)
                {
                    Log.Debug("Used bottle");
                    bottleItem.UseAbility();
                    casted = true;

                    await Task.Delay(125 + (int)Game.Ping, token);
                }
            }

            // check for spells
            if (!casted)
            {
                if ((this.mirrorImage != null) && this.config.UseAbilities.Value.IsEnabled("naga_siren_mirror_image") && this.mirrorImage.CanBeCasted())
                {
                    this.mirrorImage.UseAbility();
                    var delay = (int)((this.mirrorImage.GetCastPoint(0) + this.mirrorImage.AbilitySpecialData.First(x => x.Name == "invuln_duration").Value) * 1000.0f)
                                + 250
                                + (int)Game.Ping;
                    Log.Debug($"using mirror image with delay {delay}");
                    await Task.Delay(delay, token);
                    return;
                }

                if ((this.conjureImage != null) && this.config.UseAbilities.Value.IsEnabled("terrorblade_conjure_image") && this.conjureImage.CanBeCasted())
                {
                    this.conjureImage.UseAbility();
                    var delay = (int)((this.conjureImage.GetCastPoint(0) * 1000.0f) + 250.0f) + (int)Game.Ping;
                    Log.Debug($"using conjure image with delay {delay}");
                    await Task.Delay(delay, token);
                    return;
                }

                if ((this.doppelWalk != null) && this.config.UseAbilities.Value.IsEnabled("phantom_lancer_doppelwalk") && this.doppelWalk.CanBeCasted())
                {
                    var pos = Game.MousePosition - this.context.Owner.Position;
                    if (pos.Length() > this.doppelWalk.CastRange)
                    {
                        pos.Normalize();
                        pos *= this.doppelWalk.CastRange;
                    }

                    this.doppelWalk.UseAbility(this.context.Owner.Position + pos);
                    var delay = ((int)(this.doppelWalk.GetCastPoint(0) + this.doppelWalk.AbilitySpecialData.First(x => x.Name == "delay").Value) * 1000) + 250 + (int)Game.Ping;
                    Log.Debug($"using doppel walk with delay {delay}");
                    await Task.Delay(delay, token);
                }
            }

            if (this.config.MoveHero)
            {
                // Log.Debug($"Move hero to position {Game.MousePosition}");
                this.context.Owner.Move(Game.MousePosition);
                this.heroTargetDirection = Game.MousePosition - this.context.Owner.Position;
            }
            else
            {
                this.heroTargetDirection = UnitExtensions.InFront(this.context.Owner, 250) - this.context.Owner.Position;

                // Log.Debug($"Hero target dir {heroTargetDirection}");
            }

            this.illusionHandler.RunAsync();
            await Task.Delay(125, token);
        }

        protected override void OnActivate()
        {
            base.OnActivate();

            this.context.Inventory.Attach(this);

            this.conjureImage = UnitExtensions.GetAbilityById(this.context.Owner, AbilityId.terrorblade_conjure_image);
            this.mirrorImage = UnitExtensions.GetAbilityById(this.context.Owner, AbilityId.naga_siren_mirror_image);
            this.doppelWalk = UnitExtensions.GetAbilityById(this.context.Owner, AbilityId.phantom_lancer_doppelwalk);
        }

        protected override void OnDeactivate()
        {
            this.context.Inventory.Detach(this);

            base.OnDeactivate();
        }

        private async Task MoveIllusionsAsync(CancellationToken arg)
        {
            var illusions = EntityManager<Hero>
                .Entities.Where(x => x.IsIllusion && x.IsAlive && x.IsControllable && (x.Distance2D(this.context.Owner) < this.config.IlluRange.Value))
                .ToList();
            if (!illusions.Any())
            {
                return;
            }

            var middlePosition = illusions.Aggregate(this.context.Owner.Position, (current, illusion) => current + illusion.Position);
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
                await Task.Delay(125, arg);
            }

            await Task.Delay(250, arg);
        }
    }
}