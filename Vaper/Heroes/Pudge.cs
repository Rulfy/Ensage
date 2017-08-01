// <copyright file="Pudge.cs" company="Ensage">
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
    using Ensage.Common.Menu;
    using Ensage.SDK.Abilities.Items;
    using Ensage.SDK.Abilities.npc_dota_hero_pudge;
    using Ensage.SDK.Extensions;
    using Ensage.SDK.Handlers;
    using Ensage.SDK.Helpers;
    using Ensage.SDK.Inventory.Metadata;
    using Ensage.SDK.Menu;
    using Ensage.SDK.Prediction;

    using log4net;

    using PlaySharp.Toolkit.Helper.Annotations;
    using PlaySharp.Toolkit.Logging;

    using Vaper.OrbwalkingModes;
    using Vaper.OrbwalkingModes.Combo;

    [PublicAPI]
    [ExportHero(HeroId.npc_dota_hero_pudge)]
    public class Pudge : BaseHero
    {
        private static readonly ILog Log = AssemblyLogs.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        [ItemBinding]
        public item_rod_of_atos Atos { get; private set; }

        [ItemBinding]
        public item_blink Blink { get; private set; }

        public pudge_dismember Dismember { get; private set; }

        [ItemBinding]
        public item_force_staff ForceStaff { get; set; }

        public bool HasUserEnabledRot { get; private set; }

        public pudge_meat_hook Hook { get; private set; }

        public bool HookModifierDetected { get; set; }

        public HitChance MinimumHookChance { get; private set; }

        public MenuItem<StringList> MinimumHookChanceItem { get; private set; }

        public TaskHandler OnUpdateHandler { get; private set; }

        public pudge_rot Rot { get; private set; }

        [ItemBinding]
        public item_soul_ring SoulRing { get; private set; }

        [ItemBinding]
        public item_urn_of_shadows Urn { get; private set; }

        protected override ComboOrbwalkingMode GetComboOrbwalkingMode()
        {
            return new PudgeOrbwalker(this);
        }

        protected override void OnActivate()
        {
            base.OnActivate();

            this.Hook = this.Context.AbilityFactory.GetAbility<pudge_meat_hook>();
            this.Rot = this.Context.AbilityFactory.GetAbility<pudge_rot>();
            this.Dismember = this.Context.AbilityFactory.GetAbility<pudge_dismember>();

            var factory = this.Menu.Hero.Factory;
            this.MinimumHookChanceItem = factory.Item("Minimum Hook Chance", new StringList(new[] { "Low", "Medium", "High" }, 1));
            this.MinimumHookChance = this.MinimumHookChanceItem.GetEnum<HitChance>();
            this.MinimumHookChanceItem.PropertyChanged += this.MinimumHookChancePropertyChanged;

            this.OnUpdateHandler = UpdateManager.Run(this.OnUpdate);
            Unit.OnModifierAdded += this.OnHookAdded;
            Player.OnExecuteOrder += this.OnExecuteOrder;
        }

        protected override void OnDeactivate()
        {
            this.MinimumHookChanceItem.PropertyChanged -= this.MinimumHookChancePropertyChanged;

            Player.OnExecuteOrder -= this.OnExecuteOrder;
            Unit.OnModifierAdded -= this.OnHookAdded;
            this.OnUpdateHandler.Cancel();

            base.OnDeactivate();
        }

        private void MinimumHookChancePropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            this.MinimumHookChance = this.MinimumHookChanceItem.GetEnum<HitChance>();
        }

        private void OnExecuteOrder(Player sender, ExecuteOrderEventArgs args)
        {
            if (args.IsPlayerInput && (args.OrderId == OrderId.ToggleAbility) && (args.Ability == this.Rot.Ability))
            {
                Log.Debug($"user rot armlet");
                this.HasUserEnabledRot = !this.Rot.Enabled;
            }
        }

        private void OnHookAdded(Unit sender, ModifierChangedEventArgs args)
        {
            if (this.Owner.IsEnemy(sender) && (args.Modifier.Name == this.Hook.TargetModifierName))
            {
                Log.Debug($"Hook detected");
                this.HookModifierDetected = true;
            }
        }

        private async Task OnUpdate(CancellationToken token)
        {
            if (Game.IsPaused || !this.Owner.IsAlive || !this.Rot.CanBeCasted)
            {
                await Task.Delay(250, token);
                return;
            }

            var rotEnabled = this.Rot.Enabled;
            if (rotEnabled && !this.HasUserEnabledRot)
            {
                var enemyNear = EntityManager<Hero>.Entities.Any(x => x.IsVisible && x.IsAlive && this.Owner.IsEnemy(x) && this.Rot.CanHit(x));
                if (!enemyNear)
                {
                    this.Rot.Enabled = false;
                    rotEnabled = false;
                    await Task.Delay(this.Rot.GetCastDelay(), token);
                }
            }

            var denyHealth = this.Rot.GetTickDamage(this.Owner);
            var useSoulRing = false;
            if ((this.SoulRing != null) && this.SoulRing.CanBeCasted)
            {
                useSoulRing = true;
                denyHealth += this.SoulRing.HealthCost;
            }

            var ownerHealth = (float)this.Owner.Health;
            if ((ownerHealth <= denyHealth) && (this.Owner.RecentDamage >= ownerHealth))
            {
                if (useSoulRing)
                {
                    this.SoulRing.UseAbility();
                    await Task.Delay(this.SoulRing.GetCastDelay(), token);
                }

                if (!rotEnabled)
                {
                    this.Rot.Enabled = true;
                    await Task.Delay(this.Rot.GetCastDelay(), token);
                }
            }

            await Task.Delay(125, token);
        }
    }
}