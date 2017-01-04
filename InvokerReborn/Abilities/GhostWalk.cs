namespace InvokerReborn.Abilities
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;

    using Ensage;
    using Ensage.Common.Extensions;
    using Ensage.Common.Threading;

    using InvokerReborn.Interfaces;

    internal class GhostWalk : InvokerComboAbility
    {
        private readonly Ability _quas;

        private readonly Ability _wex;

        public GhostWalk(Hero me)
            : this(me, () => 100)
        {
        }

        public GhostWalk(Hero me, Func<int> extraDelay)
            : base(me, extraDelay)
        {
            this.Ability = me.FindSpell("invoker_ghost_walk");

            this._quas = me.Spellbook.SpellQ;
            this._wex = me.Spellbook.SpellW;
        }

        public override SequenceEntryID ID { get; } = SequenceEntryID.GhostWalk;

        public override bool IsSkilled
            => (this.Owner.Spellbook.SpellQ.Level > 0) && (this.Owner.Spellbook.SpellW.Level > 0);

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
            // Q Q W
            return await this.InvokeAbility(new[] { this._quas, this._quas, this._wex }, useCooldown, tk);
        }
    }
}