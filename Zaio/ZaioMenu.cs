using System;
using System.Windows.Input;
using Ensage.Common.Menu;
using SpacebarToFarm;

namespace Zaio
{
    public enum TargetSelectorMode
    {
        NearestToMouse,
        BestAutoAttackTarget,
        HighestHealth,
        Auto
    }

    public enum NoTargetMode
    {
        None,
        Move,
        AttackMove
    }

    public enum OrbwalkerMode
    {
        Mouse,
        Target,
        Attack,
        None
    }

    public enum ActiveControlMode
    {
        None,
        Auto,
        Follow,
        AttackComboTarget
    }

    public class ZaioMenu
    {
        private static Menu _menu;

        private static Menu _heroSettings;

        // general
        private static MenuItem _orbwalker;
        private static MenuItem _targetSelector;
        private static MenuItem _displayAttackRange;
        private static MenuItem _lockTarget;
        private static MenuItem _killSteal;
        private static MenuItem _noTargetMode;
        private static MenuItem _orbwalkerMode;
        private static MenuItem _blockKillSteal;
        private static MenuItem _shouldUseBlinkDagger;
        private static MenuItem _shouldRespectEvader;

        // creep
        private static MenuItem _creepControlMode;
        private static MenuItem _selectionOverrideControlMode;
        private static MenuItem _autoUnaggroTowers;
        private static MenuItem _autoUnaggroCreeps;

        // hotkeys
        private static MenuItem _comboKey;

        public static Key ComboKey => KeyInterop.KeyFromVirtualKey((int) _comboKey.GetValue<KeyBind>().Key);
        public static bool ShouldUseOrbwalker => _orbwalker.GetValue<bool>();
        public static bool ShouldDisplayAttackRange => _displayAttackRange.GetValue<bool>();
        public static bool ShouldLockTarget => _lockTarget.GetValue<bool>();
        public static bool ShouldKillSteal => _killSteal.GetValue<bool>();
        public static bool ShouldBlockKillStealWhileComboing => _blockKillSteal.GetValue<bool>();
        public static bool ShouldUseBlinkDagger => _shouldUseBlinkDagger.GetValue<bool>();
        public static bool ShouldRespectEvader => _shouldRespectEvader.GetValue<bool>();
        public static NoTargetMode NoTargetMode { get; private set; }

        public static OrbwalkerMode OrbwalkerMode { get; private set; }
        public static TargetSelectorMode TargetSelectorMode { get; private set; }

        public static ActiveControlMode ActiveControlMode { get; private set; }
        public static bool SelectionOverridesControlMode => _selectionOverrideControlMode.GetValue<bool>();
        public static bool AutoUnaggroTowers => _autoUnaggroTowers.GetValue<bool>();
        public static bool AutoUnaggroCreeps => _autoUnaggroCreeps.GetValue<bool>();

        public static event EventHandler<BoolEventArgs> DisplayAttackRangeChanged;
        public static event EventHandler<KeyEventArgs> ComboKeyChanged;

        public static void OnLoad()
        {
            if (_menu != null)
            {
                return;
            }

            _menu = new Menu("Zaio", "zaioMenu", true);

            //  ###########
            var general = new Menu("General", "zaioGeneral");

            _orbwalker = new MenuItem("zaioOrbwalk", "Orbwalk").SetValue(true);
            _orbwalker.Tooltip = "Uses the orbwalker instead of a normal attack command.";
            general.AddItem(_orbwalker);

            _displayAttackRange = new MenuItem("zaioAttackRange", "Display AttackRange").SetValue(true);
            _displayAttackRange.Tooltip = "Displays your auto attack range";
            _displayAttackRange.ValueChanged += _displayAttackRange_ValueChanged;
            general.AddItem(_displayAttackRange);

            _lockTarget = new MenuItem("zaioLockTarget", "Lock Combo-Target").SetValue(true);
            _lockTarget.Tooltip = "Keep your target when losing sight on it.";
            general.AddItem(_lockTarget);

            _killSteal = new MenuItem("zaioKillSteal", "Auto Killsteal").SetValue(true);
            _killSteal.Tooltip = "Uses items and abilities to killsteal when not comboing.";
            general.AddItem(_killSteal);

            _blockKillSteal = new MenuItem("zaioBlockKillSteal", "Block Killsteal").SetValue(true);
            _blockKillSteal.Tooltip = "Won't killsteal while you're using the combo hotkey on a target.";
            general.AddItem(_blockKillSteal);

            _shouldUseBlinkDagger = new MenuItem("zaioUseBlinkDagger", "Use Blink Dagger").SetValue(true);
            _shouldUseBlinkDagger.Tooltip = "Uses Blink Dagger to engage the target.";
            general.AddItem(_shouldUseBlinkDagger);

            _shouldRespectEvader = new MenuItem("zaioRespectEvader", "Prioritize Evader").SetValue(true);
            _shouldRespectEvader.Tooltip = "Will disable the combo mode while the evader is active.";
            general.AddItem(_shouldRespectEvader);

            _targetSelector =
                new MenuItem("zaioTargetSelector", "Target Selector").SetValue(
                    new StringList(new[] {"Closest to mouse", "Best autoattack target", "Highest Health" /*, "Auto"*/}));
            _targetSelector.ValueChanged += _targetSelector_ValueChanged;
            general.AddItem(_targetSelector);
            SetTargetSelectorMode(_targetSelector.GetValue<StringList>().SelectedValue);

            _noTargetMode = new MenuItem("zaioNoTargetMode", "No Target Mode");
            _noTargetMode.SetValue(new StringList(new[] {"Move", "None", "AttackMove"}));
            _noTargetMode.ValueChanged += _noTargetMode_ValueChanged;
            _noTargetMode.Tooltip = "Controls what happens when no target is found.";
            general.AddItem(_noTargetMode);
            SetNoTargetMode(_noTargetMode.GetValue<StringList>().SelectedValue);

            _orbwalkerMode = new MenuItem("zaioOrbwalkerMode", "Orbwalker Mode");
            _orbwalkerMode.SetValue(new StringList(new[] {"Mouse Position", "Target Position", "Simple Attack", "None"}));
            _orbwalkerMode.ValueChanged += _orbwalkerMode_ValueChanged;
            _orbwalkerMode.Tooltip = "Controls the orbwalker.";
            general.AddItem(_orbwalkerMode);
            SetOrbwalkerMode(_orbwalkerMode.GetValue<StringList>().SelectedValue);

            _menu.AddSubMenu(general);

            // ###########
            var control = new Menu("Control", "zaioControl");

            _selectionOverrideControlMode =
                new MenuItem("zaioSelectionControlMode", "Selection Overrides Control Mode").SetValue(true);
            _selectionOverrideControlMode.Tooltip = "Units currently selected won't be controlled by ZAIO.";
            control.AddItem(_selectionOverrideControlMode);

            _autoUnaggroTowers = new MenuItem("zaioAutoUnaggroTowers", "Auto Unaggro Towers").SetValue(true);
            _autoUnaggroTowers.Tooltip = "If you get aggroed by a tower, it will automatically unaggro if possible.";
            control.AddItem(_autoUnaggroTowers);

            _autoUnaggroCreeps = new MenuItem("zaioAutoUnaggroCreeps", "Auto Unaggro Creeps").SetValue(false);
            _autoUnaggroCreeps.Tooltip = "If you get aggroed by creeps, it will automatically unaggro if possible.";
            control.AddItem(_autoUnaggroCreeps);

            _creepControlMode = new MenuItem("zaioCreepControlMode", "Creep Control Mode");
            _creepControlMode.SetValue(new StringList(new[] {"Auto", "None", "Combo Target", "Follow"}));
            _creepControlMode.Tooltip = "Controls all units which are not your hero.";
            _creepControlMode.ValueChanged += _creepControlMode_ValueChanged;
            control.AddItem(_creepControlMode);
            SetCreepControlMode(_creepControlMode.GetValue<StringList>().SelectedValue);

            _menu.AddSubMenu(control);

            // ###########
            var hotkeys = new Menu("Hotkeys", "zaioHotkeys");

            _comboKey = new MenuItem("zaioCombo", "Combo").SetValue(new KeyBind(0, KeyBindType.Press));
            _comboKey.Tooltip = "Hotkey for the default combo.";
            _comboKey.ValueChanged += _comboKey_ValueChanged;
            hotkeys.AddItem(_comboKey);

            _menu.AddSubMenu(hotkeys);

            //  ###########
            _menu.AddToMainMenu();
        }

        private static void _targetSelector_ValueChanged(object sender, OnValueChangeEventArgs e)
        {
            SetTargetSelectorMode(e.GetNewValue<StringList>().SelectedValue);
        }

        private static void _creepControlMode_ValueChanged(object sender, OnValueChangeEventArgs e)
        {
            SetCreepControlMode(e.GetNewValue<StringList>().SelectedValue);
        }

        private static void SetOrbwalkerMode(string value)
        {
            switch (value)
            {
                case "Mouse Position":
                    OrbwalkerMode = OrbwalkerMode.Mouse;
                    break;
                case "Target Position":
                    OrbwalkerMode = OrbwalkerMode.Target;
                    break;
                case "Simple Attack":
                    OrbwalkerMode = OrbwalkerMode.Attack;
                    break;
                case "None":
                    OrbwalkerMode = OrbwalkerMode.None;
                    break;
            }
        }

        private static void SetTargetSelectorMode(string value)
        {
            switch (value) // "Closest to mouse", "Best autoattack target", "Highest Health"
            {
                case "Closest to mouse":
                    TargetSelectorMode = TargetSelectorMode.NearestToMouse;
                    break;
                case "Best autoattack target":
                    TargetSelectorMode = TargetSelectorMode.BestAutoAttackTarget;
                    break;
                case "Highest Health":
                    TargetSelectorMode = TargetSelectorMode.HighestHealth;
                    break;
            }
        }

        private static void SetCreepControlMode(string value)
        {
            switch (value)
            {
                case "None":
                    ActiveControlMode = ActiveControlMode.None;
                    break;
                case "Auto":
                    ActiveControlMode = ActiveControlMode.Auto;
                    break;
                case "Follow":
                    ActiveControlMode = ActiveControlMode.Follow;
                    break;
                case "Combo Target":
                    ActiveControlMode = ActiveControlMode.AttackComboTarget;
                    break;
            }
        }

        private static void _orbwalkerMode_ValueChanged(object sender, OnValueChangeEventArgs e)
        {
            SetOrbwalkerMode(e.GetNewValue<StringList>().SelectedValue);
        }

        private static void _comboKey_ValueChanged(object sender, OnValueChangeEventArgs e)
        {
            var newKey = e.GetNewValue<KeyBind>().Key;
            if (e.GetOldValue<KeyBind>().Key != newKey)
            {
                ComboKeyChanged?.Invoke(null, new KeyEventArgs(KeyInterop.KeyFromVirtualKey((int) newKey)));
            }
        }

        private static void _displayAttackRange_ValueChanged(object sender, OnValueChangeEventArgs e)
        {
            DisplayAttackRangeChanged?.Invoke(null, new BoolEventArgs(e.GetNewValue<bool>()));
        }

        private static void SetNoTargetMode(string value)
        {
            if (value == "None")
            {
                NoTargetMode = NoTargetMode.None;
            }
            else if (value == "Move")
            {
                NoTargetMode = NoTargetMode.Move;
            }
            else if (value == "AttackMove")
            {
                NoTargetMode = NoTargetMode.AttackMove;
            }
        }

        private static void _noTargetMode_ValueChanged(object sender, OnValueChangeEventArgs e)
        {
            SetNoTargetMode(e.GetNewValue<StringList>().SelectedValue);
        }

        public static void LoadHeroSettings(Menu newHeroSettings)
        {
            ResetHeroSettings();

            _heroSettings = newHeroSettings;
            _menu.AddSubMenu(_heroSettings);
        }

        public static void ResetHeroSettings()
        {
            if (_heroSettings != null)
            {
                _menu.RemoveSubMenu(_heroSettings.Name);
                _heroSettings = null;
            }
        }
    }
}