using System;
using System.Linq;
using System.Windows.Forms;
using Ensage;
using SharpDX;

namespace TinkerMadness
{
    class Program
    {
        const int WM_KEYUP = 0x0101;
        const int WM_KEYDOWN = 0x0105;

        private static Hero _target;
        private static bool _activated;
        private readonly static Timer Timer = new Timer();
        
        static void Main(string[] args)
        {
            Timer.Tick += Timer_Tick;
            Entity.OnIntegerPropertyChange += Entity_OnIntegerPropertyChange;
            Game.OnUpdate += ComboTick;
            Game.OnWndProc += Game_OnWndProc;
        }

        static void Timer_Tick(object sender, EventArgs e)
        {
            Timer.Enabled = false;
        }

        /// <summary>
        /// Wait until we're ingame and picked a hero
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="args"></param>
        static void Entity_OnIntegerPropertyChange(Entity sender, EntityIntegerPropertyChangeEventArgs args)
        {
            // Equals to: if (args.Property == "m_nGameState" && (((GameState)args.NewValue) == GameState.Prestart || ((GameState)args.NewValue) == GameState.Started))
            if (args.Property == "m_nGameState" && Game.IsInGame ) 
            {
                var me = EntityList.Hero;
                _activated = me != null && me.ClassId == ClassId.CDOTA_Unit_Hero_Tinker;
                Console.WriteLine(_activated ? "Got tinker" : "Got the wrong hero");
            }
        }

        static void Game_OnWndProc(WndEventArgs args)
        {
            if (!_activated || args.Msg != WM_KEYUP || args.WParam != 'O' || Game.IsChatOpen || !Game.IsInGame )
                return;

            // disable
            if (_target != null)
            {
                _target = null;
                Console.WriteLine("Script disabled");
                return;
            }
            _target = GetClosestEnemyHeroToMouse();
        }

        static void ComboTick(EventArgs args)
        {
            if (!_activated || Timer.Enabled || _target == null || !Game.IsInGame || Game.IsPaused)
                return;

            var me = EntityList.Hero;
            // Check if we still got a valid target and we're alive
            if ( !_target.IsValid || !_target.IsAlive || !me.IsAlive || !_target.IsVisible || _target.UnitState.HasFlag(UnitState.MagicImmune))
            {
                _target = null;
                Console.WriteLine("Target or tinker dead");
                return;
            }

            if (!HasCombo())
                return;

            // Fetch our spells
            var qSpell = me.Spellbook.SpellQ;
            var wSpell = me.Spellbook.SpellW;
            var rSpell = me.Spellbook.SpellR;
            if (rSpell.IsInAbilityPhase || rSpell.IsChanneling)
                return;

            // Fetch our combo items
            var dagon = GetDagon();
            var blink = me.Inventory.Items.FirstOrDefault(x => x.Name == "item_blink");
            var ethereal = me.Inventory.Items.FirstOrDefault(x => x.Name == "item_ethereal_blade");
            var soulring = me.Inventory.Items.FirstOrDefault(x => x.Name == "item_soul_ring");
            var sheep = _target.ClassId == ClassId.CDOTA_Unit_Hero_Tidehunter ? null : me.Inventory.Items.FirstOrDefault(x => x.Name == "item_sheepstick");

            // Test if we need our ulti to refresh cooldowns
            if ((sheep == null || sheep.Cooldown > 0) &&
                ((sheep != null && rSpell.Level < 3) || qSpell.Cooldown > 0 || (dagon != null && dagon.Cooldown > 0) ||
                 (ethereal != null && ethereal.Cooldown > 0))
                && rSpell.AbilityState == AbilityState.Ready)
            {
                Timer.Start(1000 + Math.Ceiling(rSpell.GetCastPoint()*1000));
                rSpell.UseAbility();
                Console.WriteLine("Casting ult");
                return;
            }

            // Check if target is too far away
            var minRange = long.MaxValue;
            if (qSpell.Level > 0)
                minRange = Math.Min(minRange, qSpell.CastRange);
            if( dagon != null )
                minRange = Math.Min(minRange, dagon.CastRange);
            if (ethereal != null)
                minRange = Math.Min(minRange, ethereal.CastRange);

            var distance = GetDistance2D(me.Position, _target.Position);
            var blinkRange = blink.AbilityData.FirstOrDefault(x => x.Name == "blink_range").Value;
            if (blinkRange + minRange < distance)
            {
                // Target too far TODO: status text
                Console.WriteLine("Target too far away");
                return;
            }

            // Check if we need to blink to the enemy
            if (minRange < distance)
            {
                // Need to blink
                if (blink.Cooldown > 0 && rSpell.AbilityState == AbilityState.Ready)
                {
                    // Cast ulti because blink is on cooldown
                    Timer.Start(1000 + Math.Ceiling(rSpell.GetCastPoint()*1000));
                    rSpell.UseAbility();
                    Console.WriteLine("Casting ult");
                    return;
                }
                // Calculate blink position
                Vector3 targetPosition;
                if (distance > blinkRange)
                {
                    targetPosition = _target.Position - me.Position;
                    targetPosition /= targetPosition.Length();
                    targetPosition *= (distance - minRange*0.5f);
                    targetPosition += me.Position;
                }
                else
                {
                    targetPosition = _target.Position;
                }
                if (GetDistance2D(me.Position, targetPosition) > (blinkRange - 100))
                    targetPosition = (targetPosition - me.Position)*(blinkRange - 100)/
                                     GetDistance2D(targetPosition, me.Position) + me.Position;

                var turn =
                    (Math.Max(
                        Math.Abs(FindAngleR(me) - DegreeToRadian(FindAngleBetween(me.Position, _target.Position))) -
                        0.69f, 0)/(0.6f*(1/0.03f)))*1000.0f;
                // insert in queue
                Timer.Start(Math.Ceiling(blink.GetCastPoint()*1000 + turn));
                blink.UseAbility(targetPosition);
                Console.WriteLine("blinking to enemy");
                return;
            }
            var delay = 0.0;
            var casted = false;
            if (soulring != null && soulring.AbilityState == AbilityState.Ready)
            {
                soulring.UseAbility();
                casted = true;
                delay += 100;
            }
            var linkens = _target.Modifiers.Any(x => x.Name == "modifier_item_sphere_target") || _target.Inventory.Items.Any(x => x.Name == "item_sphere");
            // if the enemy has linkens, we should break it first with dagon
            if (linkens && dagon != null && dagon.AbilityState == AbilityState.Ready)
            {
                dagon.UseAbility(_target, casted);
                casted = true;
                delay += Math.Ceiling(dagon.GetCastPoint()*1000);
            }
            if (sheep != null && sheep.AbilityState == AbilityState.Ready)
            {
                sheep.UseAbility(_target, casted);
                casted = true;
                delay += Math.Ceiling(sheep.GetCastPoint() * 1000);
            }
            if (ethereal != null && ethereal.AbilityState == AbilityState.Ready)
            {
                ethereal.UseAbility(_target, casted);
                casted = true;
                delay += Math.Ceiling(ethereal.GetCastPoint() * 1000);
            }
            if( !linkens && dagon != null && dagon.AbilityState == AbilityState.Ready)
            {
                dagon.UseAbility(_target, casted);
                casted = true;
                delay += Math.Ceiling(dagon.GetCastPoint() * 1000);
            }
            if ( wSpell.Level > 0 && wSpell.AbilityState == AbilityState.Ready)
            {
                wSpell.UseAbility(casted);
                casted = true;
                delay += Math.Ceiling(wSpell.GetCastPoint() * 1000);
            }
            if (qSpell.Level > 0 && qSpell.AbilityState == AbilityState.Ready)
            {
                qSpell.UseAbility(_target, casted);
                casted = true;
                delay += Math.Ceiling(qSpell.GetCastPoint() * 1000);
            }
            if (casted)
            {
                Timer.Start(delay);
                Console.WriteLine("casting combo with total delay of: " + delay);
            }
        }

        static bool HasCombo()
        {
            var me = EntityList.GetLocalPlayer().Hero;
            if (me.Spellbook.Spells.Last().Level == 0)
                return false;

            // item_blink, item_sheepstick
            var items = me.Inventory.Items.ToList();
            return items.Any(x => x.Name == "item_blink") && items.Any(x => x.Name == "item_sheepstick");
        }

        static Item GetDagon()
        {
            return EntityList.GetLocalPlayer().Hero.Inventory.Items.ToList().FirstOrDefault(x => x.Name.Substring(0,10) == "item_dagon");
        }

        static Hero GetClosestEnemyHeroToMouse()
        {
            var mousePosition = Game.MousePosition;
            var enemies = EntityList.GetEntities<Hero>().Where(x => x.IsVisible && x.IsAlive && !x.IsIllusion && x.Team != EntityList.Player.Team && !x.UnitState.HasFlag(UnitState.MagicImmune)).ToList();

            var minimumDistance = float.MaxValue;
            Hero result = null;
            foreach (var hero in enemies)
            {
                var distance = Vector3.DistanceSquared(mousePosition, hero.Position);
                if (result == null || distance < minimumDistance)
                {
                    minimumDistance = distance;
                    result = hero;
                }
            }
            return result;
        }

        static float FindAngleR(Entity ent)
        {
            return (float)(ent.RotationRad < 0 ? Math.Abs(ent.RotationRad) : 2*Math.PI - ent.RotationRad);
        }

        static float FindAngleBetween(Vector3 first, Vector3 second)
        {
            var xAngle = (float)(Math.Atan(Math.Abs(second.X - first.X) / Math.Abs(second.Y - first.Y)) * (180.0 / Math.PI));
            if (first.X <= second.X && first.Y >= second.Y)
                return 90 - xAngle;
            if (first.X >= second.X && first.Y >= second.Y)
                return xAngle + 90;
            if (first.X >= second.X && first.Y <= second.Y)
                return 90 - xAngle + 180;
            if (first.X <= second.X && first.Y <= second.Y)
                return xAngle + 90 + 180;
            return 0;
        }

        static float GetDistance2D(Vector3 p1, Vector3 p2)
        {
            return (float) Math.Sqrt(Math.Pow(p1.X - p2.X, 2) + Math.Pow(p1.Y - p2.Y, 2));
        }

        static double DegreeToRadian(double angle)
        {
            return Math.PI * angle / 180.0;
        }
    }
}
