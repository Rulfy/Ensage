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

    internal class DeafeningBlast : InvokerComboAbility
    {
        private static readonly ILog Log = AssemblyLogs.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private readonly Ability _exort;

        private readonly Ability _quas;

        private readonly Ability _wex;

        public DeafeningBlast(Hero me)
            : this(me, () => 100)
        {
        }

        public DeafeningBlast(Hero me, Func<int> extraDelay)
            : base(me, extraDelay)
        {
            this.Ability = me.FindSpell("invoker_deafening_blast");

            this._quas = me.Spellbook.SpellQ;
            this._wex = me.Spellbook.SpellW;
            this._exort = me.Spellbook.SpellE;
        }

        public override SequenceEntryID ID { get; } = SequenceEntryID.DeafeningBlast;

        public override bool IsSkilled
            =>
            (this.Owner.Spellbook.SpellQ.Level > 0) && (this.Owner.Spellbook.SpellW.Level > 0)
            && (this.Owner.Spellbook.SpellE.Level > 0);

        public override async Task ExecuteAsync(Unit target, CancellationToken tk = default(CancellationToken))
        {
            var invokeDelay = await this.UseInvokeAbilityAsync(target, tk);
            Log.Debug($"DeafeningBlast {this.ExtraDelay()} - {invokeDelay}");
            await Await.Delay(Math.Max(0, this.ExtraDelay() - invokeDelay), tk);
            this.Ability.UseAbility(target.NetworkPosition);
        }

        public override async Task<int> InvokeAbility(
            bool useCooldown,
            CancellationToken tk = default(CancellationToken))
        {
            // Q W E
            return await this.InvokeAbility(new[] { this._quas, this._wex, this._exort }, useCooldown, tk);
        }
    }
}