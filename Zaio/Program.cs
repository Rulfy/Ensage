using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Ensage;
using Ensage.Common;
using Ensage.Common.Extensions;
using Ensage.Common.Threading;
using log4net;
using PlaySharp.Toolkit.Logging;
using SharpDX;
using Zaio.Helpers;
using Zaio.Heroes;
using Zaio.Interfaces;
using AbilityId = Ensage.AbilityId;
using MyAsyncHelpers = Zaio.Helpers.MyAsyncHelpers;
using Ensage.Common.Extensions.SharpDX;

namespace Zaio
{
    internal class Program
    {
        private static readonly ILog Log = AssemblyLogs.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private static ComboHero _currentHero;
        private static Dictionary<Unit, UnitController> _controlledUnits = new Dictionary<Unit, UnitController>();

        private static void Main()
        {
            Events.OnLoad += Events_OnLoad;
            Events.OnClose += Events_OnClose;
            Drawing.OnDraw += Drawing_OnDraw;

           // Game.OnIngameUpdate += Game_OnIngameUpdate;
           // ObjectManager.OnAddEntity += ObjectManager_OnAddEntity;
        }

        private static async Task ExecuteTick(UnitController controller)
        {
            await controller.Tick();
        }

        private static void OnIngameUpdate(EventArgs args)
        {
            if (Game.IsPaused)
            {
                return;
            }

            if (Utils.SleepCheck("zaio.unitControllersUpdate"))
            {
                Utils.Sleep(500, "zaio.unitControllersUpdate");
                _controlledUnits =
                    _controlledUnits.Where(x => x.Key.IsValid && x.Key.IsAlive && x.Key.IsControllable)
                                    .ToDictionary(x => x.Key, y => y.Value);
                var units =
                    ObjectManager.GetEntitiesParallel<Unit>()
                                 .Where(
                                     x =>
                                         x.IsValid && x.IsAlive && !(x is Hero) && !(x is Building) && !(x is Courier) && 
                                         x.ClassId != ClassId.CDOTA_Unit_Hero_Beastmaster_Hawk && x.IsControllable &&
                                         x.MoveCapability != MoveCapability.None && !_controlledUnits.ContainsKey(x));
                foreach (var unit in units)
                {
                    Log.Debug($"found new unit for unitcontroller {unit.Name}");
                    _controlledUnits.Add(unit, new UnitController(_currentHero, unit));
                }
            }
            if (_controlledUnits.Any())
            {
                var selectionOverrides = ZaioMenu.SelectionOverridesControlMode;
                if (selectionOverrides)
                {
                    var selection = ObjectManager.LocalPlayer.Selection.OfType<Unit>();
                    foreach (
                        var unit in _controlledUnits.Values.Where(x => !selection.Contains(x.ControlledUnit)).ToList())
                    {
                        Await.Block($"zaioUnitController_{unit.GetHashCode()}", unit.Tick);
                    }
                }
                else
                {
                    foreach (var unit in _controlledUnits.Values.ToList())
                    {
                        Await.Block($"zaioUnitController_{unit.GetHashCode()}", unit.Tick);
                    }
                }
            }
            if ((ZaioMenu.ShouldRespectEvader && !Utils.SleepCheck("Evader.Avoiding")) || _currentHero.ComboTarget != null)
            {
                //Log.Debug($"abort unaggro because evade or in combo mode");
                return;
            }
            var autoUnaggroTowers = ZaioMenu.AutoUnaggroTowers;
            var autoUnaggroCreeps = ZaioMenu.AutoUnaggroCreeps;
            if (autoUnaggroTowers || autoUnaggroCreeps)
            {
                var hero = _currentHero.Hero;

                var attackers =
                    ObjectManager.GetEntitiesParallel<Unit>()
                                 .Where(x => x.IsValid && x.IsAlive && x.Team != hero.Team &&
                                             (
                                                 autoUnaggroTowers && x is Tower && ((Tower) x).AttackTarget == hero
                                                 ||
                                                 autoUnaggroCreeps && x is Creep && ((Creep) x).IsSpawned &&
                                                 hero.RecentDamage > 0 && ((Creep) x).IsAttacking(hero) &&
                                                 x.IsAttacking()
                                             )
                                 );
                if (attackers.Any())
                {
                    foreach (var attacker in attackers)
                    {
                        var range = attacker.IsMelee ? attacker.AttackRange * 3.0f : attacker.AttackRange;
                        var ally = ObjectManager.GetEntitiesParallel<Unit>()
                                                .Where(
                                                    x =>
                                                        x.IsValid && x.IsAlive && x.Team == hero.Team &&
                                                        x.ClassId != ClassId.CDOTA_BaseNPC_Creep_Siege &&
                                                        !(x is Courier) && !(x is Building) &&
                                                        x != hero && x.IsRealUnit() && !x.CantBeAttacked() &&
                                                        x.UnitDistance2D(attacker) <= range)
                                                .OrderBy(x => x.Distance2D(attacker)).FirstOrDefault();
                        if (ally == null)
                        {
                            continue;
                        }
                        var distance = hero.UnitDistance2D(attacker);
                        var allyDistance = ally.UnitDistance2D(attacker);
                        if (allyDistance * 1.1f < distance)
                        {
                            Log.Debug($"attack ally to unaggro");
                            hero.Attack(ally);
                            Await.Block("zaioUnaggroBlock", MyAsyncHelpers.AsyncSleep);
                        }
                        else
                        {
                            Vector3 dir;
                            if (hero.Distance2D(ally) < range / 2)
                            {
                                dir = (ally.NetworkPosition - attacker.NetworkPosition).Normalized();
                            }
                            else
                            {
                                dir = (hero.NetworkPosition - attacker.NetworkPosition).Normalized();
                            }
                            var pos = attacker.NetworkPosition + dir * (allyDistance + 300);
                            hero.Move(pos);
                            Log.Debug($"move behind ally to unaggro {allyDistance} vs {distance}");
                            Await.Block("zaioUnaggroBlock", MyAsyncHelpers.AsyncSleep);
                        }
                        return;
                    }
                }
            }
        }

        private static void ObjectManager_OnAddEntity(EntityEventArgs args)
        {
            Log.Debug($"{args.Entity.Name}");
        }

        private static void Game_OnIngameUpdate(EventArgs args)
        {
            var hero = ObjectManager.LocalHero;
            if (hero == null)
            {
                return;
            }
            Console.WriteLine(hero.NetworkPosition);

            //var enemy = ObjectManager.GetEntities<Entity>().First(x => x.Team != hero.Team);

            ////Console.WriteLine(hero.IsMagicImmune());

            //foreach (var heroModifier in hero.Modifiers)
            ////{
            ////    Console.WriteLine(heroModifier.Name);
            ////}

            //var illus = ObjectManager.GetEntities<Hero>().First(x => x.IsIllusion);

            ////Console.WriteLine(hero.IsMagicImmune());

            //foreach (var heroModifier in illus.Modifiers)
            //{
            //    Console.WriteLine($"{heroModifier.Name}: {heroModifier.RemainingTime}");
            //}
        }

        private static void Drawing_OnDraw(EventArgs args)
        {
            _currentHero?.OnDraw();
        }

        private static void Events_OnClose(object sender, EventArgs e)
        {
            ZaioMenu.ResetHeroSettings();
            _controlledUnits.Clear();
            if (_currentHero == null)
            {
                return;
            }
            GameDispatcher.OnIngameUpdate -= OnIngameUpdate;

            _currentHero.OnClose();
            _currentHero.Deactivate();
            _currentHero = null;
        }

        private static void Events_OnLoad(object sender, EventArgs e)
        {
            ZaioMenu.OnLoad();

            var hero = ObjectManager.LocalHero;
            if (hero != null)
            {
                var types = Assembly.GetExecutingAssembly().GetTypes().Where(x => x.Namespace == "Zaio.Heroes");
                foreach (var type in types)
                {
                    var property = type.GetCustomAttribute<HeroAttribute>();
                    if (property != null)
                    {
                        Log.Debug($"OnLoad ID: {property.Id}");
                        if (hero.ClassId == property.Id)
                        {
                            Log.Debug($"Found hero.. activating!");
                            _currentHero = (ComboHero) Activator.CreateInstance(type);
                            break;
                        }
                    }
                }
                
                if (_currentHero == null)
                {
                    _currentHero = new GenericHero();
                    Game.PrintMessage(
                    $"Zaio: <font color='#FF1133'>The abilities of {Game.Localize(hero.Name)} are not supported!</font> But items and orbwalking will still work.");
                }
            
                _currentHero.OnLoad();
                _currentHero.Activate();

                if (ZaioMenu.ActiveControlMode != ActiveControlMode.None)
                {
                    Game.PrintMessage(
                   $"Zaio: Unit control mode <font color='#FF1133'>{ZaioMenu.ActiveControlMode}</font> is enabled!");
                }

                GameDispatcher.OnIngameUpdate += OnIngameUpdate;
            }
        }
    }
}