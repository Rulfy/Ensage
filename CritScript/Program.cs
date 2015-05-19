using System;
using System.Linq;
using Ensage;

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
                        me.Attack(Game.MousePosition);
                }
            }
        }
    }
}
