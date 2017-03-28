using System.Collections.Generic;
using System.Diagnostics.Eventing.Reader;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Ensage;
using Ensage.Common.Enums;
using Ensage.Common.Extensions;
using Ensage.Common.Menu;
using Ensage.Common.Threading;
using log4net;
using PlaySharp.Toolkit.Logging;
using SharpDX;
using Zaio.Helpers;
using Zaio.Interfaces;
using AbilityId = Ensage.Common.Enums.AbilityId;


namespace Zaio.Heroes
{
    [Hero(ClassID.CDOTA_Unit_Hero_TrollWarlord)]
    internal class TrollWarlord : ComboHero
    {
        private static readonly ILog Log = AssemblyLogs.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private static readonly string[] SupportedAbilities =
        {
            "troll_warlord_berserkers_rage",
            "troll_warlord_whirling_axes_ranged",
            "troll_warlord_whirling_axes_melee",
            "troll_warlord_battle_trance",
            "item_invis_sword",
            "item_silver_edge"
        };

        private static readonly string[] KillstealAbilities =
        {
            "troll_warlord_whirling_axes_ranged",
            "troll_warlord_whirling_axes_melee"
        };

        private Ability _toggleAbility;
        private Ability _rangedAbility;
        private Ability _meleeAbility;
        private Ability _ultAbility;

        public override void OnLoad()
        {
            base.OnLoad();

            var heroMenu = new Menu("Troll", "zaioTroll", false, "npc_dota_hero_troll_warlord", true);

            heroMenu.AddItem(new MenuItem("zaioTrollAbilitiesText", "Supported Abilities"));
            var supportedStuff = new MenuItem("zaioTrollAbilities", string.Empty);
            supportedStuff.SetValue(new AbilityToggler(SupportedAbilities.ToDictionary(x => x, y => true)));
            heroMenu.AddItem(supportedStuff);

            heroMenu.AddItem(new MenuItem("zaioTrollKillstealAbilitiesText", "Supported Killsteal Abilities"));
            var supportedKillsteal = new MenuItem("zaioTrollKillstealAbilities", string.Empty);
            supportedKillsteal.SetValue(new AbilityToggler(KillstealAbilities.ToDictionary(x => x, y => true)));
            heroMenu.AddItem(supportedKillsteal);

            OnLoadMenuItems(supportedStuff, supportedKillsteal);

            ZaioMenu.LoadHeroSettings(heroMenu);

            _toggleAbility = MyHero.GetAbilityById(AbilityId.troll_warlord_berserkers_rage);
            _rangedAbility = MyHero.GetAbilityById(AbilityId.troll_warlord_whirling_axes_ranged);
            _meleeAbility = MyHero.GetAbilityById(AbilityId.troll_warlord_whirling_axes_melee);
            _ultAbility = MyHero.GetAbilityById(AbilityId.troll_warlord_battle_trance);
        }

        protected override async Task<bool> Killsteal()
        {
            if (await base.Killsteal())
            {
                return true;
            }

            if (MyHero.IsSilenced())
            {
                return false;
            }


            if (_rangedAbility.IsKillstealAbilityEnabled() && !_toggleAbility.IsToggled && _rangedAbility.CanBeCasted())
            {
                var damage = _rangedAbility.GetAbilityData("axe_damage");
                damage *= GetSpellAmp();
                var enemies =
                    ObjectManager.GetEntitiesParallel<Hero>()
                                 .Where(
                                     x =>
                                         x.IsAlive && x.Team != MyHero.Team && !x.IsIllusion &&
                                         _rangedAbility.CanBeCasted(x) &&
                                         _rangedAbility.CanHit(x) &&
                                         x.Health < damage * (1 - x.MagicResistance()) &&
                                         !x.CantBeAttacked() && !x.CantBeKilled());

                    var speed = _rangedAbility.GetAbilityData("axe_speed");

                foreach (var enemy in enemies)
                {

                    var time = enemy.Distance2D(MyHero)/speed * 1000;
                    var predictedPosition = Prediction.Prediction.PredictPosition(enemy, (int) time, true);

                    if (predictedPosition == Vector3.Zero)
                    {
                        continue;
                    }

                if (enemy != null)
                {
                    Log.Debug(
                        $"use killsteal ult because enough damage {enemy.Health} <= {damage * (1.0f - enemy.MagicResistance())} ");
                    _rangedAbility.UseAbility(predictedPosition);
                    await Await.Delay(GetAbilityDelay(predictedPosition, _ultAbility));
                    return true;
                }
                else if (!enemies.Any(x => x.Health < damage * (1- x.MagicDamageResist)))
                {
                    Log.Debug($"toggling first skill because can't killsteal with ranged");
                    _toggleAbility.ToggleAbility();
                    await Await.Delay(GetAbilityDelay(_toggleAbility));
                    return false;
                }
            }
        }
            if (Target != null)
            {
                return false;
            }

            if (_meleeAbility.IsKillstealAbilityEnabled() && _toggleAbility.IsToggled && _meleeAbility.CanBeCasted())
            {
                var maxMeleeRange = _meleeAbility.GetAbilityData("max_range");
                var damage = _meleeAbility.GetAbilityData("damage");
                damage *= GetSpellAmp();

                var enemy =
                    ObjectManager.GetEntitiesParallel<Hero>()
                                 .FirstOrDefault(
                                     x =>
                                         x.IsAlive && x.Team != MyHero.Team && _ultAbility.CanBeCasted(x) &&
                                         _ultAbility.CanHit(x) && !x.IsIllusion && x.Distance2D(MyHero) <= maxMeleeRange &&
                                         (damage * (1 - x.MagicDamageResist)) > x.Health && !x.CantBeAttacked() &&
                                         !x.CantBeKilled());

                    if (enemy != null)
                    {
                        Log.Debug($"use melee axe's for killsteal because {(damage * (1 - enemy.MagicDamageResist))} >= {enemy.Health}");
                        _meleeAbility.UseAbility();
                        await Await.Delay(GetAbilityDelay(enemy, _ultAbility));
                        return true;
                    }
            }

            return false;
        }

        public override async Task ExecuteComboAsync(Unit target, CancellationToken tk = new CancellationToken())
        {
                if (!MyHero.IsInvisible())
                {
                    var shadowBlade = MyHero.GetItemById(ItemId.item_invis_sword) ??
                                      MyHero.GetItemById(ItemId.item_silver_edge);
                    var distance = MyHero.Distance2D(target);
                    var rangeMode = !_toggleAbility.IsToggled;
                    if (shadowBlade != null && shadowBlade.IsAbilityEnabled() && shadowBlade.CanBeCasted() && !MyHero.IsVisibleToEnemies &&
                        distance < MyHero.MovementSpeed * 7)
                    {
                        if (rangeMode && !MyHero.IsSilenced())
                        {
                            Log.Debug($"toggling to melee for extra move speed");
                            _toggleAbility.ToggleAbility();
                            await Await.Delay(GetAbilityDelay(target, _toggleAbility), tk);
                        }
                        Log.Debug($"using invis");
                        shadowBlade.UseAbility();
                        var fadeTime = shadowBlade.GetAbilityData("windwalk_fade_time") * 2 * 1000; // 0.3
                        await Await.Delay((int) fadeTime, tk);
                    }
                }
               

            if (!MyHero.IsSilenced() && !MyHero.IsInvisible())
            {
                if (!_toggleAbility.IsToggled && _rangedAbility.IsAbilityEnabled() && _rangedAbility.CanBeCasted(target) && _rangedAbility.CanHit(target) &&
                    !MyHero.IsInvisible())
                {
                       Log.Debug(
                           $"using slow so we won't get kited in melee");
                       _rangedAbility.UseAbility(target.NetworkPosition);
                       await Await.Delay(GetAbilityDelay(target, _rangedAbility), tk);
                       //return;
                }

                else if (_toggleAbility.IsToggled && _toggleAbility.IsAbilityEnabled() && _rangedAbility.CanBeCasted(target) && _rangedAbility.CanHit(target))
                {
                    Log.Debug($"toggling so we can use slow");
                    _toggleAbility.ToggleAbility();
                    await Await.Delay(GetAbilityDelay(target, _toggleAbility), tk);
                    return;
                }

                if (_meleeAbility.IsAbilityEnabled() && _toggleAbility.IsToggled && _meleeAbility.CanBeCasted(target) && _meleeAbility.CanHit(target) &&
                    !MyHero.IsInvisible())
                {
                       Log.Debug(
                           $"using melee axe");
                       _meleeAbility.UseAbility();
                       await Await.Delay(GetAbilityDelay(target, _meleeAbility), tk);
                       //return;
                }

                else if (_toggleAbility.IsAbilityEnabled() && !_toggleAbility.IsToggled && _meleeAbility.CanBeCasted(target) && _meleeAbility.CanHit(target))
                {
                    Log.Debug($"toggling so we can use melee axe");
                    _toggleAbility.ToggleAbility();
                    await Await.Delay(GetAbilityDelay(target, _toggleAbility), tk);
                    return;
                }

                if (_toggleAbility.IsAbilityEnabled() && target.HasModifier("modifier_troll_warlord_whirling_axes_slow") && !_toggleAbility.IsToggled &&
                    _toggleAbility.CanBeCasted())
                {
                    Log.Debug($"toggling so we can hit bashes");
                    _toggleAbility.ToggleAbility();
                    await Await.Delay(GetAbilityDelay(target, _toggleAbility), tk);
                    return;
                }

                if (_ultAbility.IsAbilityEnabled() && _ultAbility.CanBeCasted() && target.Distance2D(MyHero) <= 300)
                    {
                        Log.Debug($"use ult");
                        _ultAbility.UseAbility();
                        await Await.Delay(100, tk);
                    }
            }

            // make him disabled
            if (await DisableEnemy(target, tk) == DisabledState.UsedAbilityToDisable)
            {
                Log.Debug($"disabled!");
                // return;
            }

            await UseItems(target, tk);

            // check if we are near the enemy
            if (!await MoveOrBlinkToEnemy(target, tk))
            {
                Log.Debug($"return because of blink");
                return;
            }

            if (ZaioMenu.ShouldUseOrbwalker)
            {
                Orbwalk();
                Log.Debug($"orbwalking");
            }
            else
            {
                MyHero.Attack(target);
                await Await.Delay(125, tk);
            }
        }
    }
}
