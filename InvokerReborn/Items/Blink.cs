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

    using SharpDX;

    internal class Blink : SequenceEntry
    {
        private readonly Func<int> engageRange;

        public Blink(Hero me, Func<int> engageRange)
            : this(me, () => 100, engageRange)
        {
        }

        public Blink(Hero me, Func<int> extraDelay, Func<int> engageRange)
            : base(me, extraDelay)
        {
            this.engageRange = engageRange;
            this.Ability = me.FindItem("item_blink");
        }

        public int Distance => (int)this.Ability.AbilitySpecialData.First(x => x.Name == "blink_range").Value;

        public int EngageRange => this.engageRange();

        public override SequenceEntryID ID { get; } = SequenceEntryID.Blink;

        public override bool IsSkilled
        {
            get
            {
                this.Ability = this.Owner.FindItem("item_blink");
                return this.Ability != null;
            }
        }

        public override async Task ExecuteAsync(Unit target, CancellationToken tk = new CancellationToken())
        {
            await this.ExecuteAsync(target.NetworkPosition, tk);
        }

        public async Task ExecuteAsync(Vector3 target, CancellationToken tk = new CancellationToken())
        {
            if (this.Owner.Distance2D(target) <= this.EngageRange)
            {
                return;
            }

            await Await.Delay(this.ExtraDelay(), tk);

            var pos = target - this.Owner.NetworkPosition;
            pos.Normalize();
            pos *= -InvokerMenu.SafeDistance;
            pos = target + pos;

            this.Ability.UseAbility(pos);
        }
    }
}