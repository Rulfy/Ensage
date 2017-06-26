// <copyright file="Alchemist.cs" company="Ensage">
//    Copyright (c) 2017 Ensage.
// </copyright>

namespace Vaper.Heroes
{
    using System;
    using System.ComponentModel;
    using System.Linq;
    using System.Reflection;
    using System.Threading;
    using System.Threading.Tasks;

    using Ensage;
    using Ensage.SDK.Abilities.Items;
    using Ensage.SDK.Abilities.npc_dota_hero_alchemist;
    using Ensage.SDK.Extensions;
    using Ensage.SDK.Handlers;
    using Ensage.SDK.Helpers;
    using Ensage.SDK.Inventory.Metadata;
    using Ensage.SDK.Menu;

    using log4net;

    using PlaySharp.Toolkit.Helper.Annotations;
    using PlaySharp.Toolkit.Logging;

    using SharpDX;

    using Vaper.OrbwalkingModes;

    using Color = System.Drawing.Color;

    [ExportHero(HeroId.npc_dota_hero_alchemist)]
    public class Alchemist : BaseHero
    {
        private static readonly ILog Log = AssemblyLogs.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        [PublicAPI]
        [ItemBinding]
        public item_abyssal_blade AbyssalBlade { get; private set; }

        public alchemist_acid_spray Acid { get; protected set; }

        public TaskHandler AlchemistController { get; set; }

        [PublicAPI]
        [ItemBinding]
        public item_armlet Armlet { get; private set; }

        [PublicAPI]
        [ItemBinding]
        public item_silver_edge BreakBlade { get; private set; }

        public alchemist_unstable_concoction Concoction { get; private set; }

        public float ConcoctionChannelingDuration
        {
            get
            {
                if ((this.ConcoctionStartTime > 0) && this.Owner.HasModifier(this.Concoction.ModifierName))
                {
                    return Game.GameTime - this.ConcoctionStartTime;
                }

                return 0;
            }
        }

        public float ConcoctionStartTime { get; private set; }

        public alchemist_goblins_greed Greed { get; private set; }

        public bool HasUserEnabledArmlet { get; private set; }

        public MenuItem<bool> LastHitIndicator { get; private set; }

        public float LastHitTime { get; private set; }

        [PublicAPI]
        [ItemBinding]
        public item_manta Manta { get; private set; }

        [PublicAPI]
        [ItemBinding]
        public item_medallion_of_courage Medallion { get; private set; }

        [PublicAPI]
        [ItemBinding]
        public item_mjollnir Mjollnir { get; private set; }

        public Player Player { get; private set; }

        public alchemist_chemical_rage Rage { get; private set; }

        [PublicAPI]
        [ItemBinding]
        public item_invis_sword ShadowBlade { get; private set; }

        [PublicAPI]
        [ItemBinding]
        public item_solar_crest SolarCrest { get; private set; }

        protected override VaperOrbwalkingMode GetOrbwalkingMode()
        {
            return new AlchemistComboOrbwalker(this);
        }

        protected override void OnActivate()
        {
            base.OnActivate();
            this.Ensage.Inventory.Attach(this);

            this.Player = ObjectManager.LocalPlayer;

            this.Acid = this.Ensage.AbilityFactory.GetAbility<alchemist_acid_spray>();
            this.Concoction = this.Ensage.AbilityFactory.GetAbility<alchemist_unstable_concoction>();
            this.Greed = this.Ensage.AbilityFactory.GetAbility<alchemist_goblins_greed>();
            this.Rage = this.Ensage.AbilityFactory.GetAbility<alchemist_chemical_rage>();

            var factory = this.Menu.Hero.Factory;
            this.LastHitIndicator = factory.Item("Show Lasthit Indicator", true);
            this.LastHitIndicator.PropertyChanged += this.LastHitIndicatorPropertyChanged;

            Unit.OnModifierAdded += this.OnConcoction;
            Entity.OnParticleEffectAdded += this.OnLastHit;
            this.AlchemistController = UpdateManager.Run(this.OnUpdate);
            Player.OnExecuteOrder += this.Player_OnExecuteOrder;

            if (this.LastHitIndicator)
            {
                this.Ensage.Renderer.Draw += this.OnDraw;
            }
        }

        protected override void OnDeactivate()
        {
            Player.OnExecuteOrder -= this.Player_OnExecuteOrder;
            this.Ensage.Renderer.Draw -= this.OnDraw;
            this.AlchemistController.Cancel();
            Entity.OnParticleEffectAdded -= this.OnLastHit;
            Unit.OnModifierAdded += this.OnConcoction;

            this.LastHitIndicator.PropertyChanged -= this.LastHitIndicatorPropertyChanged;

            this.Ensage.Inventory.Detach(this);
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

        private void OnConcoction(Unit sender, ModifierChangedEventArgs args)
        {
            if ((sender == this.Owner) && (args.Modifier.Name == this.Concoction.ModifierName))
            {
                this.ConcoctionStartTime = Game.GameTime;
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

        private void OnLastHit(Entity sender, ParticleEffectAddedEventArgs args)
        {
            if ((sender == this.Player) && (args.Name == "particles/msg_fx/msg_gold.vpcf"))
            {
                this.LastHitTime = Game.GameTime;
            }
        }

        private async Task OnUpdate(CancellationToken token)
        {
            var gameTime = Game.GameTime;
            if ((this.ConcoctionStartTime > 0)
                && !((AlchemistComboOrbwalker)this.OrbwalkingMode).HasValidThrowTarget
                && ((gameTime - this.ConcoctionStartTime) > (this.Concoction.Duration * 0.85f))
                && this.Owner.HasModifier(this.Concoction.ModifierName))
            {
                var elapsedTime = gameTime - this.ConcoctionStartTime;

                var enemyHeroes = EntityManager<Hero>.Entities.Where(
                    x => x.IsVisible
                         && x.IsAlive
                         && (x.Team != this.Owner.Team)
                         && !x.IsReflectingAbilities()
                         && (x.Distance2D(this.Owner) <= this.Concoction.CastRange));

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

                if ((this.Manta != null) && this.Manta.CanBeCasted)
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

                if ((this.Rage != null) && this.Rage.CanBeCasted)
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

            if (!this.HasUserEnabledArmlet)
            {
                var isInvisible = this.Owner.IsInvisible() || (this.Owner.InvisiblityLevel > 0);
                if ((this.Armlet != null) && !isInvisible)
                {
                    var enemiesNear = EntityManager<Hero>.Entities.Any(
                        x => x.IsVisible && x.IsAlive && !x.IsIllusion && (x.Team != this.Owner.Team) && (x.Distance2D(this.Owner) < 1000));
                    if (enemiesNear && (this.Owner.HasModifier(this.Rage.ModifierName) || (this.Owner.HealthPercent() <= 0.75)))
                    {
                        this.Armlet.Enabled = true;
                    }
                    else
                    {
                        this.Armlet.Enabled = false;
                    }

                    await Task.Delay(this.Armlet.GetCastDelay(), token);
                }
            }

            await Task.Delay(125, token);
        }

        private void Player_OnExecuteOrder(Player sender, ExecuteOrderEventArgs args)
        {
            if ((this.Armlet != null) && args.IsPlayerInput && (args.OrderId == OrderId.ToggleAbility) && (args.Ability == this.Armlet.Ability))
            {
                Log.Debug($"user toggled armlet");
                this.HasUserEnabledArmlet = !this.Armlet.Enabled;
            }
        }
    }
}