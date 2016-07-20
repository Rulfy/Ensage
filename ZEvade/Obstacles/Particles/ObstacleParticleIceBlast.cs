
using System.Linq;
namespace Evade.Obstacles.Particles
{
    using Ensage;
    using Ensage.Common.Extensions;
    using SharpDX;
    public sealed class ObstacleParticleIceBlast : ObstacleParticle
    {
        public ObstacleParticleIceBlast(NavMeshPathfinding pathfinding, Entity owner, ParticleEffect particleEffect)
            : base(0, owner, particleEffect)
        {
            var ability =
                ObjectManager.GetEntities<Ability>()
                    .FirstOrDefault(x => x.ClassID == ClassID.CDOTA_Ability_AncientApparition_IceBlast);
            Radius = ability?.GetRadius(ability.Name) ?? 600;
            ID = pathfinding.AddObstacle(Position, Radius);
            Debugging.WriteLine("Adding IceBlast particle: {0}", Radius);
        }
        public override bool IsLine => false;

        public override Vector3 Position
        {
            get
            {
                var result = ParticleEffect.GetControlPoint(0) + ParticleEffect.GetControlPoint(1);
                var tmp = ParticleEffect.GetControlPoint(5);
                tmp.X += 1;
                tmp.Y += 1;
                tmp.Z += 1;
                result *= tmp;
                return result;
            }
        }
        public override float Radius { get; }


        /**
         * ControlPoint(3) == Projectile
         * 
         */
    }
}
