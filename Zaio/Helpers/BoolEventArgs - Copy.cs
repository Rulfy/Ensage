using System;
using System.Windows.Input;

namespace SpacebarToFarm
{
    public class KeyEventArgs : EventArgs
    {
        #region Constructors and Destructors

        public KeyEventArgs(Key value)
        {
            Value = value;
        }

        #endregion

        #region Public Properties

        public Key Value { get; }

        #endregion
    }
}