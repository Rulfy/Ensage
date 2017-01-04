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

    internal class Alacrity : InvokerComboAbility
    {
        private static readonly ILog Log = AssemblyLogs.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private readonly Ability _exort;

        private readonly Ability _wex;

        public Alacrity(Hero me)
            : this(me, () => 100)
        {
        }

        public Alacrity(Hero me, Func<int> extraDelay)
            : base(me, extraDelay)
        {
            this.Ability = me.FindSpell("invoker_alacrity");

            this._wex = me.Spellbook.SpellW;
            this._exort = me.Spellbook.SpellE;
        }

        public override SequenceEntryID ID { get; } = SequenceEntryID.Alacrity;

        public override bool IsSkilled
            => (this.Owner.Spellbook.SpellW.Level > 0) && (this.Owner.Spellbook.SpellE.Level > 0);

        public override async Task ExecuteAsync(Unit target, CancellationToken tk = default(CancellationToken))
        {
            var invokeDelay = await this.UseInvokeAbilityAsync(target, tk);
            Log.Debug($"Alacrity {this.ExtraDelay()} - {invokeDelay}");
            await Await.Delay(Math.Max(0, this.ExtraDelay() - invokeDelay), tk);
            this.Ability.UseAbility(this.Owner);
        }

        public override async Task<int> InvokeAbility(
            bool useCooldown,
            CancellationToken tk = default(CancellationToken))
        {
            // W W E
            return await this.InvokeAbility(new[] { this._wex, this._wex, this._exort }, useCooldown, tk);
        }
    }
}