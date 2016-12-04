namespace SpacebarToFarm
{
    using System;
    using System.Collections.Generic;

    using Ensage;
    using Ensage.Common;
    using Ensage.Common.Menu;

    using SharpDX;

    class FarmMenu
    {
        #region Static Fields

        // autofarm entries
        private static readonly Menu AutofarmEntries; // TODO: need Unit Selector

        // helpers
        private static readonly Dictionary<Unit, MenuItem> AutoFarmEntries = new Dictionary<Unit, MenuItem>();

        private static readonly MenuItem BlueColorItem;

        private static readonly MenuItem GreenColorItem;

        private static readonly Menu Menu = new Menu("StF| Spacebar to Farm", "stf", true);

        // general entries
        private static readonly MenuItem RedColorItem;

        private static bool _isAutostopActive;

        private static bool _isDenyModeActive;

        private static bool _isEffectActiveEffectActive;

        private static bool _isLasthitModeActive;

        private static bool _isRangeEffectActive;

        private static int _meleeRange;

        private static int _rangedBonusRange;

        #endregion

        #region Constructors and Destructors

        static FarmMenu()
        {
            // general entries
            var generalMenu = new Menu("General settings", "generalSettings");

            var lasthitModeItem = new MenuItem("autoLasthit", "Lasthit creeps").SetValue(true);
            lasthitModeItem.Tooltip = "This is what this assembly is about, isn't it? (except as a support)";
            lasthitModeItem.ValueChanged += LasthitModeItem_ValueChanged;
            generalMenu.AddItem(lasthitModeItem);

            var denyModeItem = new MenuItem("autoDeny", "Deny creeps").SetValue(true);
            denyModeItem.Tooltip = "Will automatically deny creeps whenever no lasthit is possible.";
            denyModeItem.ValueChanged += DenyModeItem_ValueChanged;
            generalMenu.AddItem(denyModeItem);

            var autoStop = new MenuItem("autoStop", "Stop after lasthit").SetValue(true);
            autoStop.Tooltip = "Will automatically issue a stop order after the last target has been killed.";
            autoStop.ValueChanged += AutoStop_ValueChanged;
            generalMenu.AddItem(autoStop);

            var meleeRangeItem = new MenuItem("meleeRange", "Melee lasthit range").SetValue(new Slider(600, 400, 1000));
            meleeRangeItem.Tooltip = "Lasthit range for melee units.";
            meleeRangeItem.ValueChanged += MeleeRangeItem_ValueChanged;
            generalMenu.AddItem(meleeRangeItem);

            var rangedBonusItem =
                new MenuItem("rangedBonus", "Bonus ranged lasthit range").SetValue(new Slider(400, 0, 1000));
            rangedBonusItem.Tooltip = "Additional lasthit range for ranged units.";
            rangedBonusItem.ValueChanged += RangedBonusItem_ValueChanged;
            generalMenu.AddItem(rangedBonusItem);

            var drawLastHitRangeItem = new MenuItem("drawRange", "Draw lasthit range").SetValue(true);
            drawLastHitRangeItem.Tooltip = "Draws the lasthit range around every active unit.";
            drawLastHitRangeItem.ValueChanged += DrawLastHitRangeItem_ValueChanged;
            generalMenu.AddItem(drawLastHitRangeItem);

            var activeEffect = new MenuItem("activeEffect", "Active Effect").SetValue(true);
            activeEffect.Tooltip = "Shows an effect while a unit is lasthitting.";
            activeEffect.ValueChanged += ActiveEffect_ValueChanged;
            generalMenu.AddItem(activeEffect);

            var colorMenu = new Menu("Lasthit-range color", "colorSetting");
            RedColorItem = new MenuItem("redColor", "Red").SetValue(new Slider(255, 0, 255)).SetFontColor(Color.Red);
            GreenColorItem =
                new MenuItem("greenColor", "Green").SetValue(new Slider(0, 0, 255)).SetFontColor(Color.Green);
            BlueColorItem = new MenuItem("BlueColor", "Blue").SetValue(new Slider(160, 0, 255)).SetFontColor(Color.Blue);
            colorMenu.AddItem(RedColorItem);
            colorMenu.AddItem(GreenColorItem);
            colorMenu.AddItem(BlueColorItem);
            generalMenu.AddSubMenu(colorMenu);

            Menu.AddSubMenu(generalMenu);

            // autofarm entries
            AutofarmEntries = new Menu("Auto Farm Entries", "autoFarmEntries");

            //Menu.AddSubMenu(autofarmEntries); TODO:

            // hotkey
            var hotkeyMenu = new Menu("Hotkeys", "hotkeyMenu");

            var toggleFarm =
                new MenuItem("toggleFarmHotkey", "Toggle auto farm").SetValue(new KeyBind(0, KeyBindType.Press));
            toggleFarm.ValueChanged += ToggleFarm_ValueChanged;
            toggleFarm.Tooltip = "Toggling automatic lasthitting for the selected units.";
            hotkeyMenu.AddItem(toggleFarm);

            var farmHotkey = new MenuItem("farmHotkey", "Farm").SetValue(new KeyBind(0, KeyBindType.Press));
            farmHotkey.ValueChanged += FarmHotkey_ValueChanged;
            farmHotkey.Tooltip = "Farming with the currently selected units while this hotkey is pressed.";
            hotkeyMenu.AddItem(farmHotkey);

            Menu.AddSubMenu(hotkeyMenu);

            Events.OnClose += Events_OnClose;

            _isRangeEffectActive = drawLastHitRangeItem.GetValue<bool>();
            _isEffectActiveEffectActive = activeEffect.GetValue<bool>();
            _isAutostopActive = autoStop.GetValue<bool>();
            _meleeRange = meleeRangeItem.GetValue<Slider>().Value;
            _rangedBonusRange = rangedBonusItem.GetValue<Slider>().Value;
            _isLasthitModeActive = lasthitModeItem.GetValue<bool>();
            _isDenyModeActive = denyModeItem.GetValue<bool>();
        }

        #endregion

        #region Public Events

        public static event EventHandler<BoolEventArgs> ActiveEffectChanged;

        // events
        public static event EventHandler<BoolEventArgs> AutoFarmChanged;

        public static event EventHandler<BoolEventArgs> FarmPressed;

        public static event EventHandler<EventArgs> RangeChanged;

        public static event EventHandler<BoolEventArgs> RangeEffectChanged;

        #endregion

        #region Public Properties

        public static int BlueColor => BlueColorItem.GetValue<Slider>().Value;

        public static int GreenColor => GreenColorItem.GetValue<Slider>().Value;

        public static bool IsAutoStopEnabled => _isAutostopActive;

        public static bool IsDenyModeActive => _isDenyModeActive;

        public static bool IsLasthittingActive => _isLasthitModeActive;

        public static int MeleeRange => _meleeRange;

        public static int RangedBonusRange => _rangedBonusRange;

        public static int RedColor => RedColorItem.GetValue<Slider>().Value;

        public static bool ShouldDrawLasthitRange => _isRangeEffectActive;

        public static bool ShouldUseActiveEffect => _isEffectActiveEffectActive;

        #endregion

        #region Public Methods and Operators

        public static void AddAutoFarmEntry(Unit unit)
        {
        }

        public static void Initialize()
        {
            Menu.AddToMainMenu();
        }

        public static void RemoveAutoFarmEntry(Unit unit)
        {
        }

        #endregion

        #region Methods

        private static void ActiveEffect_ValueChanged(object sender, OnValueChangeEventArgs e)
        {
            OnActiveEffectChanged(new BoolEventArgs(e.GetNewValue<bool>()));
        }

        private static void AutoStop_ValueChanged(object sender, OnValueChangeEventArgs e)
        {
            _isAutostopActive = e.GetNewValue<bool>();
        }

        private static void DenyModeItem_ValueChanged(object sender, OnValueChangeEventArgs e)
        {
            _isDenyModeActive = e.GetNewValue<bool>();
        }

        private static void DrawLastHitRangeItem_ValueChanged(object sender, OnValueChangeEventArgs e)
        {
            OnRangeEffectChanged(new BoolEventArgs(e.GetNewValue<bool>()));
        }

        private static void Events_OnClose(object sender, EventArgs e)
        {
            AutoFarmEntries.Clear();
        }

        private static void FarmHotkey_ValueChanged(object sender, OnValueChangeEventArgs e)
        {
            OnFarmPressed(new BoolEventArgs(e.GetNewValue<KeyBind>().Active));
        }

        private static void LasthitModeItem_ValueChanged(object sender, OnValueChangeEventArgs e)
        {
            _isLasthitModeActive = e.GetNewValue<bool>();
        }

        private static void MeleeRangeItem_ValueChanged(object sender, OnValueChangeEventArgs e)
        {
            _meleeRange = e.GetNewValue<Slider>().Value;
            OnRangeChanged(EventArgs.Empty);
        }

        private static void OnActiveEffectChanged(BoolEventArgs e)
        {
            _isEffectActiveEffectActive = e.Value;
            ActiveEffectChanged?.Invoke(null, e);
        }

        private static void OnAutoFarmChanged(BoolEventArgs e)
        {
            AutoFarmChanged?.Invoke(null, e);
        }

        private static void OnFarmPressed(BoolEventArgs e)
        {
            FarmPressed?.Invoke(null, e);
        }

        private static void OnRangeChanged(EventArgs e)
        {
            RangeChanged?.Invoke(null, e);
        }

        private static void OnRangeEffectChanged(BoolEventArgs e)
        {
            _isRangeEffectActive = e.Value;
            RangeEffectChanged?.Invoke(null, e);
        }

        private static void RangedBonusItem_ValueChanged(object sender, OnValueChangeEventArgs e)
        {
            _rangedBonusRange = e.GetNewValue<Slider>().Value;
            OnRangeChanged(EventArgs.Empty);
        }

        private static void ToggleFarm_ValueChanged(object sender, OnValueChangeEventArgs e)
        {
            OnAutoFarmChanged(new BoolEventArgs(e.GetNewValue<KeyBind>().Active));
        }

        #endregion
    }
}