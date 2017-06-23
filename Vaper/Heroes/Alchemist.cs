// <copyright file="Alchemist.cs" company="Ensage">
//    Copyright (c) 2017 Ensage.
// </copyright>

namespace Vaper.Heroes
{
    using System;
    using System.Collections.Specialized;
    using System.ComponentModel;
    using System.Linq;
    using System.Reflection;
    using System.Threading;
    using System.Threading.Tasks;

    using Ensage;
    using Ensage.Common;
    using Ensage.SDK.Abilities.Items;
    using Ensage.SDK.Abilities.npc_dota_hero_alchemist;
    using Ensage.SDK.Extensions;
    using Ensage.SDK.Handlers;
    using Ensage.SDK.Helpers;
    using Ensage.SDK.Inventory;
    using Ensage.SDK.Menu;

    using log4net;

    using PlaySharp.Toolkit.Logging;

    using SharpDX;

    using Vaper.OrbwalkingModes;

    using Color = System.Drawing.Color;

    [ExportHero(HeroId.npc_dota_hero_alchemist)]
    public class Alchemist : BaseHero
    {
        private static readonly ILog Log = AssemblyLogs.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        public item_abyssal_blade AbyssalBlade { get; private set; }

        public alchemist_acid_spray Acid { get; private set; }

        public TaskHandler AlchemistController { get; set; }

        public item_armlet Armlet { get; private set; }

        public item_silver_edge BreakBlade { get; private set; }

        public alchemist_unstable_concoction Concoction { get; private set; }

        public float ConcoctionChannelingDuration
        {
            get
            {
                if (this.ConcoctionStartTime > 0 && this.Owner.HasModifier(this.Concoction.ModifierName))
                {
                    return Game.GameTime - this.ConcoctionStartTime;
                }

                return 0;
            }
        }

        public float ConcoctionStartTime { get; private set; }

        public alchemist_goblins_greed Greed { get; private set; }

        public MenuItem<bool> LastHitIndicator { get; private set; }

        public float LastHitTime { get; private set; }

        public item_manta Manta { get; private set; }

        public item_medallion_of_courage Medallion { get; private set; }

        public item_mjollnir Mjollnir { get; private set; }

        public int MyLastHits { get; private set; }

        public Player Player { get; private set; }

        public alchemist_chemical_rage Rage { get; private set; }

        public item_invis_sword ShadowBlade { get; private set; }

        public item_solar_crest SolarCrest { get; private set; }

        protected override VaperOrbwalkingMode GetOrbwalkingMode()
        {
            return new AlchemistComboOrbwalker(this);
        }

