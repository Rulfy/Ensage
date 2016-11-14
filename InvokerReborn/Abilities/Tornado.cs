using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Ensage;
using Ensage.Common.Extensions;
using InvokerReborn.Interfaces;

namespace InvokerReborn.Abilities
{
    internal class Tornado : InvokerComboAbility
    {
        private readonly Ability _quas;
        private readonly Ability _wex;

        public Tornado(Hero me) : this(me, () => 100)
        {
        }

        public Tornado(Hero me, Func<int> extraDelay) : base(me, extraDelay)
        {
            Ability = me.FindSpell("invoker_tornado");

            _quas = me.Spellbook.SpellQ;
            _wex = me.Spellbook.SpellW;
        }

        public override SequenceEntryID ID { get; } = SequenceEntryID.Tornado;
        public override bool IsSkilled => (Owner.Spellbook.SpellQ.Level > 0) && (Owner.Spellbook.SpellW.Level > 0);

        public override int Duration
            => (int) Ability.AbilitySpecialData.First(x => x.Name == "lift_duration").GetValue(_quas.Level - 1);

        public int Distance
            => (int) Ability.AbilitySpecialData.First(x => x.Name == "travel_distance").GetValue(_wex.Level - 1);

        // "0.8 1.1 1.4 1.7 2.0 2.3 2.6 2.9"

        public override async Task<int> InvokeAbility(bool useCooldown,
            CancellationToken tk = default(CancellationToken))
        {
            // W W Q
            return await InvokeAbility(new[] {_wex, _wex, _quas}, useCooldown, tk);
        }

        public override async Task ExecuteAsync(Unit target, CancellationToken tk = default(CancellationToken))
        {
            var invokeDelay = await UseInvokeAbilityAsync(target, tk);
            await Program.AwaitPingDelay(Math.Max(0, ExtraDelay() - invokeDelay), tk);
            Ability.UseAbility(target.NetworkPosition);
        }
    }
}