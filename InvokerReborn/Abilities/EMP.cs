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

    // ReSharper disable once InconsistentNaming
    internal class EMP : InvokerComboAbility
    {
        private static readonly ILog Log = AssemblyLogs.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private readonly Ability _wex;

        public EMP(Hero me)
            : this(me, () => 100)
        {
        }

        public EMP(Hero me, Func<int> extraDelay)
            : base(me, extraDelay)
        {
            this.Ability = me.FindSpell("invoker_emp");

            this._wex = me.Spellbook.SpellW;
        }

        public override int Delay => (int)this.Ability.AbilitySpecialData.First(x => x.Name == "delay").Value * 1000;

        // 2.9
        public override SequenceEntryID ID { get; } = SequenceEntryID.EMP;

        public override bool IsSkilled => this.Owner.Spellbook.SpellW.Level > 0;

        public override async Task ExecuteAsync(Unit target, CancellationToken tk = default(CancellationToken))
        {
            var invokeDelay = await this.UseInvokeAbilityAsync(target, tk);
            Log.Debug($"EMP {this.ExtraDelay()} - {invokeDelay}");
            await Await.Delay(Math.Max(0, this.ExtraDelay() - invokeDelay), tk);
            this.Ability.UseAbility(target.NetworkPosition);
        }

        public override async Task<int> InvokeAbility(
            bool useCooldown,
            CancellationToken tk = default(CancellationToken))
        {
            // W W W
            return await this.InvokeAbility(new[] { this._wex, this._wex, this._wex }, useCooldown, tk);
        }
    }
}