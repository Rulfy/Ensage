using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Ensage;

namespace TinkerMadness
{
    class Program
    {
        const int WM_KEYUP = 0x0101;
        const int WM_KEYDOWN = 0x0105;

        private static Hero _target;
        private static bool activated;
        static void Main(string[] args)
        {
            Game.OnGameWndProc += Game_OnGameWndProc;
        }

        static void Game_OnGameWndProc(WndProcEventArgs args)
        {
            if (args.MsgId != WM_KEYUP || args.WParam != 'O' || Game.IsChatOpen)
                return;

        }
    }
}
