using System;
using System.Linq;
using Ensage;
using SharpDX;
using SharpDX.Direct3D9;

namespace Manabars
{
    class Program
    {
        // Length of manabar
        private const int BarLength = 100;

        private static Line _rectangle, _frame;
        private static Font _text;
        static void Main(string[] args)
        {
            _rectangle = new Line(Drawing.Direct3DDevice) { Width = 7 };
            _frame = new Line(Drawing.Direct3DDevice) { Width = 1 };
            _text = new Font(
                Drawing.Direct3DDevice,
                new FontDescription
                {
                    FaceName = "Calibri",
                    Height = 10,
                    OutputPrecision = FontPrecision.Default,
                    Quality = FontQuality.Default
                });


            Drawing.OnEndScene += Drawing_OnEndScene;
            Drawing.OnPreReset += Drawing_OnPreReset;
            Drawing.OnPostReset += Drawing_OnPostReset;

            AppDomain.CurrentDomain.DomainUnload += CurrentDomain_DomainUnload;
        }

        static void CurrentDomain_DomainUnload(object sender, EventArgs e)
        {
            _text.Dispose();
            _rectangle.Dispose();
            _frame.Dispose();
        }

        static void Drawing_OnPostReset(EventArgs args)
        {
            _rectangle.OnResetDevice();
            _text.OnResetDevice();
            _frame.OnResetDevice();
        }

        static void Drawing_OnPreReset(EventArgs args)
        {
            _rectangle.OnLostDevice();
            _text.OnLostDevice();
            _frame.OnLostDevice();
        }

        static void Drawing_OnEndScene(EventArgs args)
        {
            if (Drawing.Direct3DDevice == null || Drawing.Direct3DDevice.IsDisposed)
                return;

            var enemies = EntityList.GetEntities<Hero>().Where(x => x.Visible && x.Alive && x.MaxMana != 0 && x.Team != EntityList.GetLocalPlayer().Team).ToList();
            foreach (var enemy in enemies)
            {
                Vector2 screenPos;
                var enemyPos = enemy.Position + new Vector3(0, 0, enemy.HealthbarOffset);
                if (!Drawing.WorldToScreen(enemyPos, out screenPos))
                    continue;

                var start = screenPos + new Vector2(-BarLength/2, -20);
                var manaDelta = new Vector2(BarLength * enemy.Mana / enemy.MaxMana, 0);
             
                // Draw background
                _rectangle.Begin();
                _rectangle.Draw(new[] { start + manaDelta + manaDelta, start + new Vector2(BarLength, 0) }, Color.DarkBlue);
                // Draw manabar
                _rectangle.Draw(new[] { start, start + manaDelta }, Color.DodgerBlue);
                _rectangle.End();
                // Draw frame
                var topLeft = start + new Vector2(0, -3);
                var topRight = start + new Vector2(BarLength, -3);
                var botRight = topRight + new Vector2(0, 7);
                var botLeft = topLeft + new Vector2(0, 7);
                _frame.Begin();
                _frame.Draw(new[]{topLeft,topRight,botRight,botLeft,topLeft},Color.Black);
                _frame.End();

                // Draw text
                var text = string.Format("{0}/{1}", (int)enemy.Mana, (int)enemy.MaxMana);
                var textSize = _text.MeasureText(null, text, FontDrawFlags.Center);
                var textPos = start + new Vector2(BarLength/2 -textSize.Width/2, -textSize.Height/2);
                _text.DrawText(null, text, (int)textPos.X, (int)textPos.Y, Color.White);
            }
        }
    }
}
