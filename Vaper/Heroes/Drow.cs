// <copyright file="Drow.cs" company="Ensage">
//    Copyright (c) 2017 Ensage.
// </copyright>

namespace Vaper.Heroes
{
    using System.ComponentModel;
    using System.Linq;
    using System.Reflection;
    using System.Threading;
    using System.Threading.Tasks;

    using Ensage;
    using Ensage.SDK.Abilities.Items;
    using Ensage.SDK.Abilities.npc_dota_hero_drow_ranger;
    using Ensage.SDK.Extensions;
    using Ensage.SDK.Helpers;
    using Ensage.SDK.Inventory.Metadata;
    using Ensage.SDK.Menu;

    using log4net;

    using PlaySharp.Toolkit.Helper.Annotations;
    using PlaySharp.Toolkit.Logging;

    using SharpDX;

    using Vaper.OrbwalkingModes.Combo;
    using Vaper.OrbwalkingModes.Harras;

    public enum UltStatus
    {
        NotSkilled,

        Active,

        Danger,

        Deactivated
    }

    [PublicAPI]
    [ExportHero(HeroId.npc_dota_hero_drow_ranger)]
    public class Drow : BaseHero
    {
        private static readonly ILog Log = AssemblyLogs.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        public drow_ranger_trueshot Aura { get; private set; }

        public MenuItem<bool> DrawRangeItem { get; private set; }

        public drow_ranger_frost_arrows FrostArrows { get; private set; }

        public bool HasLifeleech
        {
            get
            {
                if ((this.Mom != null) || (this.Satanic != null))
                {
                    return true;
                }

                return this.Owner.HasAnyModifiers("modifier_item_vladimir_aura", "modifier_mask_of_death");
            }
        }

        [ItemBinding]
        public item_hurricane_pike HurricanePipe { get; private set; }

        [ItemBinding]
        public item_manta Manta { get; private set; }

        public Unit LastFrostArrowTarget { get; private set; }

        public float LastFrostArrowTime { get; private set; }

        public float ManaNeeded
        {
            get
            {
                var manaNeeded = 0.0f;

                if (this.ShadowBlade != null)
                {
                    manaNeeded += this.ShadowBlade.Ability.ManaCost;
                }
                else if (this.SilverEdge != null)
                {
                    manaNeeded += this.SilverEdge.Ability.ManaCost;
                }

                if (this.HurricanePipe != null)
                {
                    manaNeeded += this.HurricanePipe.Ability.ManaCost;
                }

                if (this.Mom != null)
                {
                    manaNeeded += this.Mom.Ability.ManaCost;
                }

                if (this.Satanic != null)
                {
                    manaNeeded += this.Satanic.Ability.ManaCost;
                }

                return manaNeeded;
            }
        }

        public MenuItem<bool> MaxFrostArrowUptime { get; private set; }

        [ItemBinding]
        public item_mask_of_madness Mom { get; private set; }

        [ItemBinding]
        public item_mjollnir Mjollnir { get; private set; }

        public float ProjectileSpeed { get; private set; }

        [ItemBinding]
        public item_satanic Satanic { get; private set; }

        [ItemBinding]
        public item_invis_sword ShadowBlade { get; private set; }

        public drow_ranger_wave_of_silence Silence { get; private set; }

        [ItemBinding]
        public item_silver_edge SilverEdge { get; private set; }

        public drow_ranger_marksmanship Ult { get; private set; }

        public UltStatus UltStatus { get; private set; }

