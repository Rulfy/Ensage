using System;
using System.Linq;
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
    internal class Meteor : InvokerComboAbility
    {
        private static readonly ILog Log = AssemblyLogs.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private readonly Ability _exort;
        private readonly Ability _wex;

        public Meteor(Hero me) : this(me, () => 100)
        {
        }

        public Meteor(Hero me, Func<int> extraDelay) : base(me, extraDelay)
        {
            Ability = me.FindSpell("invoker_chaos_meteor");

            _wex = me.Spellbook.SpellW;
            _exort = me.Spellbook.SpellE;
        }

        public override SequenceEntryID ID { get; } = SequenceEntryID.Meteor;
        public override bool IsSkilled => (Owner.Spellbook.SpellW.Level > 0) && (Owner.Spellbook.SpellE.Level > 0);

        public override int Delay => (int) (Ability.AbilitySpecialData.First(x => x.Name == "land_time").Value*1000);
        // 1.3

        public override async Task<int> InvokeAbility(bool useCooldown,
            CancellationToken tk = default(CancellationToken))
        {
            // E E W
            return await InvokeAbility(new[] {_exort, _exort, _wex}, useCooldown, tk);
        }

        public override async Task ExecuteAsync(Unit target, CancellationToken tk = default(CancellationToken))
        {
            var invokeDelay = await UseInvokeAbilityAsync(target, tk);
            Log.Debug($"Meteor {ExtraDelay()} - {invokeDelay}");
            await Program.AwaitPingDelay(Math.Max(0, ExtraDelay() - invokeDelay), tk);
            Ability.UseAbility(target.NetworkPosition);
        }
    }
}