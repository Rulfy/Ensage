namespace InvokerReborn
{
    using System;

    using Ensage;
    using Ensage.Common;

    internal class Program
    {
        private static Core core;

        private static void Events_OnLoad(object sender, EventArgs e)
        {
            var hero = ObjectManager.LocalHero;
            if ((hero == null) || (hero.ClassID != ClassID.CDOTA_Unit_Hero_Invoker))
            {
                return;
            }

            core = new Core();
        }

        private static void Main()
        {
            Events.OnLoad += Events_OnLoad;
        }
    }
}