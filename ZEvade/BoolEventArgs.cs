using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Evade
{
    public class BoolEventArgs : EventArgs
    {
        public BoolEventArgs(bool value)
        {
            Value = value;
        }
        public bool Value { get; }
    }
}
