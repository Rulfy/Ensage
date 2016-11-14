using System;
using Ensage;
using Ensage.Common;

namespace InvokerReborn
{
    internal class Program
    {
        private static Core _core;

        private static void Main(string[] args)
        {
            Events.OnLoad += Events_OnLoad;
        }

        private static void Game_OnIngameUpdate(EventArgs args)
        {
            Console.WriteLine(Game.Ping);
        }

        private static void Events_OnLoad(object sender, EventArgs e)
        {
            var hero = ObjectManager.LocalHero;
            if ((hero == null) || (hero.ClassID != ClassID.CDOTA_Unit_Hero_Invoker))
                return;

            _core = new Core();
        }
    }
}