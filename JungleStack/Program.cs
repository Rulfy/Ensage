using System;
using System.Linq;
using Ensage;
using Ensage.Common.Menu;
using SharpDX;
using SharpDX.Direct3D9;

namespace JungleStack
{
    class Program
    {
        private static readonly Menu Menu = new Menu("JungleStack", "jungleStack", true);

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


        private static Unit pullCreep;
        private static Font text;
        private static int orderState = -1;
        private static int attackTime;
        private static int startTime;
        private static Vector3[] route;
        private static bool noCreep;
        private static readonly System.Windows.Forms.Timer Timer = new System.Windows.Forms.Timer();


        static void Main(string[] args)
        {
            var hotkey = new MenuItem("hotkey", "Toggle hotkey").SetValue(
               new KeyBind('O', KeyBindType.Toggle));
            hotkey.ValueChanged += Hotkey_ValueChanged;
            Menu.AddItem(hotkey);

            Menu.AddItem(new MenuItem("positionX", "X-Position").SetValue(
              new Slider(5, 0, Drawing.Width)));

            Menu.AddItem(new MenuItem("positionY", "Y-Position").SetValue(
              new Slider(50, 0, Drawing.Height)));

            Menu.AddToMainMenu();

            ChangeToggleValue(false);

            Timer.Tick += Timer_Tick;
            text = new Font(
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
            Game.OnUpdate += Game_OnGameUpdate;
            AppDomain.CurrentDomain.DomainUnload += CurrentDomain_DomainUnload;
        }
        // ReSharper disable once InconsistentNaming
        private static void Hotkey_ValueChanged(object sender, OnValueChangeEventArgs e)
        {
            var newVal = e.GetNewValue<KeyBind>().Active;
            if (newVal != e.GetOldValue<KeyBind>().Active)
            {
                // Deactivate script
                if (!newVal)
                {
                    pullCreep = null;
                }
                else
                {
                    // Activate script
                    var player = ObjectMgr.LocalPlayer;
                    if (player == null || player.Team == Team.Observer)
                        return;
                    switch (player.Team)
                    {
                        case Team.Radiant:
                            startTime = StartTimeRadiant;
                            attackTime = AttackTimeRadiant;
                            route = StackRouteRadiant;
                            break;
                        case Team.Dire:
                            startTime = StartTimeDire;
                            attackTime = AttackTimeDire;
                            route = StackRouteDire;
                            break;
                        default:
                            e.Process = false;
                            return;
                    }
                    var units = player.Selection.ToList();
                    pullCreep = (Unit)units.FirstOrDefault(unit => unit is Unit && ((Unit)unit).IsControllable && ((Unit)unit).IsRanged);
                    if (pullCreep != null)
                    {
                        pullCreep.Move(route.Last());
                        orderState = -1;
                    }
                    else e.Process = false;
                }
            }
        }

        // ReSharper disable once InconsistentNaming
        static void Timer_Tick(object sender, EventArgs e)
        {
            Timer.Enabled = false;
        }

        // ReSharper disable once InconsistentNaming
        static void CurrentDomain_DomainUnload(object sender, EventArgs e)
        {
            text.Dispose();
        }

        static void Drawing_OnEndScene(EventArgs args)
        {
            if (Drawing.Direct3DDevice9 == null || Drawing.Direct3DDevice9.IsDisposed || !Game.IsInGame)
                return;

            var player = ObjectMgr.LocalPlayer;
            if( player == null || player.Team == Team.Observer)
                return;

            int x = Menu.Item("positionX").GetValue<Slider>().Value, y = Menu.Item("positionY").GetValue<Slider>().Value;
            if (pullCreep == null)
            {
                var key = (char)Menu.Item("hotkey").GetValue<KeyBind>().Key;
                text.DrawText(null, "StackScript: Select a ranged creep and press \""+key+"\".", x, y, Color.White);
            }
            else
            {
                switch (orderState)
                {
                    case -1:
                        text.DrawText(null, "StackScript: moving to wait location", x, y, Color.White);
                        break;
                    case 0:
                        text.DrawText(null,
                            noCreep
                                ? "StackScript: found no creep for pulling."
                                : "StackScript: waiting for next pull.", x, y, Color.White);
                        break;
                    case 1:
                        text.DrawText(null, "StackScript: waiting for attack order.", x, y, Color.White);
                        break;
                    case 2:
                        text.DrawText(null, "StackScript: pulling.", x, y, Color.White);
                        break;
                }
            }
        }

        static void Drawing_OnPostReset(EventArgs args)
        {
            text.OnResetDevice();
        }

        static void Drawing_OnPreReset(EventArgs args)
        {
            text.OnLostDevice();
        }

        static void ChangeToggleValue(bool newValue)
        {
            var oldVal = Menu.Item("hotkey").GetValue<KeyBind>();
            Menu.Item("hotkey").SetValue(new KeyBind(oldVal.Key, KeyBindType.Toggle, newValue));
        }

        static void Game_OnGameUpdate(EventArgs args)
        {
            if (Timer.Enabled || !Game.IsInGame || Game.IsPaused || pullCreep == null)
                return;

            if (!pullCreep.IsValid || !pullCreep.IsAlive)
            {
                ChangeToggleValue(false);
                pullCreep = null;
                return;
            }

            var seconds = ((int) Game.GameTime) % 60;
            switch (orderState)
            {
                case -1:
                    if (GetDistance2D(pullCreep.Position, route.Last()) < 3)
                        orderState = 0;
                    break;
                case 0:
                    if (seconds >= startTime)
                    {
                        Console.WriteLine("Move to route 0");
                        pullCreep.Move(route[0]);
                        orderState = 1;
                    }
                    break;
                case 1:
                    if (seconds >= attackTime)
                    {
                        var attackme = GetNearestCreepToPull();
                        noCreep = attackme == null;
                        if (noCreep)
                        {
                            pullCreep.Move(route.Last());
                            orderState = 0;

                            Timer.Interval = 10*1000; // wait until next minute starts
                            Timer.Start();
                            return;
                        }
                        pullCreep.Attack(attackme);
                        orderState = 2;
                        Timer.Interval = 1650; // Wait until attack starts
                        Timer.Start();
                    }
                    break;
                case 2:
                    pullCreep.Move(route[1]);
                    pullCreep.Move(route[2],true);
                    orderState = 0;

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
                var distance = GetDistance2DFast(pullCreep, creep);
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
