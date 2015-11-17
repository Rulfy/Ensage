using Ensage;
using Ensage.Common.Menu;

namespace Zoomhack
{
	class Program
	{
		private static readonly Menu Menu = new Menu("Zoomhack", "zoomhack", true);

		private static readonly uint WM_MOUSEWHEEL = 0x020A;
		private static readonly uint VK_CTRL = 0x11;
		private static readonly ConVar ZoomVar = Game.GetConsoleVar("dota_camera_distance");
  
		static void Main()
		{
			var slider = new MenuItem("distance", "Default camera distance").SetValue(new Slider(1134, 1134, 2500));
			slider.ValueChanged += Slider_ValueChanged;
			Menu.AddItem(slider);

			Menu.AddToMainMenu();

			ZoomVar.RemoveFlags(ConVarFlags.Cheat);
			ZoomVar.SetValue(slider.GetValue<Slider>().Value);
			Game.GetConsoleVar("r_farz").SetValue(18000);

			Game.OnWndProc += Game_OnWndProc;
		}

		// ReSharper disable once InconsistentNaming
		private static void Slider_ValueChanged(object sender, OnValueChangeEventArgs e)
		{
			ZoomVar.SetValue(e.GetNewValue<Slider>().Value);
		}

		private static void Game_OnWndProc(WndEventArgs args)
		{
			if (args.Msg == WM_MOUSEWHEEL && Game.IsInGame )
			{
				if (Game.IsKeyDown(VK_CTRL))
				{
					// Get HIWORD(wParam)
					var delta = (short)((args.WParam >> 16) & 0xFFFF);
					// GetValue
					var zoomValue = ZoomVar.GetInt();
					if (delta < 0)
						zoomValue += 50;
					else
						zoomValue -= 50;
					// Set updated value
					ZoomVar.SetValue(zoomValue);
					// Block processed input from game
					args.Process = false;
				}
			}
		}
	}
}
