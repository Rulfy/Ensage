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

namespace InvokerReborn.Abilities
{
    internal class Sunstrike : InvokerComboAbility
    {
        private static readonly ILog Log = AssemblyLogs.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private readonly Ability _exort;

        public Sunstrike(Hero me) : this(me, () => 100)
        {
        }

        public Sunstrike(Hero me, Func<int> extraDelay) : base(me, extraDelay)
        {
            Ability = me.FindSpell("invoker_sun_strike");
            _exort = me.Spellbook.SpellE;
        }

        public override SequenceEntryID ID => SequenceEntryID.Sunstrike;
        public override bool IsSkilled => Owner.Spellbook.SpellE.Level > 0;

        public override int Delay => (int) (Ability.AbilitySpecialData.First(x => x.Name == "delay").Value*1000); // 1.7

        public override float Damage
        {
            get
            {
                var level = _exort.Level - (Owner.HasItem(ClassID.CDOTA_Item_UltimateScepter) ? 0 : 1);
                return (int)Ability.AbilitySpecialData.First(x => x.Name == "damage").GetValue((uint)level);
            }
        }

        // TODO: spell damage amp

        public override async Task<int> InvokeAbility(bool useCooldown,
            CancellationToken tk = default(CancellationToken))
        {
            // E E E
            return await InvokeAbility(new[] {_exort, _exort, _exort}, useCooldown, tk);
        }

        public override async Task ExecuteAsync(Unit target, CancellationToken tk = default(CancellationToken))
        {
            var invokeDelay = await UseInvokeAbilityAsync(target, tk);
            Log.Debug($"Sunstrike {ExtraDelay()} - {invokeDelay}");
            await Await.Delay(Math.Max(0, ExtraDelay() - invokeDelay), tk);
            Ability.UseAbility(target.NetworkPosition);
        }
    }
}