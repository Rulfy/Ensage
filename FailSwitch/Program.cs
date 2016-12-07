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
using Attribute = Ensage.Attribute;

namespace FailSwitch
{
    internal class AreaSpell
    {
        public AreaSpell(string name, string radius)
        {
            Name = name;
            Radius = radius;
        }

        public string Name { get; }
        public string Radius { get; }
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

        private static readonly List<AreaSpell> AreaSpells = new List<AreaSpell>
        {
            new AreaSpell("enigma_black_hole", "pull_radius"),
            new AreaSpell("puck_dream_coil", "coil_radius"),
            new AreaSpell("obsidian_destroyer_sanity_eclipse", "radius"),
            new AreaSpell("faceless_void_chronosphere", "radius")
        };

        private static readonly string[] LinkensSpells =
        {
            "doom_bringer_doom", "lion_finger_of_death", "lina_laguna_blade", "juggernaut_omni_slash",
            "antimage_mana_void", "legion_commander_duel",
            "bloodseeker_rupture", "terrorblade_sunder", "furion_wrath_of_nature", "bane_fiends_grip",
            "lich_chain_frost"
        };

        private static Ability _testAbility;
        private static float _testTime;
        private static Func<Hero, bool> _testExpr;

        private static void Main()
        {
            Events.OnLoad += Events_OnLoad;
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
            GameDispatcher.OnIngameUpdate += GameDispatcher_PowerTreads;
        }

#pragma warning disable CS1998 // Async method lacks 'await' operators and will run synchronously
        private static async void GameDispatcher_PowerTreads(EventArgs args)
#pragma warning restore CS1998 // Async method lacks 'await' operators and will run synchronously
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
            if (args.Ability.Name == "magnataur_reverse_polarity")
            {
                _testExpr = x =>
                    x.IsAlive && x.IsVisible && !x.IsIllusion && (x.Team != sender.Team) &&
                    (x.Distance2D(args.Entities.First()) - x.HullRadius < args.Ability.CastRange);

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
            else if (args.Ability.Name == "tidehunter_ravage")
            {
                _testExpr = x =>
                    x.IsAlive && x.IsVisible && !x.IsIllusion && (x.Team != sender.Team) &&
                    !x.IsMagicImmune() &&
                    (x.Distance2D(args.Entities.First()) - x.HullRadius <
                     args.Ability.AbilitySpecialData.First(s => s.Name == "radius").Value);
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
                _testExpr = x =>
                    x.IsAlive && x.IsVisible && !x.IsIllusion && (x.Team != sender.Team)
                    && (x.Distance2D(args.TargetPosition) - x.HullRadius
                        < args.Ability.AbilitySpecialData.First(s => s.Name == spell.Radius).Value);
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
                if (owner != null && owner.IsChanneling())
                {
                    Log.Debug("NOT toggling back due to channeling");
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
                Game.PrintMessage($"<font color='#FF6666'>{message}</font> <font color='#66B2FF'>{ability}</font>", MessageType.LogMessage);
        }
    }
}