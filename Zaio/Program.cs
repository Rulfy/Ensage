using System;
using System.Linq;
using System.Reflection;
using Ensage;
using Ensage.Common;
using Ensage.Common.Threading;
using log4net;
using PlaySharp.Toolkit.Logging;
using Zaio.Helpers;
using Zaio.Interfaces;

namespace Zaio
{
    class Program
    {
        private static readonly ILog Log = AssemblyLogs.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private static ComboHero _currentHero;

        static void Main()
        {
            Events.OnLoad += Events_OnLoad;
            Events.OnClose += Events_OnClose;
            Drawing.OnDraw += Drawing_OnDraw;
            //Game.OnIngameUpdate += Game_OnIngameUpdate;
        }

        private static void Game_OnIngameUpdate(EventArgs args)
        {
            var hero = ObjectManager.LocalHero;
            if (hero == null)
                return;

            foreach (var heroModifier in hero.Modifiers)
            {
                Console.WriteLine(heroModifier.Name);
            }
        }

        private static void Drawing_OnDraw(EventArgs args)
        {
            _currentHero?.OnDraw();
        }

        private static void Events_OnClose(object sender, EventArgs e)
        {
            ZaioMenu.ResetHeroSettings();

            if (_currentHero == null)
            {
                return;
            }
            _currentHero.OnClose();
            _currentHero.Deactivate();
            _currentHero = null;
        }

        private static void Events_OnLoad(object sender, EventArgs e)
        {
            ZaioMenu.OnLoad();

            var hero = ObjectManager.LocalHero;
            if (hero != null)
            {
                var types = Assembly.GetExecutingAssembly().GetTypes().Where( x => x.Namespace == "Zaio.Heroes");
                foreach (var type in types)
                {
                    var property = type.GetCustomAttribute<HeroAttribute>();
                    if (property != null)
                    {
                        Log.Debug($"OnLoad ID: {property.Id}");
                        if (hero.ClassID == property.Id)
                        {
                            Log.Debug($"Found hero.. activating!");
                            _currentHero = (ComboHero) Activator.CreateInstance(type);
                            _currentHero.OnLoad();
                            _currentHero.Activate();
                            return;
                        }
                    }
                }
                Game.PrintMessage($"Zaio: <font color='#FF1133'>{Game.Localize(hero.Name)} is not supported! ({hero.ClassID})</font>");
            }
        }
    }
}