        // only use frost arrows if we have enough mana
        public bool ShouldUseFrostArrow(Unit target, bool preferLifeleech = false)
        {
            var useArrow = false;
            if ((this.ManaNeeded <= (this.Owner.Mana - this.FrostArrows.Ability.ManaCost)) && this.FrostArrows.CanBeCasted)
            {
                // dont pull aggro
                var creepsClose = EntityManager<Creep>.Entities.Any(x => x.IsVisible && x.IsAlive && x.IsSpawned && x.IsEnemy(this.Owner) && (x.Distance2D(this.Owner) <= 500.0f));
                useArrow = creepsClose;
                if (!useArrow)
                {
                    // check for maximum slow duration
                    var modifier = target.Modifiers.FirstOrDefault(x => x.Name == this.FrostArrows.TargetModifierName);
                    var duration = this.FrostArrows.GetModifierDuration(target);
                    var hitTime = this.FrostArrows.GetHitTime(target) / 1000.0f;

                    if (this.LastFrostArrowTarget != target)
                    {
                        this.LastFrostArrowTime = 0;
                    }

                    var timeDiff = Game.GameTime - this.LastFrostArrowTime;
                    var arrivalTime = hitTime - timeDiff;
                    if (modifier == null)
                    {
                        // Log.Debug($"{useArrow} = ({duration} + {arrivalTime}) = {duration + arrivalTime} <= {hitTime}");
                        useArrow = (duration + arrivalTime) <= hitTime;
                    }
                    else if (this.MaxFrostArrowUptime && !(preferLifeleech && this.Owner.HealthPercent() <= 0.6f && this.HasLifeleech))
                    {
                        if (arrivalTime > 0)
                        {
                            useArrow = (modifier.RemainingTime + duration + arrivalTime) <= hitTime;
                        }
                        else
                        {
                            useArrow = modifier.RemainingTime <= hitTime;
                        }
                    }
                }
            }

            return useArrow;
        }

        public async Task UseFrostArrow(Unit target, CancellationToken token = default(CancellationToken))
        {
            this.LastFrostArrowTarget = target;
            this.LastFrostArrowTime = Game.GameTime;
            this.FrostArrows.UseAbility(target);
            await Task.Delay(this.FrostArrows.GetCastDelay(target), token);
        }

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
            this.MaxFrostArrowUptime = factory.Item("Max FrostArrow Uptime", true);
            this.MaxFrostArrowUptime.Item.Tooltip = "Tries to have maximum uptime with the slow";

            if (this.DrawRangeItem)
            {
                this.Context.Particle.DrawRange(this.Owner, "vaper_drowUltRange", this.Ult.Radius, Color.Green);
            }

            UpdateManager.Subscribe(this.UpdateDrawRange, 250);
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
            }
            else
            {
                this.Context.Particle.Remove("vaper_drowUltRange");
            }
        }

        private void UpdateDrawRange()
        {
            if (this.Ult.Ability.Level == 0)
            {
                this.UltStatus = UltStatus.NotSkilled;
                return;
            }

            var range = this.Ult.Radius;

            var enemies = EntityManager<Hero>.Entities.Where(x => x.IsVisible && x.IsAlive && !x.IsIllusion && this.Owner.IsEnemy(x)).ToList();
            if (enemies.Any(x => x.Position.Distance(this.Owner.Position) < range))
            {
                if (this.DrawRangeItem && this.UltStatus != UltStatus.Deactivated)
                {
                    this.Context.Particle.Remove("vaper_drowUltRange");
                    this.Context.Particle.DrawRange(this.Owner, "vaper_drowUltRange", this.Ult.Radius, Color.Red);
                }

                this.UltStatus = UltStatus.Deactivated;
            }
            else if (enemies.Any(x => x.Distance2D(this.Owner) <= (range * 1.5f)))
            {
                if (this.DrawRangeItem && this.UltStatus != UltStatus.Danger)
                {
                    this.Context.Particle.Remove("vaper_drowUltRange");
                    this.Context.Particle.DrawRange(this.Owner, "vaper_drowUltRange", this.Ult.Radius, Color.Yellow);
                }

                this.UltStatus = UltStatus.Danger;
            }
            else
            {
                if (this.DrawRangeItem && this.UltStatus != UltStatus.Active)
                {
                    this.Context.Particle.Remove("vaper_drowUltRange");
                    this.Context.Particle.DrawRange(this.Owner, "vaper_drowUltRange", this.Ult.Radius, Color.Green);
                }

                this.UltStatus = UltStatus.Active;
            }
        }
    }
}