using System;
using System.Linq;
using System.Windows.Forms;
using Ensage;
using SharpDX;
using SharpDX.Direct3D9;

namespace JungleStack
{
    class Program
    {
        const int WM_KEYUP = 0x0101;
        const int WM_KEYDOWN = 0x0105;

        private static readonly Vector3[] StackRouteRadiant =
        {
            new Vector3(-2514,-155,256),  // Wait for attack
            new Vector3(-4762,-2229,256), // Move for pulling
            new Vector3(-2144,-544,256) // Wait position
        };

        private static readonly Vector3[] StackRouteDire =
        {
            new Vector3(3458, -640, 127),  // Wait for attack
            new Vector3(2278, 338, 127), // Move for pulling
            new Vector3(3808, -96, 256) // Wait position
        };

        private const int StartTimeRadiant = 47;
        private const int StartTimeDire = 45;

        private const int AttackTimeRadiant = 51;
        private const int AttackTimeDire = 52;


        private static Unit _pullCreep;
        private static Font _text;
        private static int _orderState = -1;
        private static int _attackTime;
        private static int _startTime;
        private static Vector3[] _route;
        private static bool _noCreep;
        private static readonly Timer Timer = new Timer();


        static void Main(string[] args)
        {
            Timer.Tick += Timer_Tick;
            _text = new Font(
               Drawing.Direct3DDevice9,
               new FontDescription
               {
                   FaceName = "Calibri",
                   Height = 13,
                   OutputPrecision = FontPrecision.Default,
                   Quality = FontQuality.Default
               });

            Drawing.OnPreReset += Drawing_OnPreReset;
            Drawing.OnPostReset += Drawing_OnPostReset;
            Drawing.OnEndScene += Drawing_OnEndScene;
            Game.OnWndProc += Game_OnGameWndProc;
            Game.OnUpdate += Game_OnGameUpdate;
            AppDomain.CurrentDomain.DomainUnload += CurrentDomain_DomainUnload;
        }

        static void Timer_Tick(object sender, EventArgs e)
        {
            Timer.Enabled = false;
        }

        static void CurrentDomain_DomainUnload(object sender, EventArgs e)
        {
            _text.Dispose();
        }

        static void Drawing_OnEndScene(EventArgs args)
        {
            if (Drawing.Direct3DDevice9 == null || Drawing.Direct3DDevice9.IsDisposed || !Game.IsInGame)
                return;

            var player = ObjectMgr.LocalPlayer;
            if( player == null || player.Team == Team.Observer)
                return;

            if (_pullCreep == null)
            {
                _text.DrawText(null, "StackScript: Select a ranged creep and press \"O\".", 5, 50, Color.White);
            }
            else
            {
                switch (_orderState)
                {
                    case -1:
                        _text.DrawText(null, "StackScript: moving to wait location", 5, 50, Color.White);
                        break;
                    case 0:
                        _text.DrawText(null,
                            _noCreep
                                ? "StackScript: found no creep for pulling."
                                : "StackScript: waiting for next pull.", 5, 50, Color.White);
                        break;
                    case 1:
                        _text.DrawText(null, "StackScript: waiting for attack order.", 5, 50, Color.White);
                        break;
                    case 2:
                        _text.DrawText(null, "StackScript: pulling.", 5, 50, Color.White);
                        break;
                }
            }
        }

        static void Drawing_OnPostReset(EventArgs args)
        {
            _text.OnResetDevice();
        }

        static void Drawing_OnPreReset(EventArgs args)
        {
            _text.OnLostDevice();
        }

        static void Game_OnGameWndProc(WndEventArgs args)
        {
            if (args.Msg != WM_KEYUP || args.WParam != 'O' || Game.IsChatOpen) 
                return;
            
            // Deactivate script
            if (_pullCreep != null)
            {
                Console.WriteLine("Deactivated");
                _pullCreep = null;
                return;
            }
            
            // Activate script
            var player = ObjectMgr.LocalPlayer;
            if (player == null || player.Team == Team.Observer) 
                return;
            switch (player.Team)
            {
                case Team.Radiant:
                    _startTime = StartTimeRadiant;
                    _attackTime = AttackTimeRadiant;
                    _route = StackRouteRadiant;
                    break;
                case Team.Dire:
                    _startTime = StartTimeDire;
                    _attackTime = AttackTimeDire;
                    _route = StackRouteDire;
                    break;
                default:
                    return;
            }
            var units = player.Selection.ToList();
            _pullCreep = (Unit)units.FirstOrDefault(unit => unit is Unit && ((Unit)unit).IsControllable && ((Unit)unit).IsRanged);
            if (_pullCreep != null)
            {
                _pullCreep.Move(_route.Last());
                _orderState = -1;
            }
        }

        static void Game_OnGameUpdate(EventArgs args)
        {
            if (Timer.Enabled || !Game.IsInGame || Game.IsPaused || _pullCreep == null)
                return;

            if (!_pullCreep.IsValid || !_pullCreep.IsAlive)
            {
                _pullCreep = null;
                return;
            }

            var seconds = ((int) Game.GameTime) % 60;
            switch (_orderState)
            {
                case -1:
                    if (GetDistance2D(_pullCreep.Position, _route.Last()) < 3)
                        _orderState = 0;
                    break;
                case 0:
                    if (seconds >= _startTime)
                    {
                        Console.WriteLine("Move to route 0");
                        _pullCreep.Move(_route[0]);
                        _orderState = 1;
                    }
                    break;
                case 1:
                    if (seconds >= _attackTime)
                    {
                        var attackme = GetNearestCreepToPull();
                        _noCreep = attackme == null;
                        if (_noCreep)
                        {
                            _pullCreep.Move(_route.Last());
                            _orderState = 0;

                            Timer.Interval = 10*1000; // wait until next minute starts
                            Timer.Start();
                            return;
                        }
                        _pullCreep.Attack(attackme);
                        _orderState = 2;
                        Timer.Interval = 1650; // Wait until attack starts
                        Timer.Start();
                    }
                    break;
                case 2:
                    _pullCreep.Move(_route[1]);
                    _pullCreep.Move(_route[2],true);
                    _orderState = 0;

                    Timer.Interval = 10*1000; // wait until next minute starts
                    Timer.Start();
                    break;
            }
        }

        private static Unit GetNearestCreepToPull()
        {
            var creeps =
                ObjectMgr.GetEntities<Creep>()
                    .Where(x => x.IsAlive && x.IsSpawned && x.IsAncient && x.IsVisible)
                    .ToList();
            Unit bestCreep = null;
            var bestDistance = float.MaxValue;
            foreach (var creep in creeps)
            {
                var distance = GetDistance2DFast(_pullCreep, creep);
                if (bestCreep == null || distance < bestDistance)
                {
                    bestDistance = distance;
                    bestCreep = creep;
                }
                
            }
            return bestCreep;
        }

        private static float GetDistance2DFast(Entity e1, Entity e2)
        {
            return (float)(Math.Pow(e1.Position.X - e2.Position.X, 2) + Math.Pow(e1.Position.Y - e2.Position.Y,2));
        }

        private static float GetDistance2D(Vector3 p1, Vector3 p2)
        {
            return (float)Math.Sqrt(Math.Pow(p1.X - p2.X, 2) + Math.Pow(p1.Y - p2.Y,2));
        }
    }
}