        protected override void InventoryChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.Action == NotifyCollectionChangedAction.Add)
            {
                foreach (var newItem in e.NewItems.OfType<InventoryItem>())
                {
                    switch (newItem.Id)
                    {
                        case AbilityId.item_abyssal_blade:
                            this.AbyssalBlade = this.Ensage.AbilityFactory.GetAbility<item_abyssal_blade>(newItem.Item);
                            break;
                        case AbilityId.item_manta:
                            this.Manta = this.Ensage.AbilityFactory.GetAbility<item_manta>(newItem.Item);
                            break;
                        case AbilityId.item_mjollnir:
                            this.Mjollnir = this.Ensage.AbilityFactory.GetAbility<item_mjollnir>(newItem.Item);
                            break;
                        case AbilityId.item_armlet:
                            this.Armlet = this.Ensage.AbilityFactory.GetAbility<item_armlet>(newItem.Item);
                            break;
                        case AbilityId.item_medallion_of_courage:
                            this.Medallion = this.Ensage.AbilityFactory.GetAbility<item_medallion_of_courage>(newItem.Item);
                            break;
                        case AbilityId.item_solar_crest:
                            this.SolarCrest = this.Ensage.AbilityFactory.GetAbility<item_solar_crest>(newItem.Item);
                            break;
                        case AbilityId.item_invis_sword:
                            this.ShadowBlade = this.Ensage.AbilityFactory.GetAbility<item_invis_sword>(newItem.Item);
                            break;
                        case AbilityId.item_silver_edge:
                            this.BreakBlade = this.Ensage.AbilityFactory.GetAbility<item_silver_edge>(newItem.Item);
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
                        case AbilityId.item_abyssal_blade:
                            this.AbyssalBlade = null;
                            break;
                        case AbilityId.item_manta:
                            this.Manta = null;
                            break;
                        case AbilityId.item_mjollnir:
                            this.Mjollnir = null;
                            break;
                        case AbilityId.item_armlet:
                            this.Armlet = null;
                            break;
                        case AbilityId.item_medallion_of_courage:
                            this.Medallion = null;
                            break;
                        case AbilityId.item_solar_crest:
                            this.SolarCrest = null;
                            break;
                        case AbilityId.item_invis_sword:
                            this.ShadowBlade = null;
                            break;
                        case AbilityId.item_silver_edge:
                            this.BreakBlade = null;
                            break;
                    }
                }
            }
        }

        protected override void OnActivate()
        {
            base.OnActivate();

            this.Player = ObjectManager.LocalPlayer;

            this.Acid = this.Ensage.AbilityFactory.GetAbility<alchemist_acid_spray>();
            this.Concoction = this.Ensage.AbilityFactory.GetAbility<alchemist_unstable_concoction>();
            this.Greed = this.Ensage.AbilityFactory.GetAbility<alchemist_goblins_greed>();
            this.Rage = this.Ensage.AbilityFactory.GetAbility<alchemist_chemical_rage>();

            this.Armlet = this.Ensage.AbilityFactory.GetItem<item_armlet>();
            this.Manta = this.Ensage.AbilityFactory.GetItem<item_manta>();
            this.AbyssalBlade = this.Ensage.AbilityFactory.GetItem<item_abyssal_blade>();
            this.Mjollnir = this.Ensage.AbilityFactory.GetItem<item_mjollnir>();
            this.Medallion = this.Ensage.AbilityFactory.GetItem<item_medallion_of_courage>();
            this.SolarCrest = this.Ensage.AbilityFactory.GetItem<item_solar_crest>();
            this.ShadowBlade = this.Ensage.AbilityFactory.GetItem<item_invis_sword>();
            this.BreakBlade = this.Ensage.AbilityFactory.GetItem<item_silver_edge>();

            var factory = this.Menu.Hero.Factory;
            this.LastHitIndicator = factory.Item("Show Lasthit Indicator", true);
            this.LastHitIndicator.PropertyChanged += this.LastHitIndicatorPropertyChanged;

            Unit.OnModifierAdded += this.OnModifierAdded;
            Entity.OnInt32PropertyChange += this.OnLastHit;
            this.AlchemistController = UpdateManager.Run(this.OnUpdate);

            if (this.LastHitIndicator)
            {
                this.Ensage.Renderer.Draw += this.OnDraw;
            }
        }

        protected override void OnDeactivate()
        {
            this.Ensage.Renderer.Draw -= this.OnDraw;
            this.AlchemistController.Cancel();
            Entity.OnInt32PropertyChange -= this.OnLastHit;
            Unit.OnModifierAdded -= this.OnModifierAdded;

            this.LastHitIndicator.PropertyChanged -= this.LastHitIndicatorPropertyChanged;

            base.OnDeactivate();
        }

        private void LastHitIndicatorPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (this.LastHitIndicator)
            {
                this.Ensage.Renderer.Draw += this.OnDraw;
            }
            else
            {
                this.Ensage.Renderer.Draw -= this.OnDraw;
            }
        }

        private void OnDraw(object sender, EventArgs e)
        {
            if (this.Greed.Ability.Level <= 0)
            {
                return;
            }

            Vector2 screenPos;
            var barPos = this.Owner.Position + new Vector3(0, 0, this.Owner.HealthBarOffset);
            if (Drawing.WorldToScreen(barPos, out screenPos))
            {
                this.Ensage.Renderer.DrawRectangle(new RectangleF(screenPos.X - 40, screenPos.Y - 12, 80, 7), Color.Gold);

                var percentage = Math.Max(0.0f, Math.Min(1.0f, (this.Greed.Duration - (Game.GameTime - this.LastHitTime)) / this.Greed.Duration));

                // Log.Debug($"({this.Greed.Duration} - {Game.GameTime - this.LastHitTime})");
                var durationWidth = 80.0f * percentage;
                this.Ensage.Renderer.DrawLine(new Vector2(screenPos.X - 40, screenPos.Y - 8), new Vector2((screenPos.X - 40) + durationWidth, screenPos.Y - 8), Color.Gold, 7);
            }
        }

        private void OnLastHit(Entity sender, Int32PropertyChangeEventArgs args)
        {
            if (args.PropertyName == "m_iLastHitCount" && this.Player.LastHitCount == args.OldValue)
            {
                DelayAction.Add(
                    50,
                    () =>
                        {
                            if (this.Player.LastHitCount == args.NewValue)
                            {
                                this.LastHitTime = Game.GameTime;
                            }
                        });
            }
            else if (args.PropertyName == "m_iKills" && this.Player.Kills == args.OldValue)
            {
                DelayAction.Add(
                    50,
                    () =>
                        {
                            if (this.Player.Kills == args.NewValue)
                            {
                                this.LastHitTime = Game.GameTime;
                            }
                        });
            }
        }

        private void OnModifierAdded(Unit sender, ModifierChangedEventArgs args)
        {
            if (sender == this.Owner && args.Modifier.Name == this.Concoction.ModifierName)
            {
                this.ConcoctionStartTime = Game.GameTime;
            }
        }

        private async Task OnUpdate(CancellationToken token)
        {
            var gameTime = Game.GameTime;
            if (this.ConcoctionStartTime > 0
                && !((AlchemistComboOrbwalker)this.OrbwalkingMode).HasValidThrowTarget
                && (gameTime - this.ConcoctionStartTime) > (this.Concoction.Duration * 0.85f)
                && this.Owner.HasModifier(this.Concoction.ModifierName))
            {
                var elapsedTime = gameTime - this.ConcoctionStartTime;

                var enemyHeroes = EntityManager<Hero>.Entities.Where(
                    x => x.IsVisible
                         && x.IsAlive
                         && x.Team != this.Owner.Team
                         && !x.IsReflectingAbilities()
                         && x.Distance2D(this.Owner) <= this.Concoction.CastRange);

                var throwTarget = enemyHeroes.OrderBy(x => x.IsLinkensProtected())
                                             .ThenByDescending(x => x.IsIllusion)
                                             .ThenByDescending(x => this.Concoction.GetDamage(elapsedTime, x))
                                             .FirstOrDefault();
                if (throwTarget != null)
                {
                    // wait maximum possible time
                    var waitTime = (int)(((this.Concoction.ExplosionDuration - elapsedTime) * 1000.0f) - Game.Ping) - (this.Concoction.ThrowAbility.GetCastDelay(throwTarget) * 2);

                    // Log.Debug($"wait {waitTime} = {this.Concoction.ExplosionDuration - elapsedTime} - {this.Concoction.ThrowAbility.GetCastDelay(throwTarget)}");
                    await Task.Delay(waitTime, token);

                    if (this.Concoction.UseAbility(throwTarget))
                    {
                        Log.Debug($"throw on enemy {throwTarget.Name}");
                        this.ConcoctionStartTime = 0;
                        await Task.Delay(this.Concoction.ThrowAbility.GetCastDelay(throwTarget), token);
                        return;
                    }
                }

                if (this.Manta != null && this.Manta.CanBeCasted)
                {
                    // manta modifier duration = 0.1
                    var waitTime = (int)(((this.Concoction.ExplosionDuration - elapsedTime) * 1000.0f) - 50 - Game.Ping);
                    await Task.Delay(waitTime, token);

                    if (this.Manta.UseAbility())
                    {
                        Log.Debug("manta dodge");
                        this.ConcoctionStartTime = 0;
                        await Task.Delay(this.Manta.GetCastDelay(), token);
                        return;
                    }
                }

                if (this.Rage != null && this.Rage.CanBeCasted)
                {
                    if (this.Rage.UseAbility())
                    {
                        Log.Debug("use ult");
                        this.ConcoctionStartTime = 0;
                        await Task.Delay(this.Rage.GetCastDelay(), token);
                        return;
                    }
                }
            }

            var isInvisible = this.Owner.IsInvisible() || this.Owner.InvisiblityLevel > 0;
            if (this.Armlet != null && !isInvisible)
            {
                this.Armlet.Enabled = EntityManager<Hero>.Entities.Any(x => x.IsVisible && x.IsAlive && !x.IsIllusion && x.Team != this.Owner.Team && x.Distance2D(this.Owner) < 1000);
                await Task.Delay(this.Armlet.GetCastDelay(), token);
            }

            await Task.Delay(125, token);
        }
    }
}