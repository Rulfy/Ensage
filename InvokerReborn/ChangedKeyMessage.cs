namespace InvokerReborn
{
    using System.Windows.Input;

    public class ChangedKeyMessage
    {
        public ChangedKeyMessage(string menuItemName, Key key)
        {
            this.MenuItemName = menuItemName;
            this.Key = key;
        }

        public Key Key { get; }

        public string MenuItemName { get; }
    }
}