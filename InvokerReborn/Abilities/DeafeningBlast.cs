using System;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Ensage;
using Ensage.Common.Extensions;
using InvokerReborn.Interfaces;
using log4net;
using PlaySharp.Toolkit.Logging;

namespace InvokerReborn.Abilities
{
    internal class DeafeningBlast : InvokerComboAbility
    {
        private static readonly ILog Log = AssemblyLogs.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
        private readonly Ability _exort;

        private readonly Ability _quas;
        private readonly Ability _wex;

        public DeafeningBlast(Hero me) : this(me, () => 100)
        {
        }

        public DeafeningBlast(Hero me, Func<int> extraDelay) : base(me, extraDelay)
        {
            Ability = me.FindSpell("invoker_deafening_blast");

            _quas = me.Spellbook.SpellQ;
            _wex = me.Spellbook.SpellW;
            _exort = me.Spellbook.SpellE;
        }

        public override SequenceEntryID ID { get; } = SequenceEntryID.DeafeningBlast;

        public override bool IsSkilled
            =>
            (Owner.Spellbook.SpellQ.Level > 0) && (Owner.Spellbook.SpellW.Level > 0) &&
            (Owner.Spellbook.SpellE.Level > 0);

        public override async Task<int> InvokeAbility(bool useCooldown,
            CancellationToken tk = default(CancellationToken))
        {
            // Q W E
            return await InvokeAbility(new[] {_quas, _wex, _exort}, useCooldown, tk);
        }

        public override async Task ExecuteAsync(Unit target, CancellationToken tk = default(CancellationToken))
        {
            var invokeDelay = await UseInvokeAbilityAsync(target, tk);
            Log.Debug($"DeafeningBlast {ExtraDelay()} - {invokeDelay}");
            await Program.AwaitPingDelay(Math.Max(0, ExtraDelay() - invokeDelay), tk);
            Ability.UseAbility(target.NetworkPosition);
        }
    }
}