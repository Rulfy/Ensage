using System;
using System.Linq;

namespace Evade.Obstacles.Particles
{
    using Ensage;
    using Ensage.Common.Extensions;

    using SharpDX;

    public sealed class ObstacleParticleTimberChain : ObstacleParticle
    {
        /*
         * Control Points
         * 0 == StartPosition
         * 1 == EndPosition
         * 2 == Speed in X
         */
        public ObstacleParticleTimberChain(NavMeshPathfinding pathfinding, Entity owner, ParticleEffect particleEffect)
            : base(0, owner, particleEffect)
        {
            var ability =
                ObjectManager.GetEntities<Ability>()
                    .FirstOrDefault(x => x.ClassID == ClassID.CDOTA_Ability_Shredder_TimberChain);
            Radius = ability?.GetRadius(ability.Name) + 8 ?? 98;
            if (ability != null && ability.Level > 0)
            {
                _range = ability.GetRange(ability.Level - 1) + Radius;
            }
            ID = pathfinding.AddObstacle(Position, EndPosition, Radius);
            Debugging.WriteLine("Adding TimberChain particle: {0} - {1}", Radius, _range);
        }

        private readonly float _range = 700;
        public override bool IsLine => true;

        public override Vector3 Position => ParticleEffect.GetControlPoint(0);

        public override Vector3 EndPosition
        {
            get
            {
                Console.WriteLine("{0} - {1}", ParticleEffect.GetControlPoint(2), ParticleEffect.GetControlPoint(3));
                var result = ParticleEffect.GetControlPoint(1);
                var direction = result - Position;
                direction.Normalize();
                direction *= Radius;
                return result + direction;
            }
        }

        private float Speed => ParticleEffect.GetControlPoint(2).X;

        public override Vector3 CurrentPosition
        {
            get
            {
                var result = Position;
                var end = EndPosition;
                var direction = end - result;
                direction.Normalize();
                direction *= Speed * (Game.RawGameTime - Started);
                result += direction;

                return (result- Position).LengthSquared() <= (EndPosition-Position).LengthSquared() ? result : EndPosition;
            }
        }

        public override float Radius { get; }

        public override float TimeLeft => Math.Max(0, (Started + _range / Speed) - Game.RawGameTime);
    }
}
