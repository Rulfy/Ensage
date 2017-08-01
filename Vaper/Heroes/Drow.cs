// <copyright file="Drow.cs" company="Ensage">
//    Copyright (c) 2017 Ensage.
// </copyright>

namespace Vaper.Heroes
{
    using System.ComponentModel;
    using System.Linq;

    using Ensage;
    using Ensage.SDK.Abilities.Items;
    using Ensage.SDK.Abilities.npc_dota_hero_drow_ranger;
    using Ensage.SDK.Extensions;
    using Ensage.SDK.Helpers;
    using Ensage.SDK.Inventory.Metadata;
    using Ensage.SDK.Menu;

    using PlaySharp.Toolkit.Helper.Annotations;

    using SharpDX;

    using Vaper.OrbwalkingModes.Combo;
    using Vaper.OrbwalkingModes.Harras;

    [PublicAPI]
    [ExportHero(HeroId.npc_dota_hero_drow_ranger)]
    public class Drow : BaseHero
    {
        public drow_ranger_trueshot Aura { get; private set; }

        public MenuItem<bool> DrawRangeItem { get; private set; }

        public drow_ranger_frost_arrows FrostArrows { get; private set; }

        [ItemBinding]
        public item_hurricane_pike HurricanePipe { get; private set; }

        [ItemBinding]
        public item_mask_of_madness Mom { get; private set; }

        [ItemBinding]
        public item_invis_sword ShadowBlade { get; private set; }

        public drow_ranger_wave_of_silence Silence { get; private set; }

        [ItemBinding]
        public item_silver_edge SilverEdge { get; private set; }

        public drow_ranger_marksmanship Ult { get; private set; }

        public float ProjectileSpeed { get; private set; }

        protected override ComboOrbwalkingMode GetComboOrbwalkingMode()
        {
            return new DrowComboOrbwalker(this);
        }

        protected override HarrasOrbwalkingMode GetHarrasOrbwalkingMode()
        {
            return new DrowHarras(this);
        }

        protected override void OnActivate()
        {
            base.OnActivate();

            this.ProjectileSpeed = this.Owner.ProjectileSpeed();

            this.FrostArrows = this.Context.AbilityFactory.GetAbility<drow_ranger_frost_arrows>();
            this.Silence = this.Context.AbilityFactory.GetAbility<drow_ranger_wave_of_silence>();
            this.Aura = this.Context.AbilityFactory.GetAbility<drow_ranger_trueshot>();
            this.Ult = this.Context.AbilityFactory.GetAbility<drow_ranger_marksmanship>();

            var factory = this.Menu.Hero.Factory;
            this.DrawRangeItem = factory.Item("Draw Ult Range", true);
            this.DrawRangeItem.PropertyChanged += this.DrawRangeItemPropertyChanged;

            if (this.DrawRangeItem)
            {
                this.Context.Particle.DrawRange(this.Owner, "vaper_drowUltRange", this.Ult.Radius, Color.Green);
                UpdateManager.Subscribe(this.UpdateDrawRange, 250);
            }
        }

        protected override void OnDeactivate()
        {
            this.DrawRangeItem.PropertyChanged -= this.DrawRangeItemPropertyChanged;
            UpdateManager.Unsubscribe(this.UpdateDrawRange);

            base.OnDeactivate();
        }

        private void DrawRangeItemPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (this.DrawRangeItem)
            {
                this.Context.Particle.DrawRange(this.Owner, "vaper_drowUltRange", this.Ult.Radius, Color.Green);
                UpdateManager.Subscribe(this.UpdateDrawRange, 250);
            }
            else
            {
                UpdateManager.Unsubscribe(this.UpdateDrawRange);
                this.Context.Particle.Remove("vaper_drowUltRange");
            }
        }

        private void UpdateDrawRange()
        {
            var range = this.Ult.Radius;

            var enemies = EntityManager<Hero>.Entities.Where(x => x.IsVisible && x.IsAlive && !x.IsIllusion && this.Owner.IsEnemy(x)).ToList();
            if (enemies.Any(x => x.Position.Distance(this.Owner.Position) < range))
            {
                this.Context.Particle.DrawRange(this.Owner, "vaper_drowUltRange", this.Ult.Radius, Color.Red);
            }
            else if (enemies.Any(x => x.Distance2D(this.Owner) <= (range * 1.5f)))
            {
                this.Context.Particle.DrawRange(this.Owner, "vaper_drowUltRange", this.Ult.Radius, Color.Yellow);
            }
            else
            {
                this.Context.Particle.DrawRange(this.Owner, "vaper_drowUltRange", this.Ult.Radius, Color.Green);
            }
        }
    }
}