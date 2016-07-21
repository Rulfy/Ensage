namespace Evade.Obstacles
{
    using Ensage;

    using SharpDX;

    public abstract class ObstacleParticle : IObstacle
    {
        public ObstacleParticle(uint id, Entity owner, ParticleEffect particleEffect)
        {
            ID = id;
            Entity = owner;
            ParticleEffect = particleEffect;
            Started = Game.RawGameTime;
        }


        public uint GetHandle()
        {
            return ParticleEffect.Handle ^ Entity.Handle;
        }

        public uint ID { get;  protected set; }
        protected float Started { get; }
        public Entity Entity { get; }
        public ParticleEffect ParticleEffect { get; }
        public abstract bool IsLine { get; }
        public abstract Vector3 Position { get; }
        public virtual Vector3 EndPosition => Position;
        public virtual Vector3 CurrentPosition => Position;
        public abstract float Radius { get; }
        public virtual bool IsValid => ParticleEffect.IsValid;
        public virtual float TimeLeft => 1;
        public virtual bool UseCurrentPosition => true;
    }
}
