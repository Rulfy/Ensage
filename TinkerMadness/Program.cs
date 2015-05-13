using System;
using System.Linq;
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
        static void Main(string[] args)
        {
            Entity.OnIntegerPropertyChange += Entity_OnIntegerPropertyChange;
            Game.OnUpdate += ComboTick;
            Game.OnWndProc += Game_OnWndProc;
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
            }
        }

        static void Game_OnWndProc(WndEventArgs args)
        {
            if (!_activated || args.Msg != WM_KEYUP || args.WParam != 'O' || Game.IsChatOpen || !Game.IsInGame)
                return;

            // disable
            if (_target != null)
            {
                _target = null;
                return;
            }

            _target = GetClosestEnemyHeroToMouse();
        }

        static void ComboTick(EventArgs args)
        {
            if (!_activated || !Game.IsInGame || Game.IsPaused || _target == null)
                return;

            var me = EntityList.Hero;
            // Check if we still got a valid target and we're alive
            if ( !_target.IsValid || !_target.IsAlive || !me.IsAlive || !_target.IsVisible || _target.UnitState.HasFlag(UnitState.MagicImmune))
            {
                _target = null;
                return;
            }

            if (!HasCombo())
                return;

            // Fetch our spells
            var Q = me.Spellbook.SpellQ;
            var W = me.Spellbook.SpellW;
            var R = me.Spellbook.SpellR;
            if (R.IsInAbilityPhase || R.IsChanneling)
                return;

            // Fetch our combo items
            var dagon = GetDagon();
            var blink = me.Inventory.Items.FirstOrDefault(x => x.Name == "item_blink");
            var ethereal = me.Inventory.Items.FirstOrDefault(x => x.Name == "item_ethereal_blade");
            var soulring = me.Inventory.Items.FirstOrDefault(x => x.Name == "item_soul_ring");
            var sheep = _target.ClassId == ClassId.CDOTA_Unit_Hero_Tidehunter ? null : me.Inventory.Items.FirstOrDefault(x => x.Name == "item_sheepstick");

            // Cast the queue


            // Test if we need our ulti to refresh cooldowns
            if ((sheep == null || sheep.Cooldown > 0) &&
                ((sheep != null && R.Level < 3) || Q.Cooldown > 0 || (dagon != null && dagon.Cooldown > 0) ||
                 (ethereal != null && ethereal.Cooldown > 0))
                && R.AbilityState == AbilityState.Ready)
            {
                // table.insert(castQueue,{1000+math.ceil(R:FindCastPoint()*1000),R})
                return;
            }


            // Check if target is too far away
            var minRange = long.MaxValue;
            if (Q.Level > 0)
                minRange = Math.Min(minRange, Q.CastRange);
            if( dagon != null )
                minRange = Math.Min(minRange, dagon.CastRange);
            if (ethereal != null)
                minRange = Math.Min(minRange, ethereal.CastRange);

            var distance = GetDistance2D(me.Position, _target.Position);
            var blinkRange = blink.AbilityData.FirstOrDefault(x => x.Name == "blink_range").Value;
            if (blinkRange + minRange < distance)
            {
                // Target too far TODO: status text
                return;
            }

            // Check if we need to blink to the enemy
            if (minRange < distance)
            {
                // Need to blink
                if (blink.Cooldown > 0 && R.AbilityState == AbilityState.Ready)
                {
                    // Cast ulti because blink is on cooldown
                    // table.insert(castQueue,{1000+math.ceil(R:FindCastPoint()*1000),R})
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
                    targetPosition = (targetPosition - me.Position)*(blinkRange - 100) / GetDistance2D(targetPosition, me.Position) + me.Position;

                var turn = (Math.Max(Math.Abs(FindAngleR(me) - DegreeToRadian(FindAngleBetween(me.Position, _target.Position))) - 0.69f, 0) / (0.6f * (1 / 0.03f))) * 1000.0f;
                // insert in queue
                // table.insert(castQueue,{math.ceil(blink:FindCastPoint()*1000 + turn),blink,tpos})

                /*
                 -- soul ring
table.insert(castQueue,{100,soulring})
-- now the rest of our combo: tp -> [[blink] -> sheep -> ethereal -> dagon -> W -> Q -> R
local linkens = target:IsLinkensProtected()
if linkens and dagon and dagon:CanBeCasted() then
table.insert(castQueue,{math.ceil(dagon:FindCastPoint()*1000),dagon,target,true})
end
if sheep and sheep:CanBeCasted() then
table.insert(castQueue,{math.ceil(sheep:FindCastPoint()*1000),sheep,target})
end
if ethereal and ethereal:CanBeCasted() and not linkens then
table.insert(castQueue,{math.ceil(ethereal:FindCastPoint()*1000 + ((GetDistance2D(tpos,target)-50)/1200)*1000 - dagon:FindCastPoint()*1000 - client.latency),"item_ethereal_blade",target})
elseif linkens then
table.insert(castQueue,{math.ceil(ethereal:FindCastPoint()*1000 + ((GetDistance2D(tpos,target)-50)/1200)*1000 - W:FindCastPoint()*1000 - ((GetDistance2D(tpos,target)-50)/900)*1000 - client.latency),"item_ethereal_blade",target})
end
if dagon and not linkens and dagon:CanBeCasted() then
table.insert(castQueue,{math.ceil(dagon:FindCastPoint()*1000),dagon,target})
end
if W.level > 0 and W:CanBeCasted() then
table.insert(castQueue,{100,W})
end
if Q.level > 0 and (not sheep or R.level == 3) and Q:CanBeCasted() then
table.insert(castQueue,{math.ceil(Q:FindCastPoint()*1000),Q,target})
end
casted = true*/
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
