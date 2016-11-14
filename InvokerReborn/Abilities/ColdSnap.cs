using System;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Ensage;
using Ensage.Common.Extensions;
using Ensage.Common.Threading;
using InvokerReborn.Interfaces;
using log4net;
using PlaySharp.Toolkit.Logging;

namespace InvokerReborn.Abilities
{
    internal class ColdSnap : InvokerComboAbility
    {
        private static readonly ILog Log = AssemblyLogs.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private readonly Ability _quas;

        public ColdSnap(Hero me) : this(me, () => 100)
        {
        }

        public ColdSnap(Hero me, Func<int> extraDelay) : base(me, extraDelay)
        {
            Ability = me.FindSpell("invoker_cold_snap");

            _quas = me.Spellbook.SpellQ;
        }

        public override SequenceEntryID ID { get; } = SequenceEntryID.ColdSnap;
        public override bool IsSkilled => Owner.Spellbook.SpellQ.Level > 0;

        public override async Task<int> InvokeAbility(bool useCooldown,
            CancellationToken tk = default(CancellationToken))
        {
            // Q Q Q
            return await InvokeAbility(new[] {_quas, _quas, _quas}, useCooldown, tk);
        }

        public override async Task ExecuteAsync(Unit target, CancellationToken tk = default(CancellationToken))
        {
            var invokeDelay = await UseInvokeAbilityAsync(target, tk);
            Log.Debug($"ColdSnap {ExtraDelay()} | {invokeDelay}");
            await Await.Delay(Math.Max(0, ExtraDelay() - invokeDelay), tk);
            Ability.UseAbility(target);
        }
    }
}