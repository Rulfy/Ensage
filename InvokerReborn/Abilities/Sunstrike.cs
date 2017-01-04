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

    using Prediction = InvokerReborn.Prediction.Prediction;

    internal class Sunstrike : InvokerComboAbility
    {
        private static readonly ILog Log = AssemblyLogs.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private readonly Ability _exort;

        public Sunstrike(Hero me)
            : this(me, () => 100)
        {
        }

        public Sunstrike(Hero me, Func<int> extraDelay)
            : base(me, extraDelay)
        {
            this.Ability = me.FindSpell("invoker_sun_strike");
            this._exort = me.Spellbook.SpellE;
        }

        public override float Damage
        {
            get
            {
                var level = this._exort.Level - (this.Owner.HasItem(ClassID.CDOTA_Item_UltimateScepter) ? 0 : 1);
                return (int)this.Ability.AbilitySpecialData.First(x => x.Name == "damage").GetValue((uint)level);
            }
        }

        public override int Delay => (int)(this.Ability.AbilitySpecialData.First(x => x.Name == "delay").Value * 1000);

        // 1.7
        public override SequenceEntryID ID => SequenceEntryID.Sunstrike;

        public override bool IsSkilled => this.Owner.Spellbook.SpellE.Level > 0;

        public override async Task ExecuteAsync(Unit target, CancellationToken tk = default(CancellationToken))
        {
            var invokeDelay = await this.UseInvokeAbilityAsync(target, tk);
            Log.Debug($"Sunstrike {this.ExtraDelay()} - {invokeDelay}");
            await Await.Delay(Math.Max(0, this.ExtraDelay() - invokeDelay), tk);
            this.Ability.UseAbility(Prediction.PredictPosition(target, this.Delay));
        }

        // TODO: spell damage amp
        public override async Task<int> InvokeAbility(
            bool useCooldown,
            CancellationToken tk = default(CancellationToken))
        {
            // E E E
            return await this.InvokeAbility(new[] { this._exort, this._exort, this._exort }, useCooldown, tk);
        }
    }
}