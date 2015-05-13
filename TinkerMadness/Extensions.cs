using System.Windows.Forms;

namespace TinkerMadness
{
    public static class Extensions
    {
        public static void Start(this Timer timer, double time)
        {
            timer.Interval += (int) time;
            timer.Start();
        }
    }
}
