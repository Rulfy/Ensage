using System.Linq;
namespace Evade.Obstacles.Particles
{
    using Ensage;
    using Ensage.Common.Extensions;
    using SharpDX;
    public sealed class ObstacleParticleFireSpirit : ObstacleParticle
    {
        public ObstacleParticleFireSpirit(NavMeshPathfinding pathfinding, Entity owner, ParticleEffect particleEffect)
            : base(0, owner, particleEffect)
        {
            var ability =
                ObjectManager.GetEntities<Ability>()
                    .FirstOrDefault(x => x.ClassID == ClassID.CDOTA_Ability_Phoenix_LaunchFireSpirit);
            Radius = ability?.GetRadius(ability.Name) ?? 175;
            ID = pathfinding.AddObstacle(Position, Radius);
            Debugging.WriteLine("Adding FireSpirit particle: {0}", Radius);
        }
        public override bool IsLine => false;
        public override Vector3 Position => ParticleEffect.GetControlPoint(0) + ParticleEffect.GetControlPoint(1); // TODO: correct direction but wrong length :/
        public override float Radius { get; }
    }
}