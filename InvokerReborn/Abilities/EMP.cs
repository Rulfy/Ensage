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
    // ReSharper disable once InconsistentNaming
    internal class EMP : InvokerComboAbility
    {
        private static readonly ILog Log = AssemblyLogs.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private readonly Ability _wex;

        public EMP(Hero me) : this(me, () => 100)
        {
        }

        public EMP(Hero me, Func<int> extraDelay) : base(me, extraDelay)
        {
            Ability = me.FindSpell("invoker_emp");

            _wex = me.Spellbook.SpellW;
        }

        public override SequenceEntryID ID { get; } = SequenceEntryID.EMP;
        public override bool IsSkilled => Owner.Spellbook.SpellW.Level > 0;

        public override async Task<int> InvokeAbility(bool useCooldown,
            CancellationToken tk = default(CancellationToken))
        {
            // W W W
            return await InvokeAbility(new[] {_wex, _wex, _wex}, useCooldown, tk);
        }

        public override async Task ExecuteAsync(Unit target, CancellationToken tk = default(CancellationToken))
        {
            var invokeDelay = await UseInvokeAbilityAsync(target, tk);
            Log.Debug($"EMP {ExtraDelay()} - {invokeDelay}");
            await Program.AwaitPingDelay(Math.Max(0, ExtraDelay() - invokeDelay), tk);
            Ability.UseAbility(target.NetworkPosition);
        }
    }
}