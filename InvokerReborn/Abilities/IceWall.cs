using System;
using System.Threading;
using System.Threading.Tasks;
using Ensage;
using Ensage.Common.Extensions;
using InvokerReborn.Interfaces;

namespace InvokerReborn.Abilities
{
    internal class IceWall : InvokerComboAbility
    {
        private readonly Ability _exort;
        private readonly Ability _quas;

        public IceWall(Hero me) : this(me, () => 100)
        {
        }

        public IceWall(Hero me, Func<int> extraDelay) : base(me, extraDelay)
        {
            Ability = me.FindSpell("invoker_ice_wall");

            _quas = me.Spellbook.SpellQ;
            _exort = me.Spellbook.SpellE;
        }

        public override SequenceEntryID ID { get; } = SequenceEntryID.IceWall;
        public override bool IsSkilled => (Owner.Spellbook.SpellQ.Level > 0) && (Owner.Spellbook.SpellE.Level > 0);

        public override async Task<int> InvokeAbility(bool useCooldown,
            CancellationToken tk = default(CancellationToken))
        {
            // Q Q E
            return await InvokeAbility(new[] {_quas, _quas, _exort}, useCooldown, tk);
        }

        public override async Task ExecuteAsync(Unit target, CancellationToken tk = default(CancellationToken))
        {
            var invokeDelay = await UseInvokeAbilityAsync(target, tk);
            await Program.AwaitPingDelay(Math.Max(0, ExtraDelay() - invokeDelay), tk);
            Ability.UseAbility();
        }
    }
}