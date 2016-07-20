using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Evade
{
    using Ensage;
    using Ensage.Common.Menu;

    class Program
    {
        private static Evade evade = null;

        public static EvadeMenu Menu;

        static void Main(string[] args)
        {
            Menu = new EvadeMenu(); 

            Ensage.Common.Events.OnLoad += Events_OnLoad;
            Ensage.Common.Events.OnClose += Events_OnClose;


            //foreach (var hero   in ObjectManager.GetEntities<Hero>())
            //{
            //    Console.WriteLine("{0}:",hero.Name);
            //    foreach (var spell in hero.Spellbook.Spells)
            //    {
            //        Console.WriteLine("{0} ({1})",spell.Name,spell.ClassID);
            //    }
            //    Console.WriteLine();
            //}

            //foreach (var hero in ObjectManager.GetEntities<Building>().Where(x=>x.ClassID == ClassID.CDOTA_Unit_Fountain))
        }

        private static void Events_OnClose(object sender, EventArgs e)
        {
            evade = null;
        }

        private static void Events_OnLoad(object sender, EventArgs e)
        {
            evade = new Evade();
        }
    }
}
