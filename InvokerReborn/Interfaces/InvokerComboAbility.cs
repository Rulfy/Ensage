using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Ensage;
using Ensage.Common.Extensions;
using log4net;
using PlaySharp.Toolkit.Logging;

namespace InvokerReborn.Interfaces
{
    public abstract class InvokerComboAbility : SequenceEntry
    {
        private static readonly ILog Log = AssemblyLogs.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        protected Ability Invoke;

        protected InvokerComboAbility(Hero me) : base(me)
        {
            Invoke = me.Spellbook.SpellR;
        }

        protected InvokerComboAbility(Hero me, Func<int> extraDelay) : base(me, extraDelay)
        {
            Invoke = me.Spellbook.SpellR;
        }

        public bool IsInvokeReady => (Invoke.Cooldown <= 0) && (Invoke.ManaCost <= Owner.Mana);

        public int InvokeCooldown
        {
            get
            {
                if (Owner.HasItem(ClassID.CDOTA_Item_UltimateScepter))
                {
                    var special = Invoke.AbilitySpecialData.First(x => x.Name == "cooldown_scepter");
                    return (int) (special.GetValue(Invoke.Level - 1)*1000);
                }
                return (int) (Invoke.GetCooldown(Invoke.Level - 1)*1000);
            }
        }

        public async Task<int> InvokeAbility(CancellationToken tk = default(CancellationToken))
        {
            return await InvokeAbility(false, tk);
        }

        public abstract Task<int> InvokeAbility(bool useCooldown, CancellationToken tk = default(CancellationToken));

        protected async Task<int> InvokeAbility(IEnumerable<Ability> abilities, bool useCooldown = false,
            CancellationToken tk = default(CancellationToken))
        {
            var wait = 0;
            if (!IsInvokeReady)
            {
                Log.Debug($"Invoke not ready {Invoke.Cooldown} - {Invoke.ManaCost <= Owner.Mana}");
                //return false;
                wait = await Program.AwaitPingDelay(100 + (int) (Invoke.Cooldown*1000), tk);
            }


            foreach (var ability in abilities)
                ability.UseAbility();
            Invoke.UseAbility();
            Log.Debug($"Wait after Invoke {100 + (useCooldown ? InvokeCooldown : 0)}");
            return wait + await Program.AwaitPingDelay(100, tk);
        }

        protected async Task<int> UseInvokeAbilityAsync(Unit target, CancellationToken tk = default(CancellationToken))
        {
            if (Ability.IsHidden)
            {
                var wait1 = await Program.AwaitPingDelay(100, tk);
                Log.Debug($"Invoke {Ability.Name} - {100 + InvokeCooldown}- {100 + Invoke.Cooldown}");
                // ReSharper disable once CompareOfFloatsByEqualityOperator
                var hasCd = Invoke.Cooldown != 0;
                var wait2 = await InvokeAbility(hasCd, tk);
                return wait1 + (hasCd ? InvokeCooldown : 0) + wait2; // InvokeCooldown;
            }
            return 0;
        }
    }
}