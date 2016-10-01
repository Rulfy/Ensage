using System;

namespace SpacebarToFarm
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
