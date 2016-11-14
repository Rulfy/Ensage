using System;
using System.Threading;
using System.Threading.Tasks;
using Ensage;
using Ensage.Common.Extensions;
using Ensage.Common.Threading;
using InvokerReborn.Interfaces;

namespace InvokerReborn.Abilities
{
    internal class GhostWalk : InvokerComboAbility
    {
        private readonly Ability _quas;
        private readonly Ability _wex;

        public GhostWalk(Hero me) : this(me, () => 100)
        {
        }

        public GhostWalk(Hero me, Func<int> extraDelay) : base(me, extraDelay)
        {
            Ability = me.FindSpell("invoker_ghost_walk");

            _quas = me.Spellbook.SpellQ;
            _wex = me.Spellbook.SpellW;
        }

        public override SequenceEntryID ID { get; } = SequenceEntryID.GhostWalk;
        public override bool IsSkilled => (Owner.Spellbook.SpellQ.Level > 0) && (Owner.Spellbook.SpellW.Level > 0);

        public override async Task<int> InvokeAbility(bool useCooldown,
            CancellationToken tk = default(CancellationToken))
        {
            // Q Q W
            return await InvokeAbility(new[] {_quas, _quas, _wex}, useCooldown, tk);
        }

        public override async Task ExecuteAsync(Unit target, CancellationToken tk = default(CancellationToken))
        {
            var invokeDelay = await UseInvokeAbilityAsync(target, tk);
            await Await.Delay(Math.Max(0, ExtraDelay() - invokeDelay), tk);
            Ability.UseAbility();
        }
    }
}