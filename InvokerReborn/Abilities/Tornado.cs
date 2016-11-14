using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Ensage;
using Ensage.Common.Extensions;
using Ensage.Common.Threading;
using InvokerReborn.Interfaces;

namespace InvokerReborn.Abilities
{
    internal class Tornado : InvokerComboAbility
    {
        private readonly Ability _quas;
        private readonly Ability _wex;

        public Tornado(Hero me) : this(me, () => 100)
        {
        }

        public Tornado(Hero me, Func<int> extraDelay) : base(me, extraDelay)
        {
            Ability = me.FindSpell("invoker_tornado");

            _quas = me.Spellbook.SpellQ;
            _wex = me.Spellbook.SpellW;
        }

        public override SequenceEntryID ID { get; } = SequenceEntryID.Tornado;
        public override bool IsSkilled => (Owner.Spellbook.SpellQ.Level > 0) && (Owner.Spellbook.SpellW.Level > 0);

        public override int Duration
        {
            get
            {
                var level = _quas.Level - (Owner.HasItem(ClassID.CDOTA_Item_UltimateScepter) ? 0 : 1);
                return (int)Ability.AbilitySpecialData.First(x => x.Name == "lift_duration").GetValue((uint)level);
            }
        }
       

        public int Distance
        {
            get
            {
                var level = _wex.Level - (Owner.HasItem(ClassID.CDOTA_Item_UltimateScepter) ? 0 : 1);
                return (int)Ability.AbilitySpecialData.First(x => x.Name == "travel_distance").GetValue((uint)level);
            }
        }

        // "0.8 1.1 1.4 1.7 2.0 2.3 2.6 2.9"

        public override async Task<int> InvokeAbility(bool useCooldown,
            CancellationToken tk = default(CancellationToken))
        {
            // W W Q
            return await InvokeAbility(new[] {_wex, _wex, _quas}, useCooldown, tk);
        }

        public override async Task ExecuteAsync(Unit target, CancellationToken tk = default(CancellationToken))
        {
            var invokeDelay = await UseInvokeAbilityAsync(target, tk);
            await Await.Delay(Math.Max(0, ExtraDelay() - invokeDelay), tk);
            Ability.UseAbility(target.NetworkPosition);
        }
    }
}