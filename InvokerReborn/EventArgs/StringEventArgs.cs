using System;

namespace InvokerReborn
{
    public class StringEventArgs : EventArgs
    {
        public StringEventArgs(string value)
        {
            Value = value;
        }

        public string Value { get; }
    }
}