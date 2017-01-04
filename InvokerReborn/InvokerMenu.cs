namespace InvokerReborn
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Windows.Input;

    using Ensage.Common.Menu;
    using Ensage.Common.Menu.MenuItems;

    using PlaySharp.Toolkit.EventAggregator;

    public class InvokerMenu
    {
        public const string ComboName = "combo";

        public const string SmartComboName = "smartCombo";

        private static readonly List<string> ComboNames = new List<string>
                                                              {
                                                                  "AlaForSnap",
                                                                  "EulsSSMeteorBlast",
                                                                  "Assassination",
                                                                  "ToEMPMetBlastRefMetBlastEMP"
                                                              };

        private static readonly List<List<string>> ComboPictures = new List<List<string>>
                                                                       {
                                                                           new List<string>
                                                                               {
                                                                                   "invoker_alacrity",
                                                                                   "invoker_forge_spirit",
                                                                                   "cold_snap"
                                                                               },
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
                                                                               },
                                                                           new List<string>
                                                                               {
                                                                                   "invoker_tornado",
                                                                                   "invoker_emp",
                                                                                   "invoker_chaos_meteor",
                                                                                   "invoker_deafening_blast",
                                                                                   "item_refresher",
                                                                                   "invoker_chaos_meteor1",
                                                                                   "invoker_deafening_blast1",
                                                                                   "invoker_emp1"
                                                                               }
                                                                       };

        private static MenuItem combo;

        // combo
        private static StringSwitcher comboListItem;

        private static MenuItem comboPicturesItem;

        private static MenuItem ghostWalkItem;

        private static MenuItem maxWalkDistance;

        // main
        private static Menu menu;

        private static MenuItem moveTimeoutItem;

        // hotkeys
        private static MenuItem prepareCombo;

        // general
        private static MenuItem safeDistanceItem;

        private static MenuItem smartCombo;

        private static MenuItem sunstrikeAutoKillItem;

        private static MenuItem sunstrikeKillStealItem;

        private static MenuItem sunstrikeSafeCastItem;

        // events
        public static event EventHandler<StringEventArgs> ActiveComboChanged;

        public static event EventHandler<BoolEventArgs> GhostWalkKeyPressed;

        public static Key ComboKey => KeyInterop.KeyFromVirtualKey((int)combo.GetValue<KeyBind>().Key);

        public static string CurrentlyActiveCombo => comboListItem.GetValue<StringList>().SelectedValue;

        // events
        public static IEventAggregator EventAggregator { get; private set; }

        public static Key GhostWalkKey => KeyInterop.KeyFromVirtualKey((int)ghostWalkItem.GetValue<KeyBind>().Key);

        public static bool IsPrepareKeyPressed => prepareCombo.GetValue<KeyBind>().Active;

        public static int MaxWalkDistance => maxWalkDistance.GetValue<Slider>().Value;

        public static int MoveTimeout => moveTimeoutItem.GetValue<Slider>().Value;

        public static int SafeDistance => safeDistanceItem.GetValue<Slider>().Value;

        public static Key SmartComboKey => KeyInterop.KeyFromVirtualKey((int)smartCombo.GetValue<KeyBind>().Key);

        public static bool SunStrikeAutoKill => sunstrikeAutoKillItem.GetValue<bool>();

        public static bool SunStrikeKillSteal => sunstrikeKillStealItem.GetValue<bool>();

        public static bool SunStrikeSafeCast => sunstrikeSafeCastItem.GetValue<bool>();

        public static void BuildMenu()
        {
            if (menu != null)
            {
                return;
            }

            foreach (var comboPicture in ComboPictures)
            {
                comboPicture.Reverse();
            }

            EventAggregator = new EventAggregator();
            menu = new Menu("Invoker Reborn", "invreb", true, "npc_dota_hero_invoker", true);

            // general
            var generalMenu = new Menu("General", "generalMenu");

            moveTimeoutItem = new MenuItem("moveTimeout", "Reach Timeout [ms]").SetValue(new Slider(2000, 500, 10000));
            moveTimeoutItem.Tooltip = "Aborts the combo if the target couldn't be reached in that time.";
            generalMenu.AddItem(moveTimeoutItem);

            safeDistanceItem = new MenuItem("safeDistance", "Safe Distance").SetValue(new Slider(400, 0, 600));
            safeDistanceItem.Tooltip =
                "Distance to keep after blinking/moving. Mustn't be higher than the combo range.";
            generalMenu.AddItem(safeDistanceItem);

            maxWalkDistance = new MenuItem("maxWalkDistance", "Max Walk Distance [%]").SetValue(new Slider(25));
            maxWalkDistance.Tooltip = "If target is further away than this distance, then blink will be used.";
            generalMenu.AddItem(maxWalkDistance);

            sunstrikeSafeCastItem = new MenuItem("ssSafeCast", "SunStrike Safe Cast").SetValue(true);
            sunstrikeSafeCastItem.Tooltip =
                "Cast SunStrike as soon as an enemy is disabled for a long enough duration.";
            generalMenu.AddItem(sunstrikeSafeCastItem);

            sunstrikeAutoKillItem = new MenuItem("ssAutoKill", "SunStrike Auto Kill").SetValue(true);
            sunstrikeAutoKillItem.Tooltip = "Cast SunStrike if an enemy is killable and no allies are too close.";
            generalMenu.AddItem(sunstrikeAutoKillItem);

            sunstrikeKillStealItem = new MenuItem("ssKillSteal", "SunStrike Kill Steal").SetValue(false);
            sunstrikeKillStealItem.Tooltip = "Cast SunStrike as soon as an enemy is killable.";
            generalMenu.AddItem(sunstrikeKillStealItem);

            menu.AddSubMenu(generalMenu);

            // combo
            var comboMenu = new Menu("Combo", "comboMenu");

            comboListItem = new StringSwitcher("activeCombo", "Active Combo", ComboNames.ToArray())
                                 {
                                     Tooltip = "Currently active combo."
                                 };
            comboListItem.ValueChanged += ComboListItem_ValueChanged;
            comboMenu.AddItem(comboListItem);

            var selectedIndex = comboListItem.GetValue<StringList>().SelectedIndex;
            comboPicturesItem =
                new MenuItem("activeComboPictures", string.Empty).SetValue(
                    new AbilityToggler(ComboPictures[selectedIndex].ToDictionary(x => x, x => true)));
            comboMenu.AddItem(comboPicturesItem);

            menu.AddSubMenu(comboMenu);

            // hotkey
            var hotkeyMenu = new Menu("Hotkeys", "hotkeyMenu");

            prepareCombo = new MenuItem("prepareCombo", "Prepare Combo").SetValue(new KeyBind(0, KeyBindType.Press));
            prepareCombo.Tooltip = "Press this key combined with any combo key to prepare the combo.";
            hotkeyMenu.AddItem(prepareCombo);

            combo = new MenuItem(ComboName, "Combo").SetValue(new KeyBind(0, KeyBindType.Press));
            combo.ValueChanged += _combo_ValueChanged;
            combo.Tooltip = "Uses the currently selected combo.";
            hotkeyMenu.AddItem(combo);

            smartCombo = new MenuItem(SmartComboName, "Smart Combo").SetValue(new KeyBind(0, KeyBindType.Press));
            smartCombo.ValueChanged += SmartCombo_ValueChanged;
            smartCombo.Tooltip = "Tries to use a combo with the currently invoked spells.";
            hotkeyMenu.AddItem(smartCombo);

            var nextCombo = new MenuItem("nextCombo", "Next Combo").SetValue(new KeyBind(0x6B, KeyBindType.Press));
            nextCombo.ValueChanged += NextCombo_ValueChanged;
            nextCombo.Tooltip = "Selects the next combo.";
            hotkeyMenu.AddItem(nextCombo);

            var prevCombo = new MenuItem("prevCombo", "Previous Combo").SetValue(new KeyBind(0x6D, KeyBindType.Press));
            prevCombo.ValueChanged += PrevCombo_ValueChanged;
            prevCombo.Tooltip = "Selects the previous combo.";
            hotkeyMenu.AddItem(prevCombo);

            ghostWalkItem = new MenuItem("ghostWalk", "GhostWalk").SetValue(new KeyBind(0, KeyBindType.Press));
            ghostWalkItem.ValueChanged += _ghostWalkItem_ValueChanged;
            ghostWalkItem.Tooltip = "Invokes and uses ghostwalk if ready.";
            hotkeyMenu.AddItem(ghostWalkItem);

            menu.AddSubMenu(hotkeyMenu);

            menu.AddToMainMenu();
        }

        private static void _combo_ValueChanged(object sender, OnValueChangeEventArgs e)
        {
            if (e.GetNewValue<KeyBind>().Key != e.GetOldValue<KeyBind>().Key)
            {
                EventAggregator.PublishOnCurrentThread(
                    new ChangedKeyMessage(ComboName, KeyInterop.KeyFromVirtualKey((int)e.GetNewValue<KeyBind>().Key)));
            }
        }

        private static void _ghostWalkItem_ValueChanged(object sender, OnValueChangeEventArgs e)
        {
            if (e.GetNewValue<KeyBind>().Key == e.GetOldValue<KeyBind>().Key)
            {
                GhostWalkKeyPressed?.Invoke(null, new BoolEventArgs(e.GetNewValue<KeyBind>().Active));
            }
        }

        private static void ComboListItem_ValueChanged(object sender, OnValueChangeEventArgs e)
        {
            SetActiveCombo(e.GetNewValue<StringList>().SelectedIndex);
        }

        private static void NextCombo_ValueChanged(object sender, OnValueChangeEventArgs e)
        {
            if ((e.GetNewValue<KeyBind>().Key == e.GetOldValue<KeyBind>().Key) && e.GetNewValue<KeyBind>().Active)
            {
                var tmpList = comboListItem.GetValue<StringList>();
                tmpList.SelectedIndex++;
                if (tmpList.SelectedIndex >= tmpList.SList.Length)
                {
                    tmpList.SelectedIndex = 0;
                }

                comboListItem.SetValue(tmpList);
            }
        }

        private static void PrevCombo_ValueChanged(object sender, OnValueChangeEventArgs e)
        {
            if ((e.GetNewValue<KeyBind>().Key == e.GetOldValue<KeyBind>().Key) && e.GetNewValue<KeyBind>().Active)
            {
                var tmpList = comboListItem.GetValue<StringList>();
                tmpList.SelectedIndex--;
                if (tmpList.SelectedIndex < 0)
                {
                    tmpList.SelectedIndex = tmpList.SList.Length - 1;
                }

                comboListItem.SetValue(tmpList);
            }
        }

        private static void SetActiveCombo(int index)
        {
            comboPicturesItem.SetValue(new AbilityToggler(ComboPictures[index].ToDictionary(x => x, x => true)));

            ActiveComboChanged?.Invoke(null, new StringEventArgs(ComboNames[index]));
        }

        private static void SmartCombo_ValueChanged(object sender, OnValueChangeEventArgs e)
        {
            if (e.GetNewValue<KeyBind>().Key != e.GetOldValue<KeyBind>().Key)
            {
                EventAggregator.PublishOnCurrentThread(
                    new ChangedKeyMessage(
                        SmartComboName,
                        KeyInterop.KeyFromVirtualKey((int)e.GetNewValue<KeyBind>().Key)));
            }
        }
    }
}