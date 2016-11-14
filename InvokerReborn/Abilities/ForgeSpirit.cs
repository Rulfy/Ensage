using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Ensage;
using Ensage.Common;
using Ensage.Common.Extensions;
using Ensage.Common.Threading;
using InvokerReborn.Interfaces;

namespace InvokerReborn.Abilities
{
    internal class ForgeSpirit : InvokerComboAbility
    {
        private readonly Ability _exort;
        private readonly Ability _quas;

        public ForgeSpirit(Hero me) : this(me, () => 100)
        {
        }

        public ForgeSpirit(Hero me, Func<int> extraDelay) : base(me, extraDelay)
        {
            Ability = me.FindSpell("invoker_forge_spirit");

            _quas = me.Spellbook.SpellQ;
            _exort = me.Spellbook.SpellE;
        }

        public override SequenceEntryID ID => SequenceEntryID.ForgeSpirit;
        public override bool IsSkilled => (Owner.Spellbook.SpellQ.Level > 0) && (Owner.Spellbook.SpellE.Level > 0);

        public override async Task<int> InvokeAbility(bool useCooldown,
            CancellationToken tk = default(CancellationToken))
        {
            // E E Q
            return await InvokeAbility(new[] {_exort, _exort, _quas}, useCooldown, tk);
        }

        public override async Task ExecuteAsync(Unit target, CancellationToken tk = default(CancellationToken))
        {
            var invokeDelay = await UseInvokeAbilityAsync(target, tk);
            await Await.Delay(Math.Max(0, ExtraDelay() - invokeDelay), tk);
            Ability.UseAbility();

            DelayAction.Add(250, () =>
                    ObjectManager.GetEntitiesFast<Unit>()
                        .Where(x => x.ClassID == ClassID.CDOTA_BaseNPC_Invoker_Forged_Spirit)
                        .ToList().ForEach(x => x.Attack(target))
            );
        }
    }
}