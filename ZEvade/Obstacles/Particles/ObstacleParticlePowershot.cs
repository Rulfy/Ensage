using System.Linq;
namespace Evade.Obstacles.Particles
{
    using Ensage;
    using Ensage.Common.Extensions;
    using SharpDX;
    public sealed class ObstacleParticlePowershot : ObstacleParticle
    {
        public ObstacleParticlePowershot(NavMeshPathfinding pathfinding, Entity owner, ParticleEffect particleEffect)
            : base(0, owner, particleEffect)
        {
            var ability =
                ObjectManager.GetEntities<Ability>()
                    .FirstOrDefault(x => x.ClassID == ClassID.CDOTA_Ability_Windrunner_Powershot);

            Radius = ability?.GetRadius(ability.Name) ?? 125;
            ID = pathfinding.AddObstacle(Position, EndPosition, Radius);
            Debugging.WriteLine("Adding Powershot particle: {0}", Radius);
        }
        public override bool IsLine => true;

        private Vector3 pos1 = Vector3.Zero, pos2 = Vector3.Zero;
        public override Vector3 Position
        {
            get
            {
                if (pos1 == Vector3.Zero) pos1 = ParticleEffect.Position;
                return pos1;
            }
        }

        public override Vector3 EndPosition
        {
            get
            {
                if (pos2 == Vector3.Zero) pos2 = ParticleEffect.GetControlPoint(1);
                return pos2;
            }
        }
        public override float Radius { get; }
    }
}