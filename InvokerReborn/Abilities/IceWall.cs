namespace InvokerReborn.Abilities
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;

    using Ensage;
    using Ensage.Common.Extensions;
    using Ensage.Common.Threading;

    using InvokerReborn.Interfaces;

    internal class IceWall : InvokerComboAbility
    {
        private readonly Ability _exort;

        private readonly Ability _quas;

        public IceWall(Hero me)
            : this(me, () => 100)
        {
        }

        public IceWall(Hero me, Func<int> extraDelay)
            : base(me, extraDelay)
        {
            this.Ability = me.FindSpell("invoker_ice_wall");

            this._quas = me.Spellbook.SpellQ;
            this._exort = me.Spellbook.SpellE;
        }

        public override SequenceEntryID ID { get; } = SequenceEntryID.IceWall;

        public override bool IsSkilled
            => (this.Owner.Spellbook.SpellQ.Level > 0) && (this.Owner.Spellbook.SpellE.Level > 0);

        public override async Task ExecuteAsync(Unit target, CancellationToken tk = default(CancellationToken))
        {
            var invokeDelay = await this.UseInvokeAbilityAsync(target, tk);
            await Await.Delay(Math.Max(0, this.ExtraDelay() - invokeDelay), tk);
            this.Ability.UseAbility();
        }

        public override async Task<int> InvokeAbility(
            bool useCooldown,
            CancellationToken tk = default(CancellationToken))
        {
            // Q Q E
            return await this.InvokeAbility(new[] { this._quas, this._quas, this._exort }, useCooldown, tk);
        }
    }
}