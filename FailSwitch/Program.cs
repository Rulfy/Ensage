using System;
using System.Linq;
using Ensage;
using Ensage.Common.Extensions;

namespace FailSwitch
{
    using Ensage.Common.Extensions;

    using Attribute = Ensage.Attribute;

    class Program
    {
        // TODO: if preventing a fail action -> notify the player!
        private static int _lastTowerKillTick = 0;
        private static int _powerTreadsRestore = 0;
        static void Main(string[] args)
        {
            Player.OnExecuteOrder += Player_OnExecuteAction;
            Game.OnFireEvent += Game_OnGameEvent;
        }

        static void Game_OnGameEvent(FireEventEventArgs args)
        {
            if (args.GameEvent.Name == "dota_tower_kill") // TODO: Test if own tower died and not enemy ones / maybe also for baracks
                _lastTowerKillTick = Environment.TickCount;
            Console.WriteLine(args.GameEvent.Name);
        }

        static void Player_OnExecuteAction(Player sender, ExecuteOrderEventArgs args)
        {
            _powerTreadsRestore = 0;
            switch (args.Order)
            {
                case Order.GlyphOfFortification:
                    // Test if tower just died a second ago and prevent glyph
                    if (_lastTowerKillTick + 1000 < Environment.TickCount)
                        args.Process = false;
                    break;
                case Order.AbilityTarget:
                    {
                        var unit = args.Target as Unit;
                        if (unit != null && args.Ability != null)
                            TargetSpellCheck(sender, args);
                        break;
                    }
                case Order.AbilityLocation:
                    {
                        AreaSpellCheck(sender, args);
                        break;
                    }
                case Order.Ability:
                    {
                        AbilityCheck(sender, args);
                        break;
                    }
            }
            if (_powerTreadsRestore != 0)
            {
                var powerTreads =
                    (Ensage.Items.PowerTreads)ObjectMgr.LocalHero.Inventory.Items.FirstOrDefault(x => x is Ensage.Items.PowerTreads);
                for (var i = 0; i < _powerTreadsRestore; ++i)
                    powerTreads.ToggleAbility(true);
            }
        }

        private static void AbilityCheck(Player sender, ExecuteOrderEventArgs args)
        {
            // Check if we waste a no target spell
            if (args.Ability.Name == "magnataur_reverse_polarity")
            {
                var enemies =
                    ObjectMgr.GetEntities<Hero>()
                        .Where(
                            x =>
                                x.IsAlive && x.IsVisible && !x.IsIllusion && x.Team != sender.Team &&
                                x.Distance2D(args.Entities.First()) - x.HullRadius < args.Ability.CastRange);
                if (!enemies.Any())
                {
                    args.Process = false;
                    return;
                }
            }
            else if (args.Ability.Name == "tidehunter_ravage")
            {
                var enemies =
                    ObjectMgr.GetEntities<Hero>()
                        .Where(
                            x =>
                                x.IsAlive && x.IsVisible && !x.IsIllusion && x.Team != sender.Team &&
                                x.Distance2D(args.Entities.First()) - x.HullRadius < args.Ability.AbilityData.First(s => s.Name == "radius").Value);
                if (!enemies.Any())
                {
                    args.Process = false;
                    return;
                }
            }
            if (PowerTreadsIntCheck(args))
                args.Ability.UseAbility(true);
        }

        static void TargetSpellCheck(Player sender, ExecuteOrderEventArgs args)
        {
            var hero = args.Target as Hero;
            if (hero != null)
            {
                // Check if target is illusion and real hero is near
                if (hero.IsIllusion)
                {
                    var realTarget = hero.ReplicateFrom;
                    if (realTarget.IsAlive && realTarget.IsVisible && realTarget.Distance2D(args.Entities.First()) - realTarget.HullRadius < args.Ability.CastRange)
                    {
                        args.Ability.UseAbility(realTarget);
                        args.Process = false;
                        return;
                    }
                }
            }
            // Check if target is linkens protected for certain spells
            var unit = args.Target as Unit;
            var noLinkenSpells = new[]
            {
                "doom_bringer_doom", "lion_finger_of_death", "lina_laguna_blade", "juggernaut_omni_slash", "antimage_mana_void", "legion_commander_duel",
                "bloodseeker_rupture", "terrorblade_sunder", "furion_wrath_of_nature", "bane_fiends_grip", "lich_chain_frost",
            };
            if (noLinkenSpells.Contains(args.Ability.Name))
            {
                // TODO: fix linkens check with cooldown testing?
                var linkensItem = unit.FindItem("item_sphere");
                var linkens = unit.Modifiers.Any(x => x.Name == "modifier_item_sphere_target")
                              || (linkensItem != null && linkensItem.Cooldown <= 0);
                if (linkens)
                {
                    args.Process = false;
                    return;
                }
            }
            if (PowerTreadsIntCheck(args))
                args.Ability.UseAbility(unit, true);
        }


        private class AreaSpell
        {
            public AreaSpell(string name, string radius)
            {
                Name = name;
                Radius = radius;
            }
            public string Name { get; private set; }
            public string Radius { get; private set; }
        }
        static void AreaSpellCheck(Player sender, ExecuteOrderEventArgs args)
        {
            var spells = new[]
            {
                new AreaSpell("enigma_black_hole", "pull_radius"),
                new AreaSpell("puck_dream_coil", "coil_radius"),
                new AreaSpell("obsidian_destroyer_sanity_eclipse", "radius"),
                new AreaSpell("faceless_void_chronosphere", "radius"),

            };
            var spell = spells.FirstOrDefault(x => x.Name == args.Ability.Name);
            if (spell != null)
            {
                var enemies =
                    ObjectMgr.GetEntities<Hero>()
                        .Where(
                            x =>
                            x.IsAlive && x.IsVisible && !x.IsIllusion && x.Team != sender.Team
                            && x.Distance2D(args.TargetPosition) - x.HullRadius
                            < args.Ability.AbilityData.First(s => s.Name == spell.Radius).Value);
                if (!enemies.Any())
                {
                    args.Process = false;
                    return;
                }
            }
            if (PowerTreadsIntCheck(args))
                args.Ability.UseAbility(args.TargetPosition, true);
        }

        static bool PowerTreadsIntCheck(ExecuteOrderEventArgs args)
        {
            return false;
            //var powerTreads = ObjectMgr.LocalHero.FindItem("item_power_treads");
            //if (powerTreads == null)
            //    return false;

            //// agility -> strength -> int
            //switch (((Ensage.Items.PowerTreads)powerTreads).ActiveAttribute)
            //{
            //    case Attribute.Agility:
            //        powerTreads.ToggleAbility();
            //        powerTreads.ToggleAbility(true);
            //        _powerTreadsRestore = 1;
            //        args.Process = false;
            //        break;
            //    case Attribute.Strength:
            //        powerTreads.ToggleAbility();
            //        _powerTreadsRestore = 2;
            //        args.Process = false;
            //        break;
            //    //case Attribute.Intelligence:
            //    //    args.Process = true;
            //    //    _powerTreadsRestore = 0;
            //    //    break;
            //}
            //Console.WriteLine(_powerTreadsRestore);
            //return args.Process;
        }
    }
}
