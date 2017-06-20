// <copyright file="PhantomAssassin.cs" company="Ensage">
//    Copyright (c) 2017 Ensage.
// </copyright>

namespace Vaper.Heroes
{
    using System;
    using System.Collections.Specialized;
    using System.ComponentModel;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;

    using Ensage;
    using Ensage.Common.Extensions;
    using Ensage.SDK.Abilities.Items;
    using Ensage.SDK.Abilities.npc_dota_hero_phantom_assassin;
    using Ensage.SDK.Helpers;
    using Ensage.SDK.Inventory;
    using Ensage.SDK.Menu;

    using SharpDX;

    using Vaper.OrbwalkingModes;

    using Color = System.Drawing.Color;

    [ExportHero(HeroId.npc_dota_hero_phantom_assassin)]
    public class PhantomAssassin : BaseHero
    {
        public const float CritPrd = 0.03221f; // = 15%

        public item_abyssal_blade AbyssalBlade { get; private set; }

        public phantom_assassin_blur Blur { get; private set; }

        public MenuItem<bool> BlurIndicator { get; private set; }

        public phantom_assassin_coup_de_grace Crit { get; private set; }

        public MenuItem<bool> CritIndicator { get; private set; }

        public float CurrentCritChance { get; private set; }

        public phantom_assassin_stifling_dagger Dagger { get; private set; }

        public phantom_assassin_phantom_strike PhantomStrike { get; private set; }

        protected override VaperOrbwalkingMode GetOrbwalkingMode()
        {
            return new PhantomAssassinOrbwalker(this);
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
                    }
                }
            }
        }

        protected override void OnActivate()
        {
            base.OnActivate();

            this.Dagger = this.Ensage.AbilityFactory.GetAbility<phantom_assassin_stifling_dagger>();
            this.PhantomStrike = this.Ensage.AbilityFactory.GetAbility<phantom_assassin_phantom_strike>();
            this.Blur = this.Ensage.AbilityFactory.GetAbility<phantom_assassin_blur>();
            this.Crit = this.Ensage.AbilityFactory.GetAbility<phantom_assassin_coup_de_grace>();

            this.AbyssalBlade = this.Ensage.AbilityFactory.GetItem<item_abyssal_blade>();

            var factory = this.Menu.Hero.Factory;
            this.CritIndicator = factory.Item("Show Crit Indicator", true);
            this.BlurIndicator = factory.Item("Show Blur Indicator", true);
            this.BlurIndicator.PropertyChanged += this.BlurIndicatorPropertyChanged;

            this.Ensage.Renderer.Draw += this.OnDraw;
            Entity.OnInt32PropertyChange += this.OnNetworkActivity;
        }

        protected override void OnDeactivate()
        {
            Entity.OnInt32PropertyChange -= this.OnNetworkActivity;
            this.Ensage.Renderer.Draw -= this.OnDraw;

            base.OnDeactivate();
        }

        protected override async Task OnKillsteal(CancellationToken token)
        {
            if (!this.Owner.IsAlive || !this.Dagger.CanBeCasted)
            {
                await Task.Delay(125, token);
                return;
            }

            var killstealTarget = EntityManager<Hero>.Entities.FirstOrDefault(
                x => x.IsAlive
                     && x.Team != this.Owner.Team
                     && !x.IsIllusion
                     && this.Dagger.CanHit(x)
                     && !x.IsLinkensProtected()
                     && this.Dagger.GetDamage(x) > x.Health);

            if (killstealTarget != null)
            {
                if (this.Dagger.UseAbility(killstealTarget))
                {
                    var castDelay = this.Dagger.GetCastDelay(killstealTarget);
                    await this.AwaitKillstealDelay(castDelay, token);
                }
            }

            await Task.Delay(125, token);
        }

        protected override void OnUpdateParticles()
        {
            if (this.BlurIndicator)
            {
                if (!this.Owner.HasModifier(this.Blur.ModifierName))
                {
                    this.Ensage.Particle.AddOrUpdate(this.Owner, "vaper_blurIndicator", @"particles/dire_fx/tower_bad_lamp_f.vpcf", ParticleAttachment.AbsOriginFollow);
                }
                else
                {
                    this.Ensage.Particle.Remove("vaper_blurIndicator");
                }
            }

            base.OnUpdateParticles();
        }

        private void BlurIndicatorPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (!this.BlurIndicator)
            {
                this.Ensage.Particle.Remove("vaper_blurIndicator");
            }
        }

        private void OnDraw(object sender, EventArgs e)
        {
            if (!this.CritIndicator || this.Crit.Ability.Level <= 0)
            {
                return;
            }

            Vector2 screenPos;
            var barPos = this.Owner.Position + new Vector3(0, 0, this.Owner.HealthBarOffset);
            if (Drawing.WorldToScreen(barPos, out screenPos))
            {
                this.Ensage.Renderer.DrawRectangle(new RectangleF(screenPos.X - 40, screenPos.Y - 15, 80, 7), Color.Red, 1.0f);

                var critWidth = 80.0f * this.CurrentCritChance;
                this.Ensage.Renderer.DrawLine(new Vector2(screenPos.X - 40, screenPos.Y - 11), new Vector2((screenPos.X - 40) + critWidth, screenPos.Y - 11), Color.Red, 7);
            }
        }

        private void OnNetworkActivity(Entity sender, Int32PropertyChangeEventArgs args)
        {
            if (sender != this.Owner)
            {
                return;
            }

            if (args.PropertyName != "m_NetworkActivity")
            {
                return;
            }

            var newNetworkActivity = (NetworkActivity)args.NewValue;

            switch (newNetworkActivity)
            {
                case NetworkActivity.Attack:
                case NetworkActivity.Attack2:
                    // TODO: check for allies, buildings and wards target
                    this.CurrentCritChance = Math.Min(1.0f, this.CurrentCritChance + CritPrd);
                    break;

                case NetworkActivity.Crit:
                    // Pseudo-random_distribution
                    this.CurrentCritChance = CritPrd;
                    break;
            }
        }
    }
}