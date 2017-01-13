using System;
using System.Windows.Input;
using Ensage.Common.Menu;
using SpacebarToFarm;

namespace Zaio
{
    public enum TargetSelectorMode
    {
        NearestToMouse,
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
        Attack
    }

    public class ZaioMenu
    {
        private static Menu _menu;

        private static Menu _heroSettings;

        private static MenuItem _comboKey;
        private static MenuItem _orbwalker;
        private static MenuItem _targetSelector;
        private static MenuItem _displayAttackRange;
        private static MenuItem _lockTarget;
        private static MenuItem _killSteal;
        private static MenuItem _noTargetMode;
        private static MenuItem _orbwalkerMode;
        private static MenuItem _blockKillSteal;

        public static Key ComboKey => KeyInterop.KeyFromVirtualKey((int) _comboKey.GetValue<KeyBind>().Key);
        public static bool ShouldUseOrbwalker => _orbwalker.GetValue<bool>();
        public static bool ShouldDisplayAttackRange => _displayAttackRange.GetValue<bool>();
        public static bool ShouldLockTarget => _lockTarget.GetValue<bool>();
        public static bool ShouldKillSteal => _killSteal.GetValue<bool>();
        public static bool ShouldBlockKillStealWhileComboing => _blockKillSteal.GetValue<bool>();
        public static NoTargetMode NoTargetMode { get; private set; }

        public static OrbwalkerMode OrbwalkerMode { get; private set; }

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

            _targetSelector =
                new MenuItem("zaioTargetSelector", "Target Selector").SetValue(
                    new StringList(new[] {"Closest to mouse" /*, "Auto"*/}));
            general.AddItem(_targetSelector);

            _noTargetMode = new MenuItem("zaioNoTargetMode", "No Target Mode");
            _noTargetMode.SetValue(new StringList(new[] {"None", "Move", "AttackMove"}));
            _noTargetMode.ValueChanged += _noTargetMode_ValueChanged;
            _noTargetMode.Tooltip = "Controls what happens when no target is found.";
            general.AddItem(_noTargetMode);
            SetNoTargetMode(_noTargetMode.GetValue<StringList>().SelectedValue);

            _orbwalkerMode = new MenuItem("zaioOrbwalkerMode", "Orbwalker Mode");
            _orbwalkerMode.SetValue(new StringList(new[] {"Mouse Position", "Target Position", "Simple Attack"}));
            _orbwalkerMode.ValueChanged += _orbwalkerMode_ValueChanged;
            _orbwalkerMode.Tooltip = "Controls the orbwalker.";
            general.AddItem(_orbwalkerMode);
            SetOrbwalkerMode(_orbwalkerMode.GetValue<StringList>().SelectedValue);

            _menu.AddSubMenu(general);

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

        private static void SetOrbwalkerMode(string value)
        {
            if (value == "Mouse Position")
            {
                OrbwalkerMode = OrbwalkerMode.Mouse;
            }
            else if (value == "Target Position")
            {
                OrbwalkerMode = OrbwalkerMode.Target;
            }
            else if (value == "Simple Attack")
            {
                OrbwalkerMode = OrbwalkerMode.Attack;
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