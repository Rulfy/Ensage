using System;
using System.Linq;
using Ensage;
using SharpDX;

namespace CritScript
{
    class Program
    {
        /// <summary>
        /// Enabled for the current game? Are we ingame and have the correct hero?
        /// </summary>
        private static bool _activated;
        /// <summary>
        /// Currently toggled on or off by hotkey?
        /// </summary>
        private static bool _enabled;

        private const int HeroDistance = 500;

        private static readonly ClassId[] ValidHeroes = {
            ClassId.CDOTA_Unit_Hero_Juggernaut,
            ClassId.CDOTA_Unit_Hero_PhantomAssassin,
            ClassId.CDOTA_Unit_Hero_Brewmaster,
            ClassId.CDOTA_Unit_Hero_SkeletonKing,
            ClassId.CDOTA_Unit_Hero_ChaosKnight
        };
        const int WM_KEYUP = 0x0101;
        const int WM_KEYDOWN = 0x0105;

        static void Main(string[] args)
        {
            Entity.OnIntegerPropertyChange += Entity_OnIntegerPropertyChange;
            Game.OnWndProc += Game_OnWndProc;
            if (Game.IsInGame)
                _activated = CorrectHero();
        }

        static void Game_OnWndProc(WndEventArgs args)
        {
            if (!_activated || args.Msg != WM_KEYUP || args.WParam != 'F' || Game.IsChatOpen || !Game.IsInGame)
                return;

            _enabled = !_enabled;
            Console.WriteLine("Crit switched to "+ (_enabled ? "on" : "off"));
        }

        static bool CorrectHero()
        {
            var me = EntityList.Hero;
            return me != null && ValidHeroes.Contains(me.ClassId);
        }

        static void Entity_OnIntegerPropertyChange(Entity sender, EntityIntegerPropertyChangeEventArgs args)
        {
            if (args.Property == "m_nGameState")
            {
                _activated = Game.IsInGame;

                if (!_activated)
                    return;

                _activated = CorrectHero();
            }
            if (_enabled && _activated)
            {
                var me = EntityList.Hero;
                if (sender != null && me.Equals(sender) && args.Property == "m_NetworkActivity")
                {

                    if (args.NewValue == 424)
                        me.Hold();
                    else if (args.NewValue == 419)
                    {
                        var target = GetClosestEnemyHeroToMouse();
                        if (target == null)
                            me.Attack(Game.MousePosition);
                        else
                            me.Attack(target);
                    }
                }
            }
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
            return minimumDistance > HeroDistance ? null : result;
        }
    }
}
