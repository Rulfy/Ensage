namespace InvokerReborn
{
    using System;

    public class StringEventArgs : EventArgs
    {
        public StringEventArgs(string value)
        {
            this.Value = value;
        }

        public string Value { get; }
    }
}