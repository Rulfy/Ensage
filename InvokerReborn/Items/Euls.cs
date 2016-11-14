using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Ensage;
using Ensage.Common.Extensions;
using InvokerReborn.Interfaces;

namespace InvokerReborn.Items
{
    internal class Euls : SequenceEntry
    {
        public Euls(Hero me) : this(me, () => 100)
        {
        }

        public Euls(Hero me, Func<int> extraDelay) : base(me, extraDelay)
        {
            Ability = me.FindItem("item_cyclone");
        }

        public override SequenceEntryID ID => SequenceEntryID.Euls;

        public override bool IsSkilled
        {
            get
            {
                Ability = Owner.FindItem("item_cyclone");
                return Ability != null;
            }
        }

        public override int Duration
            => (int) (Ability.AbilitySpecialData.First(x => x.Name == "cyclone_duration").Value*1000); // 2.5

        public override async Task ExecuteAsync(Unit target, CancellationToken tk = default(CancellationToken))
        {
            await Program.AwaitPingDelay(ExtraDelay(), tk);
            Ability.UseAbility(target);
        }
    }
}