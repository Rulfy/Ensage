using System;
using System.Windows.Forms;
using Ensage;
using Ensage.Common;
using SharpDX;

namespace TestScript
{
    class Program
    {
        static private Render.Line _someLine ;
        static void Main(string[] args)
        {
            MessageBox.Show("Hello world from our first script");

            // hook an event
            Drawing.OnEndScene += Drawing_OnEndScene; 

            // Add some render object
            _someLine = new Render.Line(new Vector2(0,Drawing.Height/2-5), new Vector2(Drawing.Width,Drawing.Height/2-5), 10, Color.Red);
            

            // Some cool text
            var text = new Render.Text("Ensage#", Drawing.Width/2, Drawing.Height/2- 50, 48, Color.PowderBlue)
            {
                Centered = true,
                OutLined =  true,
            };
            text.Add(); // Add it to the render collection
        }

        static void Drawing_OnEndScene(EventArgs args)
        {
           _someLine.OnEndScene(); // Render our text without giving it to the collection
        }

    }
}
