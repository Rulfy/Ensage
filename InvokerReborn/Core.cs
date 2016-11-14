using System.Collections.Generic;
using System.Linq;
using Ensage;
using Ensage.Common;
using InvokerReborn.Combos;
using InvokerReborn.Interfaces;
using PlaySharp.Toolkit.EventAggregator;

namespace InvokerReborn
{
    public class Core : IHandle<ChangedKeyMessage>
    {
        private readonly Dictionary<string, InvokerCombo> _availableCombos = new Dictionary<string, InvokerCombo>();
        private readonly Ability _invokedBase;
        private readonly Hero _me = ObjectManager.LocalHero;
        private InvokerCombo _activeCombo;

        public Core()
        {
            //AssemblyLogs.GLboal..

            InvokerMenu.BuildMenu();
            InvokerMenu.EventAggregator.Subscribe(this);
            InvokerMenu.ActiveComboChanged += InvokerMenu_ActiveComboChanged;

            _invokedBase =
                _me.Spellbook.Spells.FirstOrDefault(x => x.ClassID == ClassID.CDOTA_Ability_Invoker_InvokedBase);

            // add combos
            _availableCombos.Add("EulsSSMeteorBlast", new EulsSSMeteorBlast(_me, InvokerMenu.ComboKey));
            _availableCombos.Add("AlaForSnap", new AlaForSnap(_me, InvokerMenu.ComboKey));
            _availableCombos.Add("Assassination", new AssassinationCombo(_me, InvokerMenu.ComboKey));

            _activeCombo = _availableCombos[InvokerMenu.CurrentlyActiveCombo];
            _activeCombo.Activate();

            CheckCombos();

            Entity.OnInt32PropertyChange += Entity_OnInt32PropertyChange;
            Entity.OnHandlePropertyChange += Entity_OnHandlePropertyChange;
        }

        public void Handle(ChangedKeyMessage message)
        {
            if (message.MenuItemName == InvokerMenu.ComboName)
                foreach (var availableCombo in _availableCombos)
                    availableCombo.Value.SetKey(message.Key);
        }

        private void InvokerMenu_ActiveComboChanged(object sender, StringEventArgs e)
        {
            _activeCombo.Deactivate();
            _activeCombo = _availableCombos[e.Value];
            _activeCombo.Activate();
        }


        private void Entity_OnHandlePropertyChange(Entity sender, HandlePropertyChangeEventArgs args)
        {
            var hero = sender as Hero;
            // check if we bought a new item
            if ((hero != null) && (hero == _me) && (args.PropertyName == "m_hItems"))
                DelayAction.Add(250, CheckCombos);
        }

        private void Entity_OnInt32PropertyChange(Entity sender, Int32PropertyChangeEventArgs args)
        {
            var ability = sender as Ability;
            // check if Q,W,E has been skilled the first time
            if ((ability != null) && (args.OldValue == -1) && (ability == _invokedBase) 
                /*&& (args.PropertyName == "m_nQuasLevel" || args.PropertyName == "m_nWexLevel" || args.PropertyName == "m_nExortLevel")*/)
                DelayAction.Add(250, CheckCombos);
        }

        private void CheckCombos()
        {
            foreach (var availableCombo in _availableCombos)
                availableCombo.Value.OnComboCheck();
        }
    }
}