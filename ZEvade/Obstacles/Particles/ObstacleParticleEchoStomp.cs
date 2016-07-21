using System;
using System.Linq;

namespace Evade.Obstacles.Particles
{
    using Ensage;
    using Ensage.Common.Extensions;

    using SharpDX;

    public sealed class ObstacleParticleEchoStomp : ObstacleParticle
    {
        /*
        * 0 == position
        */
        public ObstacleParticleEchoStomp(NavMeshPathfinding pathfinding, Entity owner, ParticleEffect particleEffect)
            : base(0, owner, particleEffect)
        {
            var ability =
                ObjectManager.GetEntities<Ability>()
                    .FirstOrDefault(x => x.Name == "elder_titan_echo_stomp");

            Radius = ability?.GetRadius(ability.Name) ?? 500;
            _delay = 1.6f;
            _delay = ability?.AbilitySpecialData.FirstOrDefault(x => x.Name == "cast_time")?.Value ?? 1.6f;

            ID = pathfinding.AddObstacle(Position, Radius);
            Debugging.WriteLine("Adding EchoStomp particle: {0}",Radius);

        }

        private readonly float _delay;
        public override bool IsLine => false;

        public override Vector3 Position => ParticleEffect.GetControlPoint(0);

        public override float Radius { get; }

        public override float TimeLeft => Math.Max(0, (Started + _delay) - Game.RawGameTime);
    }
}
