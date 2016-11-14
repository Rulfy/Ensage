using System.Windows.Input;
using Ensage;
using Ensage.Common.Extensions;
using InvokerReborn.Abilities;
using InvokerReborn.Interfaces;
using InvokerReborn.SequenceHelpers;

namespace InvokerReborn.Combos
{
    internal class AlaForSnap : InvokerCombo
    {
        public AlaForSnap(Hero me, Key key) : base(me, key)
        {
            AbilitySequence.Add(new AwaitBlinkOrMove(me, () => EngageRange));
            AbilitySequence.Add(new Alacrity(me));
            AbilitySequence.Add(new ForgeSpirit(me));
            AbilitySequence.Add(new ColdSnap(me));
        }

        protected sealed override int EngageRange => (int) Me.GetAttackRange();
    }
}