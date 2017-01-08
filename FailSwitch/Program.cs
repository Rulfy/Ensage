using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Ensage;
using Ensage.Common;
using Ensage.Common.Extensions;
using Ensage.Common.Menu;
using Ensage.Common.Threading;
using Ensage.Items;
using log4net;
using PlaySharp.Toolkit.Logging;
using SharpDX;
using Attribute = Ensage.Attribute;

namespace FailSwitch
{
    internal enum MagicPierce
    {
        No,
        Yes,
        Agha
    }

    internal class SpecialSpellInfo
    {
        public SpecialSpellInfo(string name, string radius, MagicPierce piercesMagic = MagicPierce.No)
        {
            PiercesMagic = piercesMagic;
            Name = name;
            Radius = radius;
        }

        public string Name { get; }
        public string Radius { get; }
        public MagicPierce PiercesMagic { get; }
    }

    internal class PositionInfo
    {
        public PositionInfo(Vector3 position, float time)
        {
            Position = position;
            Time = time;
        }

        public Vector3 Position { get; }
        public float Time { get; }
    }

    internal class Program
    {
        private static readonly
            ILog Log = AssemblyLogs.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private static Menu _menu;
        private static MenuItem _notifyItem;
        private static MenuItem _areaSpellsItem;
        private static MenuItem _targetSpellsItem;
        private static MenuItem _powerTreadsItem;

        private static int _lastTowerKillTick;
        private static Func<Task> _powerTreadsFunc;
        private static bool _loaded;

        private static readonly List<SpecialSpellInfo> AreaSpells = new List<SpecialSpellInfo>
        {
            new SpecialSpellInfo("enigma_black_hole", "pull_radius", MagicPierce.Yes),
            new SpecialSpellInfo("puck_dream_coil", "coil_radius", MagicPierce.Agha),
            new SpecialSpellInfo("obsidian_destroyer_sanity_eclipse", "radius"),
            new SpecialSpellInfo("faceless_void_chronosphere", "radius", MagicPierce.Yes)
        };

        private static readonly List<SpecialSpellInfo> NoTargetSpells = new List<SpecialSpellInfo>
        {
            new SpecialSpellInfo("magnataur_reverse_polarity", "pull_radius", MagicPierce.Yes),
            new SpecialSpellInfo("tidehunter_ravage", "radius"),
            new SpecialSpellInfo("axe_berserkers_call", "radius", MagicPierce.Yes),
            new SpecialSpellInfo("centaur_hoof_stomp", "radius"),
            new SpecialSpellInfo("slardar_slithereen_crush", "crush_radius"),
            new SpecialSpellInfo("earthshaker_echo_slam", "echo_slam_echo_range")
        };

        private static readonly string[] LinkensSpells =
        {
            "doom_bringer_doom", "lion_finger_of_death", "lina_laguna_blade", "juggernaut_omni_slash",
            "antimage_mana_void", "legion_commander_duel",
            "bloodseeker_rupture", "terrorblade_sunder", "furion_wrath_of_nature", "bane_fiends_grip",
            "lich_chain_frost"
        };

        private static readonly Dictionary<Hero, PositionInfo> _positionInfo = new Dictionary<Hero, PositionInfo>();
        private static Ability _testAbility;
        private static float _testTime;
        private static Func<Hero, bool> _testExpr;

        private static void Main()
        {
            Events.OnLoad += Events_OnLoad;
            Events.OnClose += Events_OnClose;
        }

        private static void Events_OnClose(object sender, EventArgs e)
        {
            _positionInfo.Clear();
        }

        private static void Events_OnLoad(object sender, EventArgs e)
        {
            if (_loaded)
                return;
            _loaded = true;

            _menu = new Menu("Fail Switch", "failSwitch", true);
            _notifyItem = new MenuItem("notifyPlayerForFailing", "Notify when failing").SetValue(true);
            _notifyItem.Tooltip = "Will notify you whenever it prevents you from failing";
            _areaSpellsItem = new MenuItem("areaSpellCheck", "AreaSpells Switch?").SetValue(true);
            _areaSpellsItem.Tooltip =
                "Checks for area spells like black hole or ravage, so that at least one enemy is in range";
            _targetSpellsItem = new MenuItem("targetSpellCheck", "TargetSpells Switch?").SetValue(true);
            _targetSpellsItem.Tooltip =
                "Checks for targeted spells, so you won't use them on linkens or illusion targets";
            _powerTreadsItem = new MenuItem("powerTreadsToggle", "PowerTreads Toggle?").SetValue(true);
            _powerTreadsItem.Tooltip = "Toggles your power treads to int before using spells";

            _menu.AddItem(_notifyItem);
            _menu.AddItem(_areaSpellsItem);
            _menu.AddItem(_targetSpellsItem);
            _menu.AddItem(_powerTreadsItem);
            _menu.AddToMainMenu();


            Player.OnExecuteOrder += Player_OnExecuteAction;
            Game.OnFireEvent += Game_OnGameEvent;
            Game.OnIngameUpdate += Game_OnIngameUpdate;
            //Game.OnIngameUpdate += PositionTrackerUpdate;
            GameDispatcher.OnIngameUpdate += GameDispatcher_PowerTreads;
        }

        private static void PositionTrackerUpdate(EventArgs args)
        {
            if (!Utils.SleepCheck("failswitchPositionUpdate"))
                return;
            Utils.Sleep(125, "failswitchPositionUpdate");

            var hero = ObjectManager.LocalHero;
            if (hero == null)
                return;
            var enemies = ObjectManager.GetEntities<Hero>().Where(
                x => x.IsAlive && (x.Team != hero.Team) &&
                     (
                         x.Inventory.Items.Any(y => (y.Name == "item_invis_sword") || (y.Name == "item_silver_edge")) ||
                         x.Spellbook.Spells.Any(
                             y =>
                                 (y.Name == "bounty_hunter_wind_walk") || (y.Name == "clinkz_wind_walk") ||
                                 (y.Name == "riki_permanent_invisibility") || (y.Name == "item_glimmer_cape") || (y.Name == "broodmother_spin_web"))
                     )
            );
        }

        private static void GameDispatcher_PowerTreads(EventArgs args)
        {
            if (Game.IsPaused || (_powerTreadsFunc == null))
                return;
            Await.Block("awaitPowerTreadsToggle", _powerTreadsFunc);
        }

        private static void Game_OnIngameUpdate(EventArgs args)
        {
            if ((_testAbility == null) || !Utils.SleepCheck("failSwitchStopFailing") || Game.IsPaused)
                return;

            var time = Game.RawGameTime;
            var castPoint = (float) _testAbility.FindCastPoint();
            if (time - _testTime >= castPoint - Game.Ping/1000.0f)
            {
                Log.Debug($"not testing {_testAbility.Name} anymore ({time - _testTime} | {castPoint} | {Game.Ping})");
                _testAbility = null;
                return;
            }

            var enemies =
                ObjectManager.GetEntities<Hero>().Where(_testExpr).ToList();
            if (!enemies.Any())
            {
                NotifyPlayer("Stop failing", Game.Localize(_testAbility.Name));
                Log.Debug($"abort ability!");
                _testAbility = null;
                ObjectManager.LocalHero.Stop();
                Utils.Sleep(250, "failSwitchStopFailing");
            }
            else
            {
                Log.Debug($"found enemy {enemies.FirstOrDefault()?.Name}");
            }
        }

        private static void Game_OnGameEvent(FireEventEventArgs args)
        {
            if (args.GameEvent.Name == "dota_tower_kill")
                // TODO: Test if own tower died and not enemy ones / maybe also for baracks
                _lastTowerKillTick = Environment.TickCount;
        }

        private static void Player_OnExecuteAction(Player sender, ExecuteOrderEventArgs args)
        {
            var shouldCheckForPowerTreads = false;
            switch (args.Order)
            {
                case Order.GlyphOfFortification:
                    // Test if tower just died a second ago and prevent glyph
                    if (_lastTowerKillTick + 1000 > Environment.TickCount)
                        args.Process = false;
                    break;
                case Order.AbilityTarget:
                {
                    if (_targetSpellsItem.GetValue<bool>())
                    {
                        var unit = args.Target as Unit;
                        if ((unit != null) && (args.Ability != null))
                            TargetSpellCheck(args);
                    }
                    shouldCheckForPowerTreads = args.Process;
                    break;
                }
                case Order.AbilityLocation:
                {
                    if (_areaSpellsItem.GetValue<bool>())
                        AreaSpellCheck(sender, args);
                    shouldCheckForPowerTreads = args.Process;
                    break;
                }
                case Order.Ability:
                {
                    if (_areaSpellsItem.GetValue<bool>())
                        AbilityCheck(sender, args);
                    shouldCheckForPowerTreads = args.Process;
                    break;
                }
            }
            if (shouldCheckForPowerTreads)
                PowerTreadsIntCheck(args);
        }

        private static void AbilityCheck(Player sender, ExecuteOrderEventArgs args)
        {
            // Check if we waste a no target spell
            var spell = NoTargetSpells.FirstOrDefault(x => x.Name == args.Ability.Name);
            if (spell != null)
            {
               
                _testExpr = GetExprFunc(sender, args, spell);

                Log.Debug($"Testing {spell.Name}");

                var enemies =
                    ObjectManager.GetEntities<Hero>().Where(_testExpr);
                if (!enemies.Any())
                {
                    args.Process = false;
                    NotifyPlayer("Stop failing", Game.Localize(args.Ability.Name));
                    return;
                }
                _testTime = Game.RawGameTime;
                _testAbility = args.Ability;
            }
        }

        private static void TargetSpellCheck(ExecuteOrderEventArgs args)
        {
            var hero = args.Target as Hero;
            if (hero != null)
                if (hero.IsIllusion)
                {
                    var realTarget = hero.ReplicateFrom;
                    if (realTarget.IsAlive && realTarget.IsVisible &&
                        (realTarget.Distance2D(args.Entities.First()) - realTarget.HullRadius < args.Ability.CastRange))
                    {
                        args.Ability.UseAbility(realTarget);
                        args.Process = false;
                        NotifyPlayer("Stop illusion failing", Game.Localize(args.Ability.Name));
                        return;
                    }
                }
            // Check if target is linkens protected for certain spells
            var unit = args.Target as Unit;
            if (LinkensSpells.Contains(args.Ability.Name))
            {
                // TODO: fix linkens check with cooldown testing?
                var linkensItem = unit.FindItem("item_sphere");
                var linkens = unit.HasModifier("modifier_item_sphere_target")
                              || ((linkensItem != null) && (linkensItem.Cooldown <= 0));
                if (linkens)
                {
                    args.Process = false;
                    NotifyPlayer("Stop linkens failing", Game.Localize(args.Ability.Name));
                }
            }
        }

  
        private static void AreaSpellCheck(Player sender, ExecuteOrderEventArgs args)
        {
            var spell = AreaSpells.FirstOrDefault(x => x.Name == args.Ability.Name);
            if (spell != null)
            {
                _testExpr = GetExprFunc(sender, args, spell);

                var enemies =
                    ObjectManager.GetEntities<Hero>().Where(_testExpr);
                if (!enemies.Any())
                {
                    args.Process = false;
                    NotifyPlayer("Stop failing", Game.Localize(args.Ability.Name));
                    return;
                }
                _testTime = Game.RawGameTime;
                _testAbility = args.Ability;
            }
        }

        private static void PowerTreadsIntCheck(ExecuteOrderEventArgs args)
        {
            if ((_powerTreadsFunc != null) || !_powerTreadsItem.GetValue<bool>())
                return;

            var powerTreads =
                ObjectManager.LocalHero.Inventory.Items.OfType<PowerTreads>()
                    .FirstOrDefault(x => x.ActiveAttribute != Attribute.Intelligence);
            if ((powerTreads == null) || (args.Ability == powerTreads))
                return;

            args.Process = false;
            // agility -> strength -> int
            var count = powerTreads.ActiveAttribute == Attribute.Agility ? 2 : 1;

            _powerTreadsFunc = async () =>
            {
                Log.Debug($"Calling async func {args.Ability.Name}");
                for (var i = 0; i < count; ++i)
                {
                    powerTreads.UseAbility();
                    await Await.Delay(25);
                    Log.Debug("toggling");
                }
                await Await.Delay((int) Game.Ping);
                switch (args.Order)
                {
                    case Order.AbilityTarget:
                    {
                        args.Ability.UseAbility(args.Target as Unit);
                        break;
                    }
                    case Order.AbilityLocation:
                    {
                        args.Ability.UseAbility(args.TargetPosition);
                        break;
                    }
                    case Order.Ability:
                    {
                        args.Ability.UseAbility();
                        break;
                    }
                }
                var delay = (int) (args.Ability.FindCastPoint()*1500.0f + Game.Ping);
                Log.Debug($"awaiting {delay}");
                await Await.Delay(delay);

                var owner = powerTreads.Owner as Unit;
                if ((owner != null && (owner.IsChanneling() || owner.IsInvisible())) || args.Ability.AbilityData2.ID == 5195 || args.Ability.AbilityData2.ID == 5598) // templar_assassin_meld legion_commander_duel
                {
                    Log.Debug("NOT toggling back due to channeling or invis");
                    _powerTreadsFunc = null;
                    return;
                }
                count = count == 2 ? 1 : 2;
                for (var i = 0; i < count; ++i)
                {
                    powerTreads.UseAbility();
                    await Await.Delay(25);
                    Log.Debug("toggling back");
                }
                _powerTreadsFunc = null;
            };

            Log.Debug($"toggling boots from {powerTreads.ActiveAttribute}");
        }

        private static void NotifyPlayer(string message, string ability)
        {
            if (_notifyItem.GetValue<bool>())
                Game.PrintMessage($"<font color='#FF6666'>{message}</font> <font color='#66B2FF'>{ability}</font>",
                    MessageType.LogMessage);
        }

        private static Func<Hero, bool> GetExprFunc(Player sender, ExecuteOrderEventArgs args, SpecialSpellInfo info)
        {
            var pos = args.Order == Order.AbilityTarget ? args.Target.Position : args.Ability.Owner.Position;
            switch (info.PiercesMagic)
            {
                case MagicPierce.No:
                    return
                        x => x.IsAlive && x.IsVisible && !x.IsIllusion && (x.Team != sender.Team) && !x.IsMagicImmune()
                             && (x.Distance2D(pos) - x.HullRadius
                                 < args.Ability.AbilitySpecialData.First(s => s.Name == info.Radius).Value);
                case MagicPierce.Yes:
                    return x => x.IsAlive && x.IsVisible && !x.IsIllusion && (x.Team != sender.Team)
                                && (x.Distance2D(pos) - x.HullRadius
                                    < args.Ability.AbilitySpecialData.First(s => s.Name == info.Radius).Value);
                case MagicPierce.Agha:
                    return
                        x =>
                            x.IsAlive && x.IsVisible && !x.IsIllusion && (x.Team != sender.Team) &&
                            (!x.IsMagicImmune() ||
                             ((Unit)args.Ability.Owner).HasItem(ClassID.CDOTA_Item_UltimateScepter))
                            && (x.Distance2D(pos) - x.HullRadius
                                < args.Ability.AbilitySpecialData.First(s => s.Name == info.Radius).Value);
                default:
                    throw new ArgumentOutOfRangeException(nameof(info.PiercesMagic), info.PiercesMagic, null);
            }
        }
    }
}