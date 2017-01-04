namespace InvokerReborn.Abilities
{
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

    internal class ColdSnap : InvokerComboAbility
    {
        private static readonly ILog Log = AssemblyLogs.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private readonly Ability _quas;

        public ColdSnap(Hero me)
            : this(me, () => 100)
        {
        }

        public ColdSnap(Hero me, Func<int> extraDelay)
            : base(me, extraDelay)
        {
            this.Ability = me.FindSpell("invoker_cold_snap");

            this._quas = me.Spellbook.SpellQ;
        }

        public override SequenceEntryID ID { get; } = SequenceEntryID.ColdSnap;

        public override bool IsSkilled => this.Owner.Spellbook.SpellQ.Level > 0;

        public override async Task ExecuteAsync(Unit target, CancellationToken tk = default(CancellationToken))
        {
            var invokeDelay = await this.UseInvokeAbilityAsync(target, tk);
            Log.Debug($"ColdSnap {this.ExtraDelay()} | {invokeDelay}");
            await Await.Delay(Math.Max(0, this.ExtraDelay() - invokeDelay), tk);
            this.Ability.UseAbility(target);
        }

        public override async Task<int> InvokeAbility(
            bool useCooldown,
            CancellationToken tk = default(CancellationToken))
        {
            // Q Q Q
            return await this.InvokeAbility(new[] { this._quas, this._quas, this._quas }, useCooldown, tk);
        }
    }
}