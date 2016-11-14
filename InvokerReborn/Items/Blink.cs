using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Ensage;
using Ensage.Common.Extensions;
using InvokerReborn.Interfaces;
using SharpDX;

namespace InvokerReborn.Items
{
    internal class Blink : SequenceEntry
    {
        private readonly Func<int> _engageRange;
        public int EngageRange => _engageRange();

        public Blink(Hero me, Func<int> engageRange) : this(me, () => 100, engageRange)
        {
        }

        public Blink(Hero me, Func<int> extraDelay, Func<int> engageRange) : base(me, extraDelay)
        {
            _engageRange = engageRange;
            Ability = me.FindItem("item_blink");
        }

        public override SequenceEntryID ID { get; } = SequenceEntryID.Blink;

        public override bool IsSkilled
        {
            get
            {
                Ability = Owner.FindItem("item_blink");
                return Ability != null;
            }
        }

        public int Distance => (int) Ability.AbilitySpecialData.First(x => x.Name == "blink_range").Value;

        public override async Task ExecuteAsync(Unit target, CancellationToken tk = new CancellationToken())
        {
            await ExecuteAsync(target.NetworkPosition, tk);
        }

        public async Task ExecuteAsync(Vector3 target, CancellationToken tk = new CancellationToken())
        {
            if (Owner.Distance2D(target) <= EngageRange)
                return;

            await Program.AwaitPingDelay(ExtraDelay(), tk);

            var pos = target - Owner.NetworkPosition;
            pos.Normalize();
            pos *= -InvokerMenu.SafeDistance;
            pos = target + pos;

            Ability.UseAbility(pos);
        }
    }
}