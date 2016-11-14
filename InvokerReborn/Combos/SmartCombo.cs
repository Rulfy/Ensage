using System.Windows.Input;
using Ensage;
using PlaySharp.Toolkit.EventAggregator;

namespace InvokerReborn.Combos
{
    internal class SmartCombo : IHandle<ChangedKeyMessage>
    {
        public SmartCombo(Hero me, Key key)
        {
            InvokerMenu.EventAggregator.Subscribe(this);
        }

        public void Handle(ChangedKeyMessage message)
        {
            if (message.MenuItemName == InvokerMenu.SmartComboName)
            {
            }
        }
    }
}