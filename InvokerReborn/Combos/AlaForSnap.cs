namespace InvokerReborn.Combos
{
    using System.Windows.Input;

    using Ensage;
    using Ensage.Common.Extensions;

    using InvokerReborn.Abilities;
    using InvokerReborn.Interfaces;
    using InvokerReborn.SequenceHelpers;

    internal class AlaForSnap : InvokerCombo
    {
        public AlaForSnap(Hero me, Key key)
            : base(me, key)
        {
            this.AbilitySequence.Add(new AwaitBlinkOrMove(me, () => this.EngageRange));
            this.AbilitySequence.Add(new Alacrity(me));
            this.AbilitySequence.Add(new ForgeSpirit(me));
            this.AbilitySequence.Add(new ColdSnap(me));
        }

        protected override sealed int EngageRange => (int)this.Me.GetAttackRange();
    }
}