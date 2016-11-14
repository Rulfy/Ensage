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
    internal class Alacrity : InvokerComboAbility
    {
        private static readonly ILog Log = AssemblyLogs.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private readonly Ability _exort;
        private readonly Ability _wex;


        public Alacrity(Hero me) : this(me, () => 100)
        {
        }

        public Alacrity(Hero me, Func<int> extraDelay) : base(me, extraDelay)
        {
            Ability = me.FindSpell("invoker_alacrity");

            _wex = me.Spellbook.SpellW;
            _exort = me.Spellbook.SpellE;
        }

        public override SequenceEntryID ID { get; } = SequenceEntryID.Alacrity;
        public override bool IsSkilled => (Owner.Spellbook.SpellW.Level > 0) && (Owner.Spellbook.SpellE.Level > 0);

        public override async Task ExecuteAsync(Unit target, CancellationToken tk = default(CancellationToken))
        {
            var invokeDelay = await UseInvokeAbilityAsync(target, tk);
            Log.Debug($"Alacrity {ExtraDelay()} - {invokeDelay}");
            await Program.AwaitPingDelay(Math.Max(0, ExtraDelay() - invokeDelay), tk);
            Ability.UseAbility(Owner);
        }

        public override async Task<int> InvokeAbility(bool useCooldown,
            CancellationToken tk = default(CancellationToken))
        {
            // W W E
            return await InvokeAbility(new[] {_wex, _wex, _exort}, useCooldown, tk);
        }
    }
}