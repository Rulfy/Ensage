using System.Collections.Generic;
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
using Zaio.Prediction;

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
        private MenuItem _stopOnHook;

        private bool ShouldAutoDeny => _autoDeny.GetValue<bool>();
        private bool ShouldStopOnHook => _stopOnHook.GetValue<bool>();
        
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
            _autoDeny.Tooltip = "Will automatically try to use your rot to deny your hero";
            heroMenu.AddItem(_autoDeny);

            _stopOnHook = new MenuItem("zaioPudgeStopHook", "Stop On Hook").SetValue(true);
            _stopOnHook.Tooltip = "Stops after using hook so you don't approach the enemy";
            heroMenu.AddItem(_stopOnHook);

            ZaioMenu.LoadHeroSettings(heroMenu);

            GameDispatcher.OnIngameUpdate += GameDispatcher_OnIngameUpdate;
                
        }

        private void GameDispatcher_OnIngameUpdate(System.EventArgs args)
        {
            if (!ShouldAutoDeny || !MyHero.IsAlive)
            {
                Await.Block("zaio.pudgeDenySleep", Sleep);
                return;
            }

            var rot = MyHero.GetAbilityById(AbilityId.pudge_rot);
            if (rot.CanBeCasted() && !rot.IsToggled && !MyHero.IsMagicImmune())
            {
                var damage = rot.GetAbilityData("rot_damage");
                var talent = MyHero.GetAbilityById(AbilityId.special_bonus_unique_pudge_2);
                if (talent.Level > 0)
                {
                    damage += talent.GetAbilityData("value");
                }
                var tick = rot.GetAbilityData("rot_tick");
                damage *= tick * 3;
                if (MyHero.Health < damage * (1 - MyHero.MagicResistance()))
                {
                    Log.Debug($"Using rot to deny {MyHero.Health} < {damage * (1 - MyHero.MagicResistance())}!!");
                    
                    rot.ToggleAbility();
                    Await.Block("zaio.pudgeDenySleep", Sleep);
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

       
            if (Target != null)
            {
                return false;
            }

            var hook = MyHero.Spellbook.SpellQ;
            if (hook.CanBeCasted())
            {
                var damage = MyHero.HasItem(ClassID.CDOTA_Item_UltimateScepter) ? hook.GetAbilityData("damage_scepter") : hook.GetDamage(hook.Level - 1);
                damage *= GetSpellAmp();

                var enemies =
                    ObjectManager.GetEntitiesParallel<Hero>()
                                 .Where(
                                     x =>
                                         x.IsAlive && x.Team != MyHero.Team && hook.CanBeCasted(x) && hook.CanHit(x) &&
                                         x.Health < damage && !x.CantBeAttacked() &&
                                         !x.CantBeKilled());

                var speed = hook.GetAbilityData("hook_speed");
                var radius = hook.GetAbilityData("hook_width");

                foreach (var enemy in enemies)
                {
                    var time = enemy.Distance2D(MyHero) / speed * 1000.0f;
                    var predictedPos = Prediction.Prediction.PredictPosition(enemy, (int)time, true);
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
                        hook.UseAbility(predictedPos);
                        await Await.Delay((int)(hook.FindCastPoint() * 1000.0 + Game.Ping));
                        return true;
                    }
                }
            }

            return false;
        }

        public override async Task ExecuteComboAsync(Unit target, CancellationToken tk = new CancellationToken())
        {
            if (MyHero.IsChanneling()) return;
             
            var rot = MyHero.GetAbilityById(AbilityId.pudge_rot);
            var ult = MyHero.GetAbilityById(AbilityId.pudge_dismember);
            
            if (Target.HasModifier("modifier_pudge_meat_hook"))
            {
                if (rot.CanBeCasted() && !rot.IsToggled)
                {
                    rot.ToggleAbility();
                    await Await.Delay(100, tk);
                }
                if (await HasNoLinkens(Target, tk) && ult.CanBeCasted(Target))
                {
                    if (ult.CanHit(Target))
                    {
                        ult.UseAbility(Target);
                        await Await.Delay((int)(ult.FindCastPoint() * 1000 + 100 + Game.Ping), tk);
                    }
                    else if(ShouldStopOnHook)
                    {
                        MyHero.Hold();
                    }
                }
                return;
            }

            if (rot.CanBeCasted(Target) && !rot.IsToggled && rot.CanHit(Target))
            {
                rot.ToggleAbility();
                await Await.Delay(100, tk);
            }

            if (ult.CanBeCasted(Target) && ult.CanHit(Target) && await HasNoLinkens(Target, tk))
            {

                if (ult.CanHit(Target))
                {
                    ult.UseAbility(Target);
                    await Await.Delay((int)(ult.FindCastPoint() * 1000 + 100 + Game.Ping), tk);
                    return;
                }
            }

            var hook = MyHero.Spellbook.SpellQ;
            if (hook.CanBeCasted(Target) && hook.CanHit(Target))
            {
                var speed = hook.GetAbilityData("hook_speed");
                var radius = hook.GetAbilityData("hook_width");


                var time = Target.Distance2D(MyHero) / speed * 1000.0f;
                var predictedPos = Prediction.Prediction.PredictPosition(Target, (int)time, true);
                if (predictedPos != Vector3.Zero)
                {
                    var rec = new Geometry.Polygon.Rectangle(MyHero.NetworkPosition, predictedPos, radius);

                    // test for enemies in range
                    var isUnitBlocking = ObjectManager.GetEntitiesParallel<Unit>()
                                                      .Any(
                                                          x =>
                                                              x.IsValid && x != Target && x.IsAlive && x != MyHero &&
                                                              x.IsSpawned && x.IsRealUnit() &&
                                                              x.Distance2D(Target) >= radius &&
                                                              rec.IsInside(x.NetworkPosition));
                    if (!isUnitBlocking)
                    {
                        Log.Debug($"using hook");
                        hook.UseAbility(predictedPos);
                        await Await.Delay((int)(hook.FindCastPoint() * 1000.0 + Game.Ping), tk);
                    }
                }
            }

            await UseItems(tk);

            // make him disabled
            if (await DisableEnemy(tk) == DisabledState.UsedAbilityToDisable)
            {
                Log.Debug($"disabled!");
                // return;
            }

            // check if we are near the enemy
            if (!await MoveOrBlinkToEnemy(tk))
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
                MyHero.Attack(Target);
                await Await.Delay(125, tk);
            }
        }
    }
}