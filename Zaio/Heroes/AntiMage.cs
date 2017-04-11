namespace Zaio.Heroes
{
    using System.Linq;
    using System.Reflection;
    using System.Threading;
    using System.Threading.Tasks;

    using Ensage;
    using Ensage.Common;
    using Ensage.Common.Enums;
    using Ensage.Common.Extensions;
    using Ensage.Common.Extensions.SharpDX;
    using Ensage.Common.Menu;
    using Ensage.Common.Threading;

    using log4net;

    using PlaySharp.Toolkit.Logging;

    using SharpDX;

    using Zaio.Helpers;
    using Zaio.Interfaces;

    using AbilityId = Ensage.AbilityId;

    [Hero(ClassId.CDOTA_Unit_Hero_AntiMage)]
    internal class AntiMage : ComboHero
    {
        private static readonly string[] KillstealAbilities = { "antimage_mana_void" };

        private static readonly ILog Log = AssemblyLogs.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private static readonly string[] SupportedAbilities = { "antimage_blink", "antimage_mana_void", "item_manta" };

        private Ability _blinkAbility;

        private MenuItem _minimumEnemyUltCount;

        private Ability _ultAbility;

        private int EnemyCountForUlt => this._minimumEnemyUltCount.GetValue<Slider>().Value;

        public override async Task ExecuteComboAsync(Unit target, CancellationToken tk = new CancellationToken())
        {
            var manta = this.MyHero.GetItemById(ItemId.item_manta);

            if (!await this.MoveOrBlinkToEnemy(target, tk, 0, 150))
            {
                if (!this.MyHero.IsSilenced())
                {
                    if (this._blinkAbility.IsAbilityEnabled() && this._blinkAbility.CanBeCasted(target)
                        && this._blinkAbility.CanHit(target))
                    {
                        var usedManta = false;
                        if (manta != null && manta.IsAbilityEnabled() && manta.CanBeCasted() && this.MyHero.IsSilenced())
                        {
                            Log.Debug($"use manta 1 because silenced");
                            manta.UseAbility();
                            await Await.Delay(125, tk);
                            manta = null;
                            usedManta = true;
                        }

                        if (!usedManta)
                        {
                            if (this.MyHero.Distance2D(target) >= 350)
                            {
                                Vector3 pos;
                                if (!target.IsMoving)
                                {
                                    pos = (target.NetworkPosition - this.MyHero.NetworkPosition).Normalized();
                                    pos *= 100;
                                    pos = target.NetworkPosition - pos;
                                }
                                else
                                {
                                    pos = Prediction.InFront(target, 250);
                                }

                                Log.Debug($"jumping {pos}");
                                this._blinkAbility.UseAbility(pos);
                                await Await.Delay(this.GetAbilityDelay(pos, this._blinkAbility), tk);

                                if (manta != null && manta.IsAbilityEnabled() && manta.CanBeCasted())
                                {
                                    Log.Debug($"use manta after blink");
                                    manta.UseAbility();
                                    await Await.Delay(125, tk);
                                }
                            }
                        }
                            
                    }
                }
            }

            await this.HasNoLinkens(target, tk);
            await this.UseItems(target, tk);
            await this.DisableEnemy(target, tk);

            if (!this.MyHero.IsSilenced())
            {
                if (this._ultAbility.IsAbilityEnabled() && this._ultAbility.CanBeCasted())
                {
                    var radius = this._ultAbility.GetAbilityData("mana_void_aoe_radius");
                    var damage = this._ultAbility.GetAbilityData("mana_void_damage_per_mana");
                    var spellAmp = this.GetSpellAmp();

                    var targetHealth = target.Health * (1.0f + target.MagicResistance());

                    var enemy =
                        ObjectManager.GetEntitiesParallel<Hero>()
                                     .Where(
                                         x =>
                                             x.IsValid && x.IsAlive && x.IsVisible && x.Team != this.MyHero.Team
                                             && this._ultAbility.CanHit(x) && !x.IsLinkensProtected()
                                             && targetHealth < (x.MaximumMana - x.Mana) * damage * spellAmp
                                             && x.Distance2D(target) < radius)
                                     .OrderByDescending(x => x.MaximumMana - x.Mana)
                                     .FirstOrDefault();
                    if (enemy != null)
                    {
                        var useUlt = this.EnemyCountForUlt == 0;
                        if (!useUlt)
                        {
                            var enemyDamage = (enemy.MaximumMana - enemy.Mana) * damage * spellAmp;
                            var enemyCount =
                                ObjectManager.GetEntitiesParallel<Hero>()
                                             .Count(
                                                 x =>
                                                     x.IsValid && x.IsAlive && x.IsVisible
                                                     && x.Team != this.MyHero.Team && !x.IsIllusion
                                                     && x.Health < enemyDamage * (1.0f - x.MagicResistance())
                                                     && x.Distance2D(enemy) < radius);
                            useUlt = this.EnemyCountForUlt <= enemyCount;
                        }

                        if (useUlt)
                        {
                            Log.Debug(
                                $"Kill it with fire {targetHealth} < {(enemy.MaximumMana - enemy.Mana) * damage * spellAmp}");
                            this._ultAbility.UseAbility(enemy);
                            await Await.Delay(this.GetAbilityDelay(enemy, this._ultAbility), tk);
                        }
                    }
                }
            }

            if (ZaioMenu.ShouldUseOrbwalker)
            {
                this.Orbwalk();
            }
            else
            {
                this.MyHero.Attack(target);
                await Await.Delay(125, tk);
            }

            var illusions =
                ObjectManager.GetEntitiesParallel<Hero>()
                             .Where(
                                 x =>
                                     x.IsValid && x.IsAlive && x.IsIllusion && x.IsControllable && x.Team == this.MyHero.Team
                                     && x.Distance2D(this.MyHero) < 1000);
            if (illusions.Any())
            {
                foreach (var illusion in illusions)
                {
                    illusion.Attack(target);
                }
                await Await.Delay(125, tk);
            }
        }

        public override void OnLoad()
        {
            base.OnLoad();

            var heroMenu = new Menu("AntiMage", "zaioAntiMage", false, "npc_dota_hero_antimage", true);

            heroMenu.AddItem(new MenuItem("zaioAntiMageAbilitiesText", "Supported Abilities"));
            var supportedStuff = new MenuItem("zaioAntiMageAbilities", string.Empty);
            supportedStuff.SetValue(new AbilityToggler(SupportedAbilities.ToDictionary(x => x, y => true)));
            heroMenu.AddItem(supportedStuff);

            heroMenu.AddItem(new MenuItem("zaioAntiMageKillstealAbilitiesText", "Supported Killsteal Abilities"));
            var supportedKillsteal = new MenuItem("zaioAntiMageKillstealAbilities", string.Empty);
            supportedKillsteal.SetValue(new AbilityToggler(KillstealAbilities.ToDictionary(x => x, y => true)));
            heroMenu.AddItem(supportedKillsteal);

            this._minimumEnemyUltCount =
                new MenuItem("zaioAntiMageMinEnemyCount", "Minimum Enemies for Ult").SetValue(new Slider(1, 0, 4));
            this._minimumEnemyUltCount.Tooltip = "Minimum enemies besides your target to use ult. Also used for killsteal with (+1)!";
            heroMenu.AddItem(this._minimumEnemyUltCount);

            this.OnLoadMenuItems(supportedStuff, supportedKillsteal);

            ZaioMenu.LoadHeroSettings(heroMenu);

            this._blinkAbility = this.MyHero.GetAbilityById(AbilityId.antimage_blink);
            this._ultAbility = this.MyHero.GetAbilityById(AbilityId.antimage_mana_void);
        }

        protected override async Task<bool> Killsteal()
        {
            if (await base.Killsteal())
            {
                return true;
            }

            if (this.MyHero.IsSilenced())
            {
                return false;
            }

            if (this._ultAbility.IsKillstealAbilityEnabled() && this._ultAbility.CanBeCasted())
            {
                var enemies =
                    ObjectManager.GetEntitiesParallel<Hero>()
                                 .Where(
                                     x =>
                                         x.IsValid && x.IsAlive && x.IsVisible && x.Team != this.MyHero.Team
                                         && !x.IsIllusion && this._ultAbility.CanHit(x) && !x.IsLinkensProtected())
                                 .OrderByDescending(x => x.MaximumMana - x.Mana);
                if (enemies.Any())
                {
                    var damage = this._ultAbility.GetAbilityData("mana_void_damage_per_mana");
                    var radius = this._ultAbility.GetAbilityData("mana_void_aoe_radius");
                    var spellAmp = this.GetSpellAmp();

                    var targetCount = 0;
                    Hero bestTarget = null;
                    foreach (var enemy in enemies)
                    {
                        var enemyDamage = (enemy.MaximumMana - enemy.Mana) * damage * spellAmp;
                        var currentCount =
                            ObjectManager.GetEntitiesParallel<Hero>()
                                         .Count(
                                             x =>
                                                 x.IsValid && x.IsAlive && x.IsVisible && x.Team != this.MyHero.Team
                                                 && !x.IsIllusion && !x.CantBeAttacked() && !x.CantBeKilled()
                                                 && (x == enemy || x.Distance2D(enemy) < radius)
                                                 && x.Health <= enemyDamage * (1.0f - x.MagicResistance()));
                        if (targetCount < currentCount)
                        {
                            targetCount = currentCount;
                            bestTarget = enemy;
                        }
                    }

                    Log.Debug($"killsteal count {targetCount}");
                    if (bestTarget != null && targetCount >= this.EnemyCountForUlt + 1)
                    {
                        Log.Debug($"ks ulti on {bestTarget.Name} with {targetCount} enemies killing");
                        this._ultAbility.UseAbility(bestTarget);
                        await Await.Delay(this.GetAbilityDelay(bestTarget, this._ultAbility));
                        return true;
                    }
                }
            }

            return false;
        }
    }
}