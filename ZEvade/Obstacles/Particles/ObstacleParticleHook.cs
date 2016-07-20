using System.Linq;

namespace Evade.Obstacles.Particles
{
    using Ensage;
    using Ensage.Common.Extensions;

    using SharpDX;

    public sealed class ObstacleParticleHook : ObstacleParticle
    {
        public ObstacleParticleHook( NavMeshPathfinding pathfinding, Entity owner, ParticleEffect particleEffect)
            : base(0, owner, particleEffect)
        {
            var ability =
                ObjectManager.GetEntities<Ability>()
                    .FirstOrDefault(x => x.ClassID == ClassID.CDOTA_Ability_Pudge_MeatHook);
            Radius = ability?.GetRadius(ability.Name) ?? 100;

            ID = pathfinding.AddObstacle(Position, EndPosition, Radius);
            Debugging.WriteLine("Adding Hook particle: {0}", Radius);
        }

        public override bool IsLine => true;

        public override Vector3 Position => ParticleEffect.GetControlPoint(0);

        public override Vector3 EndPosition => ParticleEffect.GetControlPoint(1);

        public override float Radius { get; }
    }
}
