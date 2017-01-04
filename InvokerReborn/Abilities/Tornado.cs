namespace InvokerReborn.Abilities
{
    using System;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;

    using Ensage;
    using Ensage.Common.Extensions;
    using Ensage.Common.Threading;

    using InvokerReborn.Interfaces;

    internal class Tornado : InvokerComboAbility
    {
        private readonly Ability _quas;

        private readonly Ability _wex;

        public Tornado(Hero me)
            : this(me, () => 100)
        {
        }

        public Tornado(Hero me, Func<int> extraDelay)
            : base(me, extraDelay)
        {
            this.Ability = me.FindSpell("invoker_tornado");

            this._quas = me.Spellbook.SpellQ;
            this._wex = me.Spellbook.SpellW;
        }

        public int Distance
        {
            get
            {
                var level = this._wex.Level - (this.Owner.HasItem(ClassID.CDOTA_Item_UltimateScepter) ? 0 : 1);
                return
                    (int)this.Ability.AbilitySpecialData.First(x => x.Name == "travel_distance").GetValue((uint)level);
            }
        }

        public override int Duration
        {
            get
            {
                var level = this._quas.Level - (this.Owner.HasItem(ClassID.CDOTA_Item_UltimateScepter) ? 0 : 1);
                return (int)this.Ability.AbilitySpecialData.First(x => x.Name == "lift_duration").GetValue((uint)level);
            }
        }

        public override SequenceEntryID ID { get; } = SequenceEntryID.Tornado;

        public override bool IsSkilled
            => (this.Owner.Spellbook.SpellQ.Level > 0) && (this.Owner.Spellbook.SpellW.Level > 0);

        public override async Task ExecuteAsync(Unit target, CancellationToken tk = default(CancellationToken))
        {
            var invokeDelay = await this.UseInvokeAbilityAsync(target, tk);
            await Await.Delay(Math.Max(0, this.ExtraDelay() - invokeDelay), tk);
            this.Ability.UseAbility(target.NetworkPosition);
        }

        // "0.8 1.1 1.4 1.7 2.0 2.3 2.6 2.9"
        public override async Task<int> InvokeAbility(
            bool useCooldown,
            CancellationToken tk = default(CancellationToken))
        {
            // W W Q
            return await this.InvokeAbility(new[] { this._wex, this._wex, this._quas }, useCooldown, tk);
        }
    }
}