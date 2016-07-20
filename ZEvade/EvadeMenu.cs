using System;

// ReSharper disable InconsistentNaming

namespace Evade
{
    using Ensage.Common.Menu;

    public sealed class EvadeMenu
    {
        private static readonly Menu Menu = new Menu("ZEvade", "zevade", true);
        
        // events
        public event EventHandler<BoolEventArgs> AutoEvadeChanged;
        public event EventHandler<BoolEventArgs> EvadePressed;
        public event EventHandler<BoolEventArgs> OrbwalkerPressed;
        public event EventHandler<BoolEventArgs> EvadeMovePressed;

        public event EventHandler<BoolEventArgs> DebugDrawChanged;
        public event EventHandler<BoolEventArgs> BotAutoAttackChanged;
        public event EventHandler<BoolEventArgs> DebugDrawMapChanged;

        // direct entries
        private readonly MenuItem autoEvadeItem;

        // hotkey entries
        private readonly Menu hotkeyMenu;

        private readonly MenuItem toggleAutoEvade;
        private readonly MenuItem evadeHotkey;
        private readonly MenuItem orbwalkHotkey;
        private readonly MenuItem evadeMoveHotkey;

        // debug entries
        private readonly Menu debugMenu;

        private readonly MenuItem debugDraw;
        private readonly MenuItem debugBotAttack;
        private readonly MenuItem debugConsoleOutput;
        private readonly MenuItem debugDrawMap;


        private bool settingValue;

        public EvadeMenu()
        {
            // direct
            autoEvadeItem = new MenuItem("autoEvade", "Auto Evade").SetValue(false);
            autoEvadeItem.ValueChanged += AutoEvadeItem_ValueChanged;
            Menu.AddItem(autoEvadeItem);
          
            // hotkey
            hotkeyMenu = new Menu("Hotkeys", "hotkeyMenu");

            toggleAutoEvade = new MenuItem("toggleEvade", "Toggle Auto Evade").SetValue(new KeyBind(0, KeyBindType.Toggle));
            toggleAutoEvade.ValueChanged += ToggleAutoEvade_ValueChanged;
            toggleAutoEvade.Tooltip = "Toggling auto evade by the hotkey.";
            hotkeyMenu.AddItem(toggleAutoEvade);

            evadeHotkey = new MenuItem("evadeHotkey", "Evade").SetValue(new KeyBind(0, KeyBindType.Press));
            evadeHotkey.ValueChanged += EvadeHotkey_ValueChanged;
            evadeHotkey.Tooltip = "Evading is only active while the hotkey is pressed.";
            hotkeyMenu.AddItem(evadeHotkey);

            orbwalkHotkey = new MenuItem("orbwalkHotkey", "Evade-Orbwalk").SetValue(new KeyBind(0x20, KeyBindType.Press, true));
            orbwalkHotkey.ValueChanged += OrbwalkHotkey_ValueChanged;
            orbwalkHotkey.Tooltip = "Orbwalks to the mouse position while evading will be active.";
            hotkeyMenu.AddItem(orbwalkHotkey);

            evadeMoveHotkey = new MenuItem("evadeMoveHotkey", "Evade-Walk").SetValue(new KeyBind(0, KeyBindType.Press, true));
            evadeMoveHotkey.ValueChanged += EvadeMoveHotkey_ValueChanged;
            evadeMoveHotkey.Tooltip = "Moves your hero to your mouse position while evading will be active, without attacking any units.";
            hotkeyMenu.AddItem(evadeMoveHotkey);

            Menu.AddSubMenu(hotkeyMenu);

            // debug
            debugMenu = new Menu("Debug Features", "debugMenu");

            debugDraw = new MenuItem("debugDraw", "Debug Drawings").SetValue(false);
            debugDraw.ValueChanged += DebugDraw_ValueChanged;
            debugDraw.Tooltip = "Draws information about obstacles around your hero.";
            debugMenu.AddItem(debugDraw);

            debugDrawMap = new MenuItem("debugDrawMap", "Draws obstacles on map").SetValue(true);
            debugDrawMap.ValueChanged += DebugDrawMap_ValueChanged;
            debugDrawMap.Tooltip = "Draws obstacles on the map.";
            debugMenu.AddItem(debugDrawMap);

            debugBotAttack = new MenuItem("debugBotAttack", "Bots auto attack").SetValue(false);
            debugBotAttack.ValueChanged += DebugBotAttack_ValueChanged;
            debugBotAttack.Tooltip = "Sunstrike Artillery!";
            debugMenu.AddItem(debugBotAttack);

            debugConsoleOutput = new MenuItem("debugConsoleOutput", "Debug console output").SetValue(false);
            debugConsoleOutput.ValueChanged += DebugConsoleOutput_ValueChanged;
            debugConsoleOutput.Tooltip = "Outputs debugging info in the console";
            Debugging.OutputEnabled = debugConsoleOutput.GetValue<bool>();
            debugMenu.AddItem(debugConsoleOutput);
            Menu.AddSubMenu(debugMenu);

            // finish
            Menu.AddToMainMenu();
        }

        private void DebugDrawMap_ValueChanged(object sender, OnValueChangeEventArgs e)
        {
           OnDebugDrawMapChanged(new BoolEventArgs(e.GetNewValue<bool>()));
        }

        private void DebugConsoleOutput_ValueChanged(object sender, OnValueChangeEventArgs e)
        {
            Debugging.OutputEnabled = e.GetNewValue<bool>();
        }

        private void EvadeMoveHotkey_ValueChanged(object sender, OnValueChangeEventArgs e)
        {
            OnEvadeMovePressed(new BoolEventArgs(e.GetNewValue<KeyBind>().Active));
        }

        private void DebugBotAttack_ValueChanged(object sender, OnValueChangeEventArgs e)
        {
            OnBotAutoAttackChanged(new BoolEventArgs(e.GetNewValue<bool>()));
        }

        private void DebugDraw_ValueChanged(object sender, OnValueChangeEventArgs e)
        {
            OnDebugDrawChanged(new BoolEventArgs(e.GetNewValue<bool>()));
        }

        private void OrbwalkHotkey_ValueChanged(object sender, OnValueChangeEventArgs e)
        {
            OnOrbwalkerPressed(new BoolEventArgs(e.GetNewValue<KeyBind>().Active));
        }

        private void EvadeHotkey_ValueChanged(object sender, OnValueChangeEventArgs e)
        {
            OnEvadePressed(new BoolEventArgs(e.GetNewValue<KeyBind>().Active));
        }

        private void OrbwalkEvade_ValueChanged(object sender, OnValueChangeEventArgs e)
        {
            OnOrbwalkerPressed(new BoolEventArgs(e.GetNewValue<bool>()));
        }

        private void AutoEvadeItem_ValueChanged(object sender, OnValueChangeEventArgs e)
        {
            if (!settingValue)
            {
                settingValue = true;

                var current = toggleAutoEvade.GetValue<KeyBind>();
                current.Active = e.GetNewValue<bool>();
                toggleAutoEvade.SetValue(current);

                OnAutoEvadeChanged(new BoolEventArgs(e.GetNewValue<bool>()));
                settingValue = false;
            }
           
        }

        private void ToggleAutoEvade_ValueChanged(object sender, OnValueChangeEventArgs e)
        {
            if (!settingValue)
            {
                settingValue = true;
                autoEvadeItem.SetValue(e.GetNewValue<KeyBind>().Active);
                OnAutoEvadeChanged(new BoolEventArgs(e.GetNewValue<KeyBind>().Active));
                settingValue = false;
            }
        }

        private void OnAutoEvadeChanged(BoolEventArgs e)
        {
            AutoEvadeChanged?.Invoke(this, e);
        }

        private void OnDebugDrawChanged(BoolEventArgs e)
        {
            DebugDrawChanged?.Invoke(this, e);
        }

        private void OnBotAutoAttackChanged(BoolEventArgs e)
        {
            BotAutoAttackChanged?.Invoke(this, e);
        }

        private void OnEvadePressed(BoolEventArgs e)
        {
            EvadePressed?.Invoke(this, e);
        }

        private void OnOrbwalkerPressed(BoolEventArgs e)
        {
            OrbwalkerPressed?.Invoke(this, e);
        }

        public bool IsDebugDrawEnabled => debugDraw.GetValue<bool>();
        public bool IsBotAutoAttackEnabled => debugBotAttack.GetValue<bool>();
        public bool IsAutoEvadeEnabled => autoEvadeItem.GetValue<bool>();

        public bool IsDebugDrawMapEnabled => debugDrawMap.GetValue<bool>();

        private void OnEvadeMovePressed(BoolEventArgs e)
        {
            EvadeMovePressed?.Invoke(this, e);
        }

        private void OnDebugDrawMapChanged(BoolEventArgs e)
        {
            DebugDrawMapChanged?.Invoke(this, e);
        }
    }
}
