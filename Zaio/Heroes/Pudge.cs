using System;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Ensage;
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
using Zaio.Prediction;
using AsyncHelpers = Zaio.Helpers.AsyncHelpers;

namespace Zaio.Heroes
{
    [Hero(ClassID.CDOTA_Unit_Hero_Pudge)]
    internal class Pudge : ComboHero
    {
        private static readonly ILog Log = AssemblyLogs.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private static readonly string[] SupportedAbilities =
        {
            "pudge_meat_hook",
            "pudge_rot",
            "pudge_dismember"
        };

        private static readonly string[] KillstealAbilities =
        {
            "pudge_meat_hook"
        };

        private MenuItem _autoDeny;
        private MenuItem _circleTillHook;
        private bool _hasHookModifier;
        private Ability _hookAbility;

        private Ability _rotAbility;
        private MenuItem _stopOnHook;
        private Ability _ultAbility;

        private bool ShouldAutoDeny => _autoDeny.GetValue<bool>();
        private bool ShouldStopOnHook => _stopOnHook.GetValue<bool>();
        private bool ShouldCircleHook => _circleTillHook.GetValue<bool>();

        public override void OnLoad()
        {
            base.OnLoad();

            var heroMenu = new Menu("Pudge", "zaioPudge", false, "npc_dota_hero_pudge", true);

            heroMenu.AddItem(new MenuItem("zaioPudgeAbilitiesText", "Supported Abilities"));
            var supportedStuff = new MenuItem("zaioPudgeAbilities", string.Empty);
            supportedStuff.SetValue(new AbilityToggler(SupportedAbilities.ToDictionary(x => x, y => true)));
            heroMenu.AddItem(supportedStuff);

            heroMenu.AddItem(new MenuItem("zaioPudgeKillstealAbilitiesText", "Supported Killsteal Abilities"));
            var supportedKillsteal = new MenuItem("zaioPudgeKillstealAbilities", string.Empty);
            supportedKillsteal.SetValue(new AbilityToggler(KillstealAbilities.ToDictionary(x => x, y => true)));
            heroMenu.AddItem(supportedKillsteal);

            _autoDeny = new MenuItem("zaioPudgeAutoDeny", "Auto Deny").SetValue(true);
            _autoDeny.Tooltip = "Will automatically try to use your rot to deny your hero.";
            heroMenu.AddItem(_autoDeny);

            _stopOnHook = new MenuItem("zaioPudgeStopHook", "Stop On Hook").SetValue(true);
            _stopOnHook.Tooltip = "Stops after using hook so you don't approach the enemy.";
            heroMenu.AddItem(_stopOnHook);

            _circleTillHook = new MenuItem("zaioPudgeStopTillHook", "Circle on Hook block").SetValue(true);
            _circleTillHook.Tooltip = "Moves on a circle when your hook is blocked.";
            heroMenu.AddItem(_circleTillHook);

            ZaioMenu.LoadHeroSettings(heroMenu);

            _rotAbility = MyHero.GetAbilityById(AbilityId.pudge_rot);
            _ultAbility = MyHero.GetAbilityById(AbilityId.pudge_dismember);
            _hookAbility = MyHero.GetAbilityById(AbilityId.pudge_meat_hook);

            GameDispatcher.OnIngameUpdate += GameDispatcher_OnIngameUpdate;
            Unit.OnModifierAdded += Unit_OnModifierAdded;
            Unit.OnModifierRemoved += Unit_OnModifierRemoved;
        }

        private void Unit_OnModifierRemoved(Unit sender, ModifierChangedEventArgs args)
        {
            if (args.Modifier.Name == "modifier_pudge_meat_hook")
            {
                _hasHookModifier = false;
            }
        }

        public override void OnClose()
        {
            Unit.OnModifierAdded -= Unit_OnModifierAdded;
            Unit.OnModifierRemoved -= Unit_OnModifierRemoved;
            GameDispatcher.OnIngameUpdate -= GameDispatcher_OnIngameUpdate;
            base.OnClose();
        }

        private void Unit_OnModifierAdded(Unit sender, ModifierChangedEventArgs args)
        {
            if (Target != null && sender == Target && args.Modifier.Name == "modifier_pudge_meat_hook")
            {
                _hasHookModifier = true;
            }
        }

        private void GameDispatcher_OnIngameUpdate(EventArgs args)
        {
            if (!ShouldAutoDeny || !MyHero.IsAlive || MyHero.IsSilenced())
            {
                Await.Block("zaio.pudgeDenySleep", AsyncHelpers.AsyncSleep);
                return;
            }


            if (_rotAbility.CanBeCasted() && !_rotAbility.IsToggled && !MyHero.IsMagicImmune())
            {
                var damage = _rotAbility.GetAbilityData("rot_damage");
                var talent = MyHero.GetAbilityById(AbilityId.special_bonus_unique_pudge_2);
                if (talent.Level > 0)
                {
                    damage += talent.GetAbilityData("value");
                }
                var tick = _rotAbility.GetAbilityData("rot_tick");
                damage *= tick * 3;
                if (MyHero.Health < damage * (1 - MyHero.MagicResistance()))
                {
                    Log.Debug($"Using rot to deny {MyHero.Health} < {damage * (1 - MyHero.MagicResistance())}!!");

                    _rotAbility.ToggleAbility();
                    Await.Block("zaio.pudgeDenySleep", AsyncHelpers.AsyncSleep);
                }
            }
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


            if (Target != null || MyHero.IsChanneling())
            {
                return false;
            }

            if (_hookAbility.CanBeCasted())
            {
                var damage = MyHero.HasItem(ClassID.CDOTA_Item_UltimateScepter)
                    ? _hookAbility.GetAbilityData("damage_scepter")
                    : _hookAbility.GetDamage(_hookAbility.Level - 1);
                damage *= GetSpellAmp();

                var enemies =
                    ObjectManager.GetEntitiesParallel<Hero>()
                                 .Where(
                                     x =>
                                         x.IsAlive && x.Team != MyHero.Team && _hookAbility.CanBeCasted(x) &&
                                         _hookAbility.CanHit(x) && !x.IsIllusion &&
                                         x.Health < damage && !x.CantBeAttacked() &&
                                         !x.CantBeKilled());

                var speed = _hookAbility.GetAbilityData("hook_speed");
                var radius = _hookAbility.GetAbilityData("hook_width") * 2;

                foreach (var enemy in enemies)
                {
                    var time = enemy.Distance2D(MyHero) / speed * 1000.0f;
                    var predictedPos = Prediction.Prediction.PredictPosition(enemy, (int) time, true);
                    if (predictedPos == Vector3.Zero)
                    {
                        continue;
                    }

                    var rec = new Geometry.Polygon.Rectangle(MyHero.NetworkPosition, predictedPos, radius);

                    // test for enemies in range
                    var isUnitBlocking = ObjectManager.GetEntitiesParallel<Unit>()
                                                      .Any(
                                                          x =>
                                                              x.IsValid && x != enemy && x.IsAlive && x != MyHero &&
                                                              x.IsSpawned && x.IsRealUnit() &&
                                                              x.Distance2D(enemy) >= radius &&
                                                              rec.IsInside(x.NetworkPosition));
                    if (!isUnitBlocking)
                    {
                        Log.Debug($"use hook for killsteal");
                        _hookAbility.UseAbility(predictedPos);
                        await Await.Delay(GetAbilityDelay(predictedPos, _hookAbility));
                        return true;
                    }
                }
            }

            return false;
        }

        public override async Task ExecuteComboAsync(Unit target, CancellationToken tk = new CancellationToken())
        {
            if (MyHero.IsChanneling() || MyHero.HasModifier("modifier_pudge_dismember"))
            {
                if (!MyHero.IsSilenced() && _rotAbility.CanBeCasted(target) && !_rotAbility.IsToggled &&
                    _rotAbility.CanHit(target))
                {
                    _rotAbility.ToggleAbility();
                    await Await.Delay(100, tk);
                }
                return;
            }

            if (_hasHookModifier || target.HasModifier("modifier_pudge_meat_hook"))
            {
                if (!MyHero.IsSilenced() && _rotAbility.CanBeCasted() && !_rotAbility.IsToggled)
                {
                    _rotAbility.ToggleAbility();
                    await Await.Delay(ItemDelay, tk);
                }
                if (await HasNoLinkens(target, tk) && _ultAbility.CanBeCasted(target))
                {
                    if (!MyHero.IsSilenced() && _ultAbility.CanHit(target))
                    {
                        _ultAbility.UseAbility(target);
                        await Await.Delay(GetAbilityDelay(target, _ultAbility) + 250, tk);
                    }
                    else if (ShouldStopOnHook)
                    {
                        MyHero.Hold();
                    }
                }
                return;
            }

            if (!MyHero.IsSilenced())
            {
                if (_rotAbility.CanBeCasted(target) && !_rotAbility.IsToggled && _rotAbility.CanHit(target))
                {
                    _rotAbility.ToggleAbility();
                    await Await.Delay(100, tk);
                }

                if (_ultAbility.CanBeCasted(target) && _ultAbility.CanHit(target) && await HasNoLinkens(target, tk))
                {
                    if (_ultAbility.CanHit(target))
                    {
                        _ultAbility.UseAbility(target);
                        await Await.Delay(GetAbilityDelay(target, _ultAbility) + 250, tk);
                        return;
                    }
                }

                if (_hookAbility.CanBeCasted(target) && _hookAbility.CanHit(target))
                {
                    var speed = _hookAbility.GetAbilityData("hook_speed");
                    var radius = _hookAbility.GetAbilityData("hook_width") * 2;


                    var time = target.Distance2D(MyHero) / speed * 1000.0f;
                    var predictedPos = Prediction.Prediction.PredictPosition(target, (int) time, true);
                    if (predictedPos != Vector3.Zero)
                    {
                        var rec = new Geometry.Polygon.Rectangle(MyHero.NetworkPosition, predictedPos, radius);

                        // test for enemies in range
                        var isUnitBlocking = ObjectManager.GetEntitiesParallel<Unit>()
                                                          .Any(
                                                              x =>
                                                                  x.IsValid && x != target && x.IsAlive && x != MyHero &&
                                                                  x.IsSpawned && x.IsRealUnit() && !x.IsIllusion &&
                                                                  x.Distance2D(target) >= radius &&
                                                                  rec.IsInside(x.NetworkPosition));
                        if (!isUnitBlocking)
                        {
                            Log.Debug($"using hook");
                            _hookAbility.UseAbility(predictedPos);
                            await Await.Delay(GetAbilityDelay(predictedPos, _hookAbility), tk);
                            return;
                        }
                        if (ShouldCircleHook)
                        {
                            //MyHero.Hold();
                            var dir = (Game.MousePosition - target.NetworkPosition).Normalized();
                            var distance = (target.NetworkPosition - MyHero.NetworkPosition).Length();
                            var targetPos = target.NetworkPosition + dir * distance;
                            MyHero.Move(Prediction.Prediction.PredictMyRoute(MyHero, 500, targetPos).Last());
                            return;
                        }
                    }
                }
            }

            await UseItems(target, tk);

            // make him disabled
            if (await DisableEnemy(target, tk) == DisabledState.UsedAbilityToDisable)
            {
                Log.Debug($"disabled!");
                // return;
            }

            // check if we are near the enemy
            if (!await MoveOrBlinkToEnemy(target, tk))
            {
                Log.Debug($"return because of blink");
                return;
            }


            if (ZaioMenu.ShouldUseOrbwalker)
            {
                Orbwalk();
            }
            else
            {
                MyHero.Attack(target);
                await Await.Delay(125, tk);
            }
        }
    }
}