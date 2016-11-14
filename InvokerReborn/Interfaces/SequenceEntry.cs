using System;
using System.Threading;
using System.Threading.Tasks;
using Ensage;

namespace InvokerReborn.Interfaces
{
    public enum SequenceEntryID
    {
        // Abilities
        ColdSnap,
        GhostWalk,
        IceWall,
        Tornado,
        DeafeningBlast,
        ForgeSpirit,
        // ReSharper disable once InconsistentNaming
        EMP,
        Alacrity,
        Meteor,
        Sunstrike,

        // Items
        Euls,
        Refresher,
        Sheep,
        Blink
    }

    public abstract class SequenceEntry : ISequenceEntry
    {
        public Func<int> ExtraDelay;

        public bool IsOptional;

        protected SequenceEntry(Hero me) : this(me, () => 100)
        {
        }

        protected SequenceEntry(Hero me, Func<int> extraDelay)
        {
            ExtraDelay = extraDelay;
            Owner = me;
        }

        // ReSharper disable once InconsistentNaming
        public abstract SequenceEntryID ID { get; }
        public abstract bool IsSkilled { get; }
        public Ability Ability { get; protected set; }
        public Hero Owner { get; }

        public virtual int Duration { get; } = 0;
        public virtual int Delay { get; } = 0;

        public virtual float Damage { get; } = 0;

        public abstract Task ExecuteAsync(Unit target, CancellationToken tk = default(CancellationToken));
    }
}