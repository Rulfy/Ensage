namespace InvokerReborn.Items
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;

    using Ensage;
    using Ensage.Common.Extensions;
    using Ensage.Common.Threading;

    using InvokerReborn.Interfaces;

    internal class Sheep : SequenceEntry
    {
        public Sheep(Hero me)
            : base(me, () => 100)
        {
        }

        public Sheep(Hero me, Func<int> extraDelay)
            : base(me, extraDelay)
        {
            this.Ability = me.FindItem("item_sheepstick");
        }

        public override SequenceEntryID ID { get; } = SequenceEntryID.Sheep;

        public override bool IsSkilled
        {
            get
            {
                this.Ability = this.Owner.FindItem("item_sheepstick");
                return this.Ability != null;
            }
        }

        public override async Task ExecuteAsync(Unit target, CancellationToken tk = new CancellationToken())
        {
            await Await.Delay(this.ExtraDelay(), tk);
            this.Ability.UseAbility(target);
        }
    }
}