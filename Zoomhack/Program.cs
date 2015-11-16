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
	    private static readonly uint VK_CTRL = 0x11;
        private static readonly ConVar ZoomVar = Game.GetConsoleVar("dota_camera_distance");
  
		static void Main()
		{
            ZoomVar.RemoveFlags(ConVarFlags.Cheat);
            Game.OnWndProc += Game_OnWndProc;
		}

		private static void Game_OnWndProc(WndEventArgs args)
		{
            
			if (args.Msg == WM_MOUSEWHEEL)
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
