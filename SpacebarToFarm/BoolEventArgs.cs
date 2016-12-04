namespace SpacebarToFarm
{
    using System;

    public class BoolEventArgs : EventArgs
    {
        #region Constructors and Destructors

        public BoolEventArgs(bool value)
        {
            Value = value;
        }

        #endregion

        #region Public Properties

        public bool Value { get; }

        #endregion
    }
}