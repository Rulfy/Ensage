using System;
using Ensage.Common.Menu;

namespace IllusionSplitter
{
    public class AssemblyMenu
    {
        // main
        private static Menu _menu;

        // options
        private static MenuItem _angleRandomizer;
        private static MenuItem _moveHero;
        private static MenuItem _minMoveRange;
        private static MenuItem _useSpells;
        private static MenuItem _useItems;
        private static MenuItem _illuRange;

        // hotkeys
        private static MenuItem _splitterHotkeyItem;

        // public getters
        public static bool ShouldRandomizeAngle => _angleRandomizer.GetValue<bool>();
        public static bool ShouldMoveHero => _moveHero.GetValue<bool>();
        public static bool ShouldUseSpells => _useSpells.GetValue<bool>();
        public static bool ShouldUseItems => _useItems.GetValue<bool>();
        public static int IllusionRange => _illuRange.GetValue<Slider>().Value;
        public static int MinimumMoveRange => _minMoveRange.GetValue<Slider>().Value;

        public static event EventHandler SplitterHotkeyPressed;


        public static bool BuildMenu()
        {
            if (_menu != null)
                return false;

            _menu = new Menu("Illusion Splitter", "illuSplitter", true);

            // options
            _angleRandomizer = new MenuItem("angleRandom", "Randomize Split Angle").SetValue(true);
            _angleRandomizer.Tooltip = "Randomizes the split angle for illusions.";
            _menu.AddItem(_angleRandomizer);

            _moveHero = new MenuItem("moveHero", "Move Hero").SetValue(true);
            _moveHero.Tooltip = "Moves your hero to your mouse position, after pressing the split hotkey.";
            _menu.AddItem(_moveHero);

            _minMoveRange = new MenuItem("minMoveRange", "Minimum Move Range").SetValue(new Slider(800, 100, 2000));
            _minMoveRange.Tooltip = "Minimum range to move the illusions.";
            _menu.AddItem(_minMoveRange);

            _useSpells = new MenuItem("useSpells", "Use Spells").SetValue(true);
            _useSpells.Tooltip = "Uses your spells to create illusions before splitting them.";
            _menu.AddItem(_useSpells);

            _useItems = new MenuItem("useItems", "Move Items").SetValue(true);
            _useItems.Tooltip = "Uses your items to create illusions before splitting them.";
            _menu.AddItem(_useItems);

            _illuRange = new MenuItem("illuRange", "Illusion Range").SetValue(new Slider(600,100,2000));
            _illuRange.Tooltip = "The range to find illusions near your hero.";
            _menu.AddItem(_illuRange);

            // hotkeys 
            _splitterHotkeyItem = new MenuItem("splitKey", "Split Illusions").SetValue(new KeyBind(0, KeyBindType.Press));
            _splitterHotkeyItem.Tooltip = "Splits all current illusions near your hero.";
            _splitterHotkeyItem.ValueChanged += _splitterHotkeyItem_ValueChanged;
            _menu.AddItem(_splitterHotkeyItem);


            _menu.AddToMainMenu();
            return true;
        }

        private static void _splitterHotkeyItem_ValueChanged(object sender, OnValueChangeEventArgs e)
        {
            if ((e.GetNewValue<KeyBind>().Key == e.GetOldValue<KeyBind>().Key) && e.GetNewValue<KeyBind>().Active)
            {
                SplitterHotkeyPressed?.Invoke(null, EventArgs.Empty);
            }
        }
    }
}
