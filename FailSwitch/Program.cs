using System;
using System.Linq;
using Ensage;
using Ensage.Common.Extensions;

namespace FailSwitch
{
    class Program
    {
        // TODO: if preventing a fail action -> notify the player!

        private static int _lastTowerKillTick = 0;
        static void Main(string[] args)
        {
            Player.OnExecuteAction += Player_OnExecuteAction;
            Game.OnGameEvent += Game_OnGameEvent;
        }

        static void Game_OnGameEvent(GameEventEventArgs args)
        {
            if (args.GameEvent.Name == "dota_tower_kill") // TODO: Test if own tower died and not enemy ones / maybe also for baracks
                _lastTowerKillTick = Environment.TickCount;
        }

        static void Player_OnExecuteAction(Player sender, ExecuteActionEventArgs args)
        {
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
                    if( unit != null && args.Ability != null)
                        TargetSpellCheck(args);
                    break;
                }
                case Order.AbilityLocation:
                {
                    AreaSpellCheck(args);
                    break;
                }
                case Order.Ability:
                {
                    AbilityCheck(args);
                    break;
                }
            }
        }

        private static void AbilityCheck(ExecuteActionEventArgs args)
        {
            // Check if we waste a no target spell
            if (args.Ability.Name == "magnataur_reverse_polarity")
            {
                var enemies =
                    EntityList.GetEntities<Hero>()
                        .Where(
                            x =>
                                x.IsAlive && x.IsVisible && !x.IsIllusion &&
                                x.GetDistance2D(args.Entities.First()) < args.Ability.CastRange);
                if (!enemies.Any())
                {
                    args.Process = false;
                    return;
                }
            }
            else if (args.Ability.Name == "tidehunter_ravage")
            {
                var enemies =
                    EntityList.GetEntities<Hero>()
                        .Where(
                            x =>
                                x.IsAlive && x.IsVisible && !x.IsIllusion &&
                                x.GetDistance2D(args.Entities.First()) < args.Ability.AbilityData.First(s => s.Name == "radius").Value);
                if (!enemies.Any())
                {
                    args.Process = false;
                    return;
                }
            }
        }

        static void TargetSpellCheck(ExecuteActionEventArgs args)
        {
            var hero = args.Target as Hero;
            if (hero != null)
            {
                // Check if target is illusion and real hero is near
                if (hero.IsIllusion)
                {
                    var realTarget = hero.ReplicateFrom;
                    if (realTarget.IsAlive && realTarget.IsVisible && realTarget.GetDistance2D(args.Entities.First()) < args.Ability.CastRange)
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
                var linkens = unit.Modifiers.Any(x => x.Name == "modifier_item_sphere_target") ||
                              unit.Inventory.Items.Any(x => x.Name == "item_sphere");
                if (linkens)
                {
                    args.Process = false;
                    return;
                }
            }
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
        static void AreaSpellCheck(ExecuteActionEventArgs args)
        {
            var spells = new[]
            {
                new AreaSpell("enigma_black_hole", "pull_radius"),
                new AreaSpell("puck_dream_coil", "coil_radius"),
                new AreaSpell("obsidian_destroyer_sanity_eclipse", "radius"),
                new AreaSpell("faceless_void_chronosphere", "radius"),

            };
            var spell = spells.FirstOrDefault(x => x.Name == args.Ability.Name);
            if (spell == null)
                return;

            var enemies =
                        EntityList.GetEntities<Hero>()
                            .Where(
                                x =>
                                    x.IsAlive && x.IsVisible && !x.IsIllusion &&
                                    x.GetDistance2D(args.TargetPosition) < args.Ability.AbilityData.First(s => s.Name == spell.Radius).Value);
            if (!enemies.Any())
            {
                args.Process = false;
                return;
            }
        }
    }
}
