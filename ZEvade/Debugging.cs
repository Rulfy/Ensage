using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Evade
{
    public static class Debugging
    {
        public static bool OutputEnabled { get; set; }

        public static void WriteLine(string line)
        {
            if(OutputEnabled)
                Console.WriteLine(line);
        }

        public static void WriteLine(string line, object p1)
        {
            if (OutputEnabled)
                Console.WriteLine(line, p1);
        }

        public static void WriteLine(string line, object p1, object p2)
        {
            if (OutputEnabled)
                Console.WriteLine(line, p1, p2);
        }

    }
}
