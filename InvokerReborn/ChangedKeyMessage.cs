using System.Windows.Input;

namespace InvokerReborn
{
    public class ChangedKeyMessage
    {
        public ChangedKeyMessage(string menuItemName, Key key)
        {
            MenuItemName = menuItemName;
            Key = key;
        }

        public string MenuItemName { get; }
        public Key Key { get; }
    }
}