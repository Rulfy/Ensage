namespace InvokerReborn.Abilities
{
    using System;
    using System.Linq;
    using System.Reflection;
    using System.Threading;
    using System.Threading.Tasks;

    using Ensage;
    using Ensage.Common.Extensions;
    using Ensage.Common.Threading;

    using InvokerReborn.Interfaces;

    using log4net;

    using PlaySharp.Toolkit.Logging;

    internal class Meteor : InvokerComboAbility
    {
        private static readonly ILog Log = AssemblyLogs.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private readonly Ability _exort;

        private readonly Ability _wex;

        public Meteor(Hero me)
            : this(me, () => 100)
        {
        }

        public Meteor(Hero me, Func<int> extraDelay)
            : base(me, extraDelay)
        {
            this.Ability = me.FindSpell("invoker_chaos_meteor");

            this._wex = me.Spellbook.SpellW;
            this._exort = me.Spellbook.SpellE;
        }

        public override int Delay
            => (int)(this.Ability.AbilitySpecialData.First(x => x.Name == "land_time").Value * 1000);

        public override SequenceEntryID ID { get; } = SequenceEntryID.Meteor;

        public override bool IsSkilled
            => (this.Owner.Spellbook.SpellW.Level > 0) && (this.Owner.Spellbook.SpellE.Level > 0);

        public override async Task ExecuteAsync(Unit target, CancellationToken tk = default(CancellationToken))
        {
            var invokeDelay = await this.UseInvokeAbilityAsync(target, tk);
            Log.Debug($"Meteor {this.ExtraDelay()} - {invokeDelay}");
            await Await.Delay(Math.Max(0, this.ExtraDelay() - invokeDelay), tk);
            this.Ability.UseAbility(target.NetworkPosition);
        }

        // 1.3
        public override async Task<int> InvokeAbility(
            bool useCooldown,
            CancellationToken tk = default(CancellationToken))
        {
            // E E W
            return await this.InvokeAbility(new[] { this._exort, this._exort, this._wex }, useCooldown, tk);
        }
    }
}