using System;
using System.Collections.Generic;
using System.Linq;
using Ensage;
using Ensage.Common;
using Ensage.Common.Menu;
using SharpDX;

namespace TakeItToTheGrave
{
    class Program
    {
        private static bool _dropItems;
        private static readonly Menu Menu = new Menu("Take it to the grave", "grave", true);
        private static MenuItem _itemToggleState;

        static void Main(string[] args)
        {
            var hotkey = new MenuItem("destroyKey", "Press to destroy").SetValue(new KeyBind(0, KeyBindType.Press));
            hotkey.Tooltip = "!Won't work if you are stunned or disabled!";
            hotkey.TooltipColor = Color.Red;
            hotkey.ValueChanged += Hotkey_ValueChanged;
            Menu.AddItem(hotkey);

            var itemsToggle = new Dictionary<string, bool> { { "item_gem", true }, { "item_rapier", true } };
            _itemToggleState = new MenuItem("enabledDestroyToggle", "Toggle destroy:").SetValue(new AbilityToggler(itemsToggle));
            Menu.AddItem(_itemToggleState);

            Menu.AddToMainMenu();

            Game.OnIngameUpdate += Game_OnIngameUpdate;
        }

        private static void Hotkey_ValueChanged(object sender, OnValueChangeEventArgs e)
        {
            if (e.GetNewValue<KeyBind>().Active)
                _dropItems = true;
        }

        private static void Game_OnIngameUpdate(EventArgs args)
        {
            if (_dropItems && Utils.SleepCheck("destroyDelay"))
            {
                var hero = ObjectManager.LocalHero;
                if (hero == null)
                {
                    _dropItems = false;
                    return;
                }

                var items = hero.Inventory.Items.Where(x =>
                    (_itemToggleState.GetValue<AbilityToggler>().IsEnabled("item_gem") && x.ClassID == ClassID.CDOTA_Item_GemOfTrueSight)
                    || (_itemToggleState.GetValue<AbilityToggler>().IsEnabled("item_rapier") && x.Name == "item_rapier") ).ToList();
                if (items.Any())
                {
                    items.First().DestroyItem();
                    for (int i = 1; i < items.Count; ++i)
                    {
                        items[i].DestroyItem(true);
                    }
                    Utils.Sleep(125, "destroyDelay");
                }
                else
                {
                    _dropItems = false;
                }
            }
        }
    }
}
