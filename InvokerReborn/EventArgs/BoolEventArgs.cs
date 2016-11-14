using System;

namespace InvokerReborn
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