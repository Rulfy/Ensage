using System;
using System.Windows.Input;
using Ensage;

namespace Zoomhack
{
	class Program
	{
		// TODO: make config and set zoomhack on load
		// TODO: make key configurable
		private static readonly uint WM_MOUSEWHEEL = 0x020A;
  
		static void Main()
		{
			Game.OnWndProc += Game_OnWndProc;
		}

		private static void Game_OnWndProc(WndEventArgs args)
		{
			if (args.Msg == WM_MOUSEWHEEL && Game.IsInGame )
			{
				if (Game.IsKeyDown(Key.LeftCtrl))
				{
					// Find zoomhack console variable
					var zoomVar = Game.GetConsoleVar("dota_camera_distance");
					if (zoomVar != null)
					{
						// Get HIWORD(wParam)
						var delta = (short)((args.WParam >> 16) & 0xFFFF);
						// GetValue
						var zoomValue = zoomVar.GetInt();
						if (delta < 0)
							zoomValue += 50;
						else
							zoomValue -= 50;
						// Set updated value
						zoomVar.SetValue(zoomValue);
						// Block processed input from game
						args.Process = false;
					}
				}
			}
		}
	}
}
