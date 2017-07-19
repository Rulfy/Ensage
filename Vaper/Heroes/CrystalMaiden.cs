// <copyright file="CrystalMaiden.cs" company="Ensage">
//    Copyright (c) 2017 Ensage.
// </copyright>

namespace Vaper.Heroes
{
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;

    using Ensage;
    using Ensage.SDK.Abilities.Items;
    using Ensage.SDK.Abilities.npc_dota_hero_crystal_maiden;
    using Ensage.SDK.Extensions;
    using Ensage.SDK.Helpers;
    using Ensage.SDK.Inventory.Metadata;

    using PlaySharp.Toolkit.Helper.Annotations;

    using Vaper.OrbwalkingModes;
    using Vaper.OrbwalkingModes.Combo;

    [PublicAPI]
    [ExportHero(HeroId.npc_dota_hero_crystal_maiden)]
    public class CrystalMaiden : BaseHero
    {
        [ItemBinding]
        public item_black_king_bar Bkb { get; private set; }

        [ItemBinding]
        public item_blink Blink { get; private set; }

        public crystal_maiden_freezing_field FreezingField { get; private set; }

        public crystal_maiden_frostbite Frostbite { get; private set; }

        [ItemBinding]
        public item_glimmer_cape GlimmerCape { get; private set; }

        [ItemBinding]
        public item_lotus_orb Lotus { get; private set; }

        public crystal_maiden_crystal_nova Nova { get; private set; }

        [ItemBinding]
        public item_veil_of_discord Veil { get; private set; }

        protected override ComboOrbwalkingMode GetComboOrbwalkingMode()
        {
            return new CrystalMaidenOrbwalker(this);
        }

        protected override void OnActivate()
        {
            base.OnActivate();

            this.Nova = this.Ensage.AbilityFactory.GetAbility<crystal_maiden_crystal_nova>();
            this.Frostbite = this.Ensage.AbilityFactory.GetAbility<crystal_maiden_frostbite>();
            this.FreezingField = this.Ensage.AbilityFactory.GetAbility<crystal_maiden_freezing_field>();
        }

        protected override async Task OnKillsteal(CancellationToken token)
        {
            if (Game.IsPaused || !this.Owner.IsAlive || !this.Nova.CanBeCasted || this.Owner.IsChanneling())
            {
                await Task.Delay(125, token);
                return;
            }

            var killstealTarget = EntityManager<Hero>.Entities.FirstOrDefault(
                x => x.IsAlive
                     && this.Owner.IsEnemy(x)
                     && !x.IsIllusion
                     && this.Nova.CanHit(x)
                     && !x.IsLinkensProtected()
                     && (this.Nova.GetDamage(x) > x.Health));

            if (killstealTarget != null)
            {
                if (this.Nova.UseAbility(killstealTarget))
                {
                    var castDelay = this.Nova.GetCastDelay(killstealTarget);
                    await this.AwaitKillstealDelay(castDelay, token);
                }
            }

            await Task.Delay(125, token);
        }
    }
}