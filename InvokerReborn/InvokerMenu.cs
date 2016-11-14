using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Input;
using Ensage.Common.Menu;
using Ensage.Common.Menu.MenuItems;
using PlaySharp.Toolkit.EventAggregator;

namespace InvokerReborn
{
    public class InvokerMenu
    {
        public const string SmartComboName = "smartCombo";
        public const string ComboName = "combo";

        private static readonly List<string> ComboNames = new List<string>
        {
            "AlaForSnap",
            "EulsSSMeteorBlast",
            "Assassination"
        };

        private static readonly List<List<string>> ComboPictures = new List<List<string>>
        {
            new List<string> {"invoker_alacrity", "invoker_forge_spirit", "cold_snap"},
            new List<string>
            {
                "item_cyclone",
                "invoker_sun_strike",
                "invoker_chaos_meteor",
                "invoker_deafening_blast",
                "invoker_cold_snap",
                "invoker_forge_spirit"
            },
            new List<string>
            {
                "invoker_tornado",
                "invoker_sun_strike",
                "invoker_chaos_meteor",
                "invoker_deafening_blast",
                "item_refresher",
                "invoker_chaos_meteor1",
                "invoker_sun_strike1"
            }
        };

        // main
        private static Menu _menu;

        // general
        private static MenuItem _safeDistanceItem;
        private static MenuItem _maxWalkDistance;
        private static MenuItem _moveTimeoutItem;

        // combo
        private static StringSwitcher _comboListItem;
        private static MenuItem _comboPicturesItem;

        // hotkeys
        private static MenuItem _prepareCombo;

        private static MenuItem _combo;
        private static MenuItem _smartCombo;

        // events
        public static IEventAggregator EventAggregator { get; private set; }

        public static bool IsPrepareKeyPressed => _prepareCombo.GetValue<KeyBind>().Active;
        public static int SafeDistance => _safeDistanceItem.GetValue<Slider>().Value;
        public static int MaxWalkDistance => _maxWalkDistance.GetValue<Slider>().Value;
        public static int MoveTimeout => _moveTimeoutItem.GetValue<Slider>().Value;

        public static Key SmartComboKey => KeyInterop.KeyFromVirtualKey((int) _smartCombo.GetValue<KeyBind>().Key);
        public static Key ComboKey => KeyInterop.KeyFromVirtualKey((int) _combo.GetValue<KeyBind>().Key);

        public static string CurrentlyActiveCombo => _comboListItem.GetValue<StringList>().SelectedValue;

        // events
        public static event EventHandler<StringEventArgs> ActiveComboChanged;

        public static void BuildMenu()
        {
            if (_menu != null)
                return;

            foreach (var comboPicture in ComboPictures)
                comboPicture.Reverse();

            EventAggregator = new EventAggregator();
            _menu = new Menu("Invoker Reborn", "invreb", true, "npc_dota_hero_invoker", true);

            // general
            var generalMenu = new Menu("General", "generalMenu");

            _moveTimeoutItem = new MenuItem("moveTimeout", "Reach Timeout [ms]").SetValue(new Slider(2000, 500, 10000));
            _moveTimeoutItem.Tooltip = "Aborts the combo if the target couldn't be reached in that time.";
            generalMenu.AddItem(_moveTimeoutItem);

            _safeDistanceItem = new MenuItem("safeDistance", "Safe Distance").SetValue(new Slider(400, 0, 600));
            _safeDistanceItem.Tooltip =
                "Distance to keep after blinking/moving. Mustn't be higher than the combo range.";
            generalMenu.AddItem(_safeDistanceItem);

            _maxWalkDistance = new MenuItem("maxWalkDistance", "Max Walk Distance [%]").SetValue(new Slider(25, 0, 100));
            _maxWalkDistance.Tooltip =
                "If target is further away than this distance, then blink will be used.";
            generalMenu.AddItem(_maxWalkDistance);

            _menu.AddSubMenu(generalMenu);

            // combo
            var comboMenu = new Menu("Combo", "comboMenu");

            _comboListItem = new StringSwitcher("activeCombo", "Active Combo", ComboNames.ToArray())
            {
                Tooltip = "Currently active combo."
            };
            _comboListItem.ValueChanged += ComboListItem_ValueChanged;
            comboMenu.AddItem(_comboListItem);

            var selectedIndex = _comboListItem.GetValue<StringList>().SelectedIndex;
            _comboPicturesItem =
                new MenuItem("activeComboPictures", "").SetValue(
                    new AbilityToggler(ComboPictures[selectedIndex].ToDictionary(x => x, x => true)));
            comboMenu.AddItem(_comboPicturesItem);

            _menu.AddSubMenu(comboMenu);

            // hotkey
            var hotkeyMenu = new Menu("Hotkeys", "hotkeyMenu");

            _prepareCombo =
                new MenuItem("prepareCombo", "Prepare Combo").SetValue(new KeyBind(0, KeyBindType.Press));
            _prepareCombo.Tooltip = "Press this key combined with any combo key to prepare the combo.";
            hotkeyMenu.AddItem(_prepareCombo);

            _combo = new MenuItem(ComboName, "Combo").SetValue(new KeyBind(0, KeyBindType.Press));
            _combo.ValueChanged += _combo_ValueChanged;
            _combo.Tooltip = "Uses the currently selected combo.";
            hotkeyMenu.AddItem(_combo);


            _smartCombo = new MenuItem(SmartComboName, "Smart Combo").SetValue(new KeyBind(0, KeyBindType.Press));
            _smartCombo.ValueChanged += SmartCombo_ValueChanged;
            _smartCombo.Tooltip = "Tries to use a combo with the currently invoked spells.";
            hotkeyMenu.AddItem(_smartCombo);


            var nextCombo = new MenuItem("nextCombo", "Next Combo").SetValue(new KeyBind(0x6B, KeyBindType.Press));
            nextCombo.ValueChanged += NextCombo_ValueChanged;
            nextCombo.Tooltip = "Selects the next combo.";
            hotkeyMenu.AddItem(nextCombo);

            var prevCombo = new MenuItem("prevCombo", "Smart Combo").SetValue(new KeyBind(0x6D, KeyBindType.Press));
            prevCombo.ValueChanged += PrevCombo_ValueChanged;
            prevCombo.Tooltip = "Selects the previous combo.";
            hotkeyMenu.AddItem(prevCombo);


            _menu.AddSubMenu(hotkeyMenu);

            _menu.AddToMainMenu();
        }

        private static void ComboListItem_ValueChanged(object sender, OnValueChangeEventArgs e)
        {
            SetActiveCombo(e.GetNewValue<StringList>().SelectedIndex);
        }

        private static void SetActiveCombo(int index)
        {
            _comboPicturesItem.SetValue(new AbilityToggler(ComboPictures[index].ToDictionary(x => x, x => true)));

            OnActiveComboChanged(new StringEventArgs(ComboNames[index]));
        }

        private static void PrevCombo_ValueChanged(object sender, OnValueChangeEventArgs e)
        {
            if ((e.GetNewValue<KeyBind>().Key == e.GetOldValue<KeyBind>().Key) && e.GetNewValue<KeyBind>().Active)
            {
                var tmpList = _comboListItem.GetValue<StringList>();
                tmpList.SelectedIndex--;
                if (tmpList.SelectedIndex < 0)
                    tmpList.SelectedIndex = tmpList.SList.Length - 1;
                _comboListItem.SetValue(tmpList);
            }
        }

        private static void NextCombo_ValueChanged(object sender, OnValueChangeEventArgs e)
        {
            if ((e.GetNewValue<KeyBind>().Key == e.GetOldValue<KeyBind>().Key) && e.GetNewValue<KeyBind>().Active)
            {
                var tmpList = _comboListItem.GetValue<StringList>();
                tmpList.SelectedIndex++;
                if (tmpList.SelectedIndex >= tmpList.SList.Length)
                    tmpList.SelectedIndex = 0;
                _comboListItem.SetValue(tmpList);
            }
        }

        private static void _combo_ValueChanged(object sender, OnValueChangeEventArgs e)
        {
            if (e.GetNewValue<KeyBind>().Key != e.GetOldValue<KeyBind>().Key)
                EventAggregator.PublishOnCurrentThread(new ChangedKeyMessage(ComboName,
                    KeyInterop.KeyFromVirtualKey((int) e.GetNewValue<KeyBind>().Key)));
        }

        private static void SmartCombo_ValueChanged(object sender, OnValueChangeEventArgs e)
        {
            if (e.GetNewValue<KeyBind>().Key != e.GetOldValue<KeyBind>().Key)
                EventAggregator.PublishOnCurrentThread(new ChangedKeyMessage(SmartComboName,
                    KeyInterop.KeyFromVirtualKey((int) e.GetNewValue<KeyBind>().Key)));
        }

        private static void OnActiveComboChanged(StringEventArgs e)
        {
            ActiveComboChanged?.Invoke(null, e);
        }
    }
}