using System.Linq;

namespace Evade.Obstacles.Particles
{
    using System.Net.Http.Headers;

    using Ensage;
    using Ensage.Common.Extensions;

    using SharpDX;

    public sealed class ObstacleParticleCarrionSwarm : ObstacleParticle
    {
        public ObstacleParticleCarrionSwarm(NavMeshPathfinding pathfinding, Entity owner, ParticleEffect particleEffect)
            : base(0, owner, particleEffect)
        {
            var ability =
                ObjectManager.GetEntities<Ability>()
                    .FirstOrDefault(x => x.ClassID == ClassID.CDOTA_Ability_DeathProphet_CarrionSwarm);
            Radius = ability?.GetRadius(ability.Name) ?? 300;

            startTime = Game.RawGameTime;
            delay = 810.0f / 1100.0f;

            var special1 = ability?.AbilitySpecialData.FirstOrDefault(x => x.Name == "range");
            var special2 = ability?.AbilitySpecialData.FirstOrDefault(x => x.Name == "speed");
            if (special1 != null && special2 != null)
            {
                delay = special1.Value / special2.Value;
            }

            ID = pathfinding.AddObstacle(Position, EndPosition, Radius);
            Debugging.WriteLine("Adding CarionSwarm particle: {0} - {1}", Radius,delay);
        }

        private float startTime;

        private float delay;

        public override bool IsLine => true;

        public override Vector3 Position => ParticleEffect.GetControlPoint(0);
        public override Vector3 EndPosition => ParticleEffect.GetControlPoint(3);
        public override float Radius { get; }

        public override bool IsValid => base.IsValid && Game.RawGameTime < startTime + delay;
    }
}
