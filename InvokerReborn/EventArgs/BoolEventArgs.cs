namespace InvokerReborn
{
    using System;

    public class BoolEventArgs : EventArgs
    {
        public BoolEventArgs(bool value)
        {
            this.Value = value;
        }

        public bool Value { get; }
    }
}