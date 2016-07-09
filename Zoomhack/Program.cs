// <copyright file="Program.cs" company="EnsageSharp">
//    Copyright (c) 2016 EnsageSharp.
//    This program is free software: you can redistribute it and/or modify
//    it under the terms of the GNU General Public License as published by
//    the Free Software Foundation, either version 3 of the License, or
//    (at your option) any later version.
//    This program is distributed in the hope that it will be useful,
//    but WITHOUT ANY WARRANTY; without even the implied warranty of
//    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
//    GNU General Public License for more details.
//    You should have received a copy of the GNU General Public License
//    along with this program.  If not, see http://www.gnu.org/licenses/
// </copyright>
namespace Zoomhack
{
    using Ensage;
    using Ensage.Common;
    using Ensage.Common.Menu;

    /// <summary>
    ///     The program.
    /// </summary>
    internal class Program
    {
        #region Constants

        /// <summary>
        ///     The zoom default value.
        /// </summary>
        private const int ZoomDefaultValue = 1134;

        /// <summary>
        ///     The zoom max value.
        /// </summary>
        private const int ZoomMaxValue = 6000;

        /// <summary>
        ///     The zoom min value.
        /// </summary>
        private const int ZoomMinValue = 0;

        #endregion

        #region Fields

        /// <summary>
        ///     The menu.
        /// </summary>
        private readonly Menu menu = new Menu("ZoomHack", "zoomhack", true);

        /// <summary>
        ///     The zoom key.
        /// </summary>
        private readonly MenuItem zoomKey = new MenuItem("zoomKey", "Key");

        /// <summary>
        ///     The zoom slider.
        /// </summary>
        private readonly MenuItem zoomSlider = new MenuItem("distance", "Default camera distance");

        /// <summary>
        ///     The zoom value.
        /// </summary>
        private readonly ConVar zoomVar = Game.GetConsoleVar("dota_camera_distance");

        #endregion

        #region Methods

        /// <summary>
        ///     The main.
        /// </summary>
        private static void Main()
        {
            var zoomHack = new Program();

            zoomHack.zoomKey.SetValue(new KeyBind(0x11, KeyBindType.Press))
                .SetTooltip("Hold the key and scroll with your mouse to change the zoom value");
            zoomHack.menu.AddItem(zoomHack.zoomKey);

            zoomHack.zoomSlider.SetValue(new Slider(ZoomDefaultValue, ZoomMinValue, ZoomMaxValue));
            zoomHack.zoomSlider.ValueChanged +=
                (sender, args) => { zoomHack.zoomVar.SetValue(args.GetNewValue<Slider>().Value); };
            zoomHack.menu.AddItem(zoomHack.zoomSlider);

            zoomHack.menu.AddToMainMenu();

            zoomHack.zoomVar.RemoveFlags(ConVarFlags.Cheat);
            zoomHack.zoomVar.SetValue(zoomHack.zoomSlider.GetValue<Slider>().Value);

            Game.GetConsoleVar("r_farz").SetValue(18000);
            Game.OnWndProc += zoomHack.Game_OnWndProc;
        }

        /// <summary>
        ///     The window process event
        /// </summary>
        /// <param name="args">
        ///     The args.
        /// </param>
        private void Game_OnWndProc(WndEventArgs args)
        {
            if (args.Msg != (ulong)WindowsMessages.MOUSEWHEEL || !Game.IsInGame)
            {
                return;
            }

            if (!this.zoomKey.IsActive())
            {
                return;
            }

            // Get HIWORD(wParam)
            var delta = (short)((args.WParam >> 16) & 0xFFFF);

            // GetValue
            var zoomValue = this.zoomVar.GetInt();
            if (delta < 0)
            {
                zoomValue += 50;
            }
            else
            {
                zoomValue -= 50;
            }

            // Set updated value
            this.zoomVar.SetValue(zoomValue);
            this.zoomSlider.SetValue(new Slider(zoomValue, ZoomMinValue, ZoomMaxValue));

            // Block processed input from game
            args.Process = false;
        }

        #endregion
    }
}
