using System;
using System.Collections.Generic;
using Ensage;
using Ensage.Common;
using Ensage.Common.Menu;
using SharpDX;

namespace SpacebarToFarm
{
    class FarmMenu
    {
        private static readonly Menu Menu = new Menu("StF| Spacebar to Farm", "stf", true);

        // events
        public static event EventHandler<BoolEventArgs> AutoFarmChanged;
        public static event EventHandler<BoolEventArgs> FarmPressed;

        // general entries
        private static readonly Menu GeneralMenu;

        private static readonly MenuItem LasthitModeItem;
        private static readonly MenuItem DenyModeItem;
        private static readonly MenuItem AutoStop;
        private static readonly MenuItem MeleeRangeItem;
        private static readonly MenuItem RangedBonusItem;
        private static readonly MenuItem DrawLastHitRangeItem;
        private static readonly MenuItem ActiveEffect;

        private static readonly MenuItem RedColorItem;
        private static readonly MenuItem GreenColorItem;
        private static readonly MenuItem BlueColorItem;


        // autofarm entries
        private static readonly Menu AutofarmEntries; // TODO: need Unit Selector

        // hotkey entries
        private static readonly Menu HotkeyMenu;

        private static readonly MenuItem ToggleFarm;
        private static readonly MenuItem FarmHotkey;

        // helpers
        private static bool _settingValue;

        private static readonly Dictionary<Unit,MenuItem> AutoFarmEntries = new Dictionary<Unit, MenuItem>();

        static FarmMenu()
        {
            // general entries
            GeneralMenu = new Menu("General settings", "generalSettings");

            LasthitModeItem = new MenuItem("autoLasthit", "Lasthit creeps").SetValue(true);
            LasthitModeItem.Tooltip = "This is what this assembly is about, isn't it? (except as a support)";
            GeneralMenu.AddItem(LasthitModeItem);

            DenyModeItem = new MenuItem("autoDeny", "Deny creeps").SetValue(true);
            DenyModeItem.Tooltip = "Will automatically deny creeps whenever no lasthit is possible.";
            GeneralMenu.AddItem(DenyModeItem);

            AutoStop = new MenuItem("autoStop", "Stop after lasthit").SetValue(true);
            AutoStop.Tooltip = "Will automatically issue a stop order after the last target has been killed.";
            GeneralMenu.AddItem(AutoStop);

            MeleeRangeItem = new MenuItem("meleeRange", "Melee lasthit range").SetValue(new Slider(600, 400, 1000));
            MeleeRangeItem.Tooltip = "Lasthit range for melee units.";
            GeneralMenu.AddItem(MeleeRangeItem);

            RangedBonusItem = new MenuItem("rangedBonus", "Bonus ranged lasthit range").SetValue(new Slider(400, 0, 1000));
            RangedBonusItem.Tooltip = "Additional lasthit range for ranged units.";
            GeneralMenu.AddItem(RangedBonusItem);

            DrawLastHitRangeItem = new MenuItem("drawRange", "Draw lasthit range").SetValue(true);
            DrawLastHitRangeItem.Tooltip = "Draws the lasthit range around every active unit.";
            GeneralMenu.AddItem(DrawLastHitRangeItem);

            ActiveEffect = new MenuItem("activeEffect", "Active Effect").SetValue(true);
            ActiveEffect.Tooltip = "Shows an effect while a unit is lasthitting.";
            GeneralMenu.AddItem(ActiveEffect);

            

            var colorMenu = new Menu("Lasthit-range color", "colorSetting");
            RedColorItem = new MenuItem("redColor", "Red").SetValue(new Slider(255, 0, 255)).SetFontColor(Color.Red);
            GreenColorItem = new MenuItem("greenColor", "Green").SetValue(new Slider(0, 0, 255)).SetFontColor(Color.Green);
            BlueColorItem = new MenuItem("BlueColor", "Blue").SetValue(new Slider(160, 0, 255)).SetFontColor(Color.Blue);
            colorMenu.AddItem(RedColorItem);
            colorMenu.AddItem(GreenColorItem);
            colorMenu.AddItem(BlueColorItem);
            GeneralMenu.AddSubMenu(colorMenu);

            Menu.AddSubMenu(GeneralMenu);

            // autofarm entries
            AutofarmEntries = new Menu("Auto Farm Entries", "autoFarmEntries");

            //Menu.AddSubMenu(autofarmEntries); TODO:

            // hotkey
            HotkeyMenu = new Menu("Hotkeys", "hotkeyMenu");

            ToggleFarm = new MenuItem("toggleFarmHotkey", "Toggle auto farm").SetValue(new KeyBind(0, KeyBindType.Press));
            ToggleFarm.ValueChanged += ToggleFarm_ValueChanged;
            ToggleFarm.Tooltip = "Toggling automatic lasthitting for the selected units.";
            HotkeyMenu.AddItem(ToggleFarm);

            FarmHotkey = new MenuItem("farmHotkey", "Farm").SetValue(new KeyBind(0, KeyBindType.Press));
            FarmHotkey.ValueChanged += FarmHotkey_ValueChanged;
            FarmHotkey.Tooltip = "Farming with the currently selected units while this hotkey is pressed.";
            HotkeyMenu.AddItem(FarmHotkey);

            Menu.AddSubMenu(HotkeyMenu);

            Events.OnClose += Events_OnClose;
        }

        public static bool IsLasthittingActive => LasthitModeItem.GetValue<bool>();
        public static bool IsDenyModeActive => DenyModeItem.GetValue<bool>();
        public static bool IsAutoStopEnabled => AutoStop.GetValue<bool>();
        public static int MeleeRange => MeleeRangeItem.GetValue<Slider>().Value;
        public static int RangedBonusRange=> RangedBonusItem.GetValue<Slider>().Value;
        public static bool ShouldDrawLasthitRange => DrawLastHitRangeItem.GetValue<bool>();

        public static bool ShouldUseActiveEffect => ActiveEffect .GetValue<bool>();

        public static int RedColor => RedColorItem.GetValue<Slider>().Value;
        public static int GreenColor => GreenColorItem.GetValue<Slider>().Value;
        public static int BlueColor => BlueColorItem.GetValue<Slider>().Value;

        private static void Events_OnClose(object sender, EventArgs e)
        {
            AutoFarmEntries.Clear();
        }

        public static void AddAutoFarmEntry(Unit unit)
        {
          
        }

        public static void RemoveAutoFarmEntry(Unit unit)
        {

        }

        private static void FarmHotkey_ValueChanged(object sender, OnValueChangeEventArgs e)
        {
            if (!_settingValue)
            {
                _settingValue = true;
                OnFarmPressed(new BoolEventArgs(e.GetNewValue<KeyBind>().Active));
                _settingValue = false;
            }
        }

        public static void Initialize()
        {
            Menu.AddToMainMenu();
        }

        private static void ToggleFarm_ValueChanged(object sender, OnValueChangeEventArgs e)
        {
            if (!_settingValue)
            {
                _settingValue = true;
                OnAutoFarmChanged(new BoolEventArgs(e.GetNewValue<KeyBind>().Active));
                _settingValue = false;
            }
        }

        protected static void OnAutoFarmChanged(BoolEventArgs e)
        {
            AutoFarmChanged?.Invoke(ToggleFarm, e);
        }

        protected static void OnFarmPressed(BoolEventArgs e)
        {
            FarmPressed?.Invoke(FarmHotkey, e);
        }
    }
}
