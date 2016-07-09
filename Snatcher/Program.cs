using System;
using System.Linq;
using Ensage;
using Ensage.Common.Menu;

namespace Snatcher
{
    using Ensage.Common.Extensions;

    class Program
    {
        private static int sleeptick;
        private static readonly Menu Menu = new Menu("Snatcher", "snatcher", true);

        static void Main(string[] args)
        {
            var hotkey = new MenuItem("hotkey", "Toggle hotkey").SetValue(
                new KeyBind('P', KeyBindType.Toggle));
            Menu.AddItem(hotkey);

            Menu.AddItem(new MenuItem("aegis", "Grab aegis").SetValue(true));
            Menu.AddItem(new MenuItem("cheese", "Grab cheese").SetValue(true));
            Menu.AddItem(new MenuItem("rune", "Grab rune").SetValue(true));
            
           
            Menu.AddToMainMenu();
        
            Game.OnIngameUpdate += Game_OnUpdate;
        }

        private static void Game_OnUpdate(EventArgs args)
        {
            var tick = Environment.TickCount;
            var hero = ObjectMgr.LocalHero;
            if (hero == null || tick < sleeptick || !Menu.Item("hotkey").GetValue<KeyBind>().Active)
                return;
            // check for runes
            if (Menu.Item("rune").GetValue<bool>())
            {
                var runes =
                    ObjectMgr.GetEntities<Rune>()
                        .Where(x => x.IsVisible && x.Distance2D(hero) < 400)
                        .ToList();
                if (runes.Any())
                {
                    hero.PickUpRune(runes.First());
                    sleeptick = tick + 125;
                    return;
                }
            }
            // check for aegis and cheese
            if (hero.Inventory.FreeSlots.Any())
            {
                var aegis = Menu.Item("aegis").GetValue<bool>();
                var cheese = Menu.Item("cheese").GetValue<bool>();
                if (!aegis && !cheese)
                    return;

                var items =
                    ObjectMgr.GetEntities<PhysicalItem>()
                        .Where(x => x.IsVisible && x.Distance2D(hero) < 400
                                    && ((aegis && x.Item.Name == "item_aegis") || (cheese && x.Item.Name == "item_cheese"))).ToList();
                if (items.Any())
                {
                    hero.PickUpItem(items.First());
                    sleeptick = tick + 125;
                }
            }
        }
    }
}
