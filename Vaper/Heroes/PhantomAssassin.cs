// <copyright file="PhantomAssassin.cs" company="Ensage">
//    Copyright (c) 2017 Ensage.
// </copyright>

namespace Vaper.Heroes
{
    using System;
    using System.ComponentModel;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;

    using Ensage;
    using Ensage.SDK.Abilities.Items;
    using Ensage.SDK.Abilities.npc_dota_hero_phantom_assassin;
    using Ensage.SDK.Extensions;
    using Ensage.SDK.Helpers;
    using Ensage.SDK.Inventory.Metadata;
    using Ensage.SDK.Menu;
    using Ensage.SDK.Utils;

    using PlaySharp.Toolkit.Helper.Annotations;

    using SharpDX;

    using Vaper.OrbwalkingModes;
    using Vaper.OrbwalkingModes.Combo;
    using Vaper.OrbwalkingModes.Harras;

    using Color = System.Drawing.Color;

    [PublicAPI]
    [ExportHero(HeroId.npc_dota_hero_phantom_assassin)]
    public class PhantomAssassin : BaseHero
    {
        [ItemBinding]
        public item_abyssal_blade AbyssalBlade { get; private set; }

        public phantom_assassin_blur Blur { get; private set; }

        public MenuItem<bool> BlurIndicator { get; private set; }

        public phantom_assassin_coup_de_grace Crit { get; private set; }

        public MenuItem<bool> CritIndicator { get; private set; }

        public float CritPrd { get; private set; } // 0.03221f; // = 15%

        public float CurrentCritChance { get; private set; }

        public phantom_assassin_stifling_dagger Dagger { get; private set; }

        public phantom_assassin_phantom_strike PhantomStrike { get; private set; }

        protected override ComboOrbwalkingMode GetComboOrbwalkingMode()
        {
            return new PhantomAssassinOrbwalker(this);
        }

        protected override void OnActivate()
        {
            base.OnActivate();
            this.Dagger = this.Context.AbilityFactory.GetAbility<phantom_assassin_stifling_dagger>();
            this.PhantomStrike = this.Context.AbilityFactory.GetAbility<phantom_assassin_phantom_strike>();
            this.Blur = this.Context.AbilityFactory.GetAbility<phantom_assassin_blur>();
            this.Crit = this.Context.AbilityFactory.GetAbility<phantom_assassin_coup_de_grace>();

            this.CritPrd = Utils.GetPseudoChance(this.Crit.ProcChance);

            var factory = this.Menu.Hero.Factory;
            this.CritIndicator = factory.Item("Show Crit Indicator", true);
            this.CritIndicator.PropertyChanged += this.CritIndicatorPropertyChanged;
            this.BlurIndicator = factory.Item("Show Blur Indicator", true);
            this.BlurIndicator.PropertyChanged += this.BlurIndicatorPropertyChanged;

            if (this.CritIndicator)
            {
                this.Context.Renderer.Draw += this.OnDraw;
            }

            Entity.OnInt32PropertyChange += this.OnNetworkActivity;
        }

        protected override void OnDeactivate()
        {
            this.CritIndicator.PropertyChanged -= this.CritIndicatorPropertyChanged;
            this.BlurIndicator.PropertyChanged -= this.BlurIndicatorPropertyChanged;

            Entity.OnInt32PropertyChange -= this.OnNetworkActivity;
            this.Context.Renderer.Draw -= this.OnDraw;

            base.OnDeactivate();
        }

        protected override async Task OnKillsteal(CancellationToken token)
        {
            if (Game.IsPaused || !this.Owner.IsAlive || !this.Dagger.CanBeCasted)
            {
                await Task.Delay(125, token);
                return;
            }

            var killstealTarget = EntityManager<Hero>.Entities.FirstOrDefault(
                x => x.IsAlive
                     && (x.Team != this.Owner.Team)
                     && !x.IsIllusion
                     && this.Dagger.CanHit(x)
                     && !x.IsLinkensProtected()
                     && (this.Dagger.GetDamage(x) > x.Health));

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

        protected override HarrasOrbwalkingMode GetHarrasOrbwalkingMode()
        {
            return new PhantomAssassinHarras(this);
        }

        protected override void OnUpdateParticles()
        {
            if (this.BlurIndicator && this.Blur.CanBeCasted)
            {
                if (!this.Owner.HasModifier(this.Blur.ModifierName))
                {
                    this.Context.Particle.AddOrUpdate(this.Owner, "vaper_blurIndicator", @"particles/dire_fx/tower_bad_lamp_f.vpcf", ParticleAttachment.AbsOriginFollow);
                }
                else
                {
                    this.Context.Particle.Remove("vaper_blurIndicator");
                }
            }

            base.OnUpdateParticles();
        }

        private void BlurIndicatorPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (!this.BlurIndicator)
            {
                this.Context.Particle.Remove("vaper_blurIndicator");
            }
        }

        private void CritIndicatorPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (this.CritIndicator)
            {
                this.Context.Renderer.Draw += this.OnDraw;
            }
            else
            {
                this.Context.Renderer.Draw -= this.OnDraw;
            }
        }

        private void OnDraw(object sender, EventArgs e)
        {
            if (!this.CritIndicator || (this.Crit.Ability.Level <= 0))
            {
                return;
            }

            Vector2 screenPos;
            var barPos = this.Owner.Position + new Vector3(0, 0, this.Owner.HealthBarOffset);
            if (Drawing.WorldToScreen(barPos, out screenPos))
            {
                this.Context.Renderer.DrawRectangle(new RectangleF(screenPos.X - 40, screenPos.Y - 15, 80, 7), Color.Red);

                var critWidth = 80.0f * this.CurrentCritChance;
                this.Context.Renderer.DrawLine(new Vector2(screenPos.X - 40, screenPos.Y - 11), new Vector2((screenPos.X - 40) + critWidth, screenPos.Y - 11), Color.Red, 7);
            }
        }

        private void OnNetworkActivity(Entity sender, Int32PropertyChangeEventArgs args)
        {
            if (this.Crit.Ability.Level <= 0)
            {
                return;
            }

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
                    this.CurrentCritChance = Math.Min(1.0f, this.CurrentCritChance + this.CritPrd);
                    break;

                case NetworkActivity.Crit:
                    // Pseudo-random_distribution
                    this.CurrentCritChance = this.CritPrd;
                    break;
            }
        }
    }
}