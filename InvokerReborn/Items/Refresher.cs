namespace InvokerReborn.Items
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;

    using Ensage;
    using Ensage.Common.Extensions;
    using Ensage.Common.Threading;

    using InvokerReborn.Interfaces;

    internal class Refresher : SequenceEntry
    {
        public Refresher(Hero me)
            : this(me, () => 100)
        {
        }

        public Refresher(Hero me, Func<int> extraDelay)
            : base(me, extraDelay)
        {
            this.Ability = me.FindItem("item_refresher");
        }

        public override SequenceEntryID ID { get; } = SequenceEntryID.Refresher;

        public override bool IsSkilled
        {
            get
            {
                this.Ability = this.Owner.FindItem("item_refresher");
                return this.Ability != null;
            }
        }

        public override async Task ExecuteAsync(Unit target, CancellationToken tk = new CancellationToken())
        {
            await Await.Delay(this.ExtraDelay(), tk);
            this.Ability.UseAbility();
        }
    }
}