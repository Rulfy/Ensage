namespace InvokerReborn.Items
{
    using System;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;

    using Ensage;
    using Ensage.Common.Extensions;
    using Ensage.Common.Threading;

    using InvokerReborn.Interfaces;

    internal class Euls : SequenceEntry
    {
        public Euls(Hero me)
            : this(me, () => 100)
        {
        }

        public Euls(Hero me, Func<int> extraDelay)
            : base(me, extraDelay)
        {
            this.Ability = me.FindItem("item_cyclone");
        }

        public override int Duration
            => (int)(this.Ability.AbilitySpecialData.First(x => x.Name == "cyclone_duration").Value * 1000); // 2.5

        public override SequenceEntryID ID => SequenceEntryID.Euls;

        public override bool IsSkilled
        {
            get
            {
                this.Ability = this.Owner.FindItem("item_cyclone");
                return this.Ability != null;
            }
        }

        public override async Task ExecuteAsync(Unit target, CancellationToken tk = default(CancellationToken))
        {
            await Await.Delay(this.ExtraDelay(), tk);
            this.Ability.UseAbility(target);
        }
    }
}