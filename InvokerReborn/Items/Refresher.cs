using System;
using System.Threading;
using System.Threading.Tasks;
using Ensage;
using Ensage.Common.Extensions;
using InvokerReborn.Interfaces;

namespace InvokerReborn.Items
{
    internal class Refresher : SequenceEntry
    {
        public Refresher(Hero me) : this(me, () => 100)
        {
        }

        public Refresher(Hero me, Func<int> extraDelay) : base(me, extraDelay)
        {
            Ability = me.FindItem("item_refresher");
        }

        public override SequenceEntryID ID { get; } = SequenceEntryID.Refresher;

        public override bool IsSkilled
        {
            get
            {
                Ability = Owner.FindItem("item_refresher");
                return Ability != null;
            }
        }

        public override async Task ExecuteAsync(Unit target, CancellationToken tk = new CancellationToken())
        {
            await Program.AwaitPingDelay(ExtraDelay(), tk);
            Ability.UseAbility();
        }
    }
}