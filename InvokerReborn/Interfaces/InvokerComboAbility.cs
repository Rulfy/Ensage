namespace InvokerReborn.Interfaces
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;
    using System.Threading;
    using System.Threading.Tasks;

    using Ensage;
    using Ensage.Common.Extensions;
    using Ensage.Common.Threading;

    using log4net;

    using PlaySharp.Toolkit.Logging;

    public abstract class InvokerComboAbility : SequenceEntry
    {
        private static readonly ILog Log = AssemblyLogs.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        protected Ability Invoke;

        protected InvokerComboAbility(Hero me)
            : base(me)
        {
            this.Invoke = me.Spellbook.SpellR;
        }

        protected InvokerComboAbility(Hero me, Func<int> extraDelay)
            : base(me, extraDelay)
        {
            this.Invoke = me.Spellbook.SpellR;
        }

        public int InvokeCooldown
        {
            get
            {
                if (this.Owner.HasItem(ClassID.CDOTA_Item_UltimateScepter))
                {
                    var special = this.Invoke.AbilitySpecialData.First(x => x.Name == "cooldown_scepter");
                    return (int)(special.GetValue(this.Invoke.Level - 1) * 1000);
                }

                return (int)(this.Invoke.GetCooldown(this.Invoke.Level - 1) * 1000);
            }
        }

        public bool IsInvokeReady => (this.Invoke.Cooldown <= 0) && (this.Invoke.ManaCost <= this.Owner.Mana);

        public async Task<int> InvokeAbility(CancellationToken tk = default(CancellationToken))
        {
            return await this.InvokeAbility(false, tk);
        }

        public abstract Task<int> InvokeAbility(bool useCooldown, CancellationToken tk = default(CancellationToken));

        protected async Task<int> InvokeAbility(
            IEnumerable<Ability> abilities,
            bool useCooldown = false,
            CancellationToken tk = default(CancellationToken))
        {
            var wait = 0;
            if (!this.IsInvokeReady)
            {
                Log.Debug($"Invoke not ready {this.Invoke.Cooldown} - {this.Invoke.ManaCost <= this.Owner.Mana}");

                // return false;
                wait = await Await.Delay(100 + (int)(this.Invoke.Cooldown * 1000), tk);
            }
            else
            {
                await Await.Delay(100, tk);
            }

            foreach (var ability in abilities)
            {
                ability.UseAbility();
            }

            this.Invoke.UseAbility();
            Log.Debug($"Wait after Invoke {100 + (useCooldown ? this.InvokeCooldown : 0)}");
            return wait + await Await.Delay(100, tk);
        }

        protected async Task<int> UseInvokeAbilityAsync(Unit target, CancellationToken tk = default(CancellationToken))
        {
            if (this.Ability.IsHidden)
            {
                var wait1 = await Await.Delay(100, tk);
                Log.Debug($"Invoke {this.Ability.Name} - {100 + this.InvokeCooldown}- {100 + this.Invoke.Cooldown}");

                // ReSharper disable once CompareOfFloatsByEqualityOperator
                var hasCd = this.Invoke.Cooldown != 0;
                var wait2 = await this.InvokeAbility(hasCd, tk);
                return wait1 + (hasCd ? this.InvokeCooldown : 0) + wait2; // InvokeCooldown;
            }

            return 0;
        }
    }
}