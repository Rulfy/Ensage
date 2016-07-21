using System.Linq;

namespace Evade.Obstacles.Particles
{
    using Ensage;
    using Ensage.Common.Extensions;

    using SharpDX;

    public sealed class ObstacleParticleChakram : ObstacleParticle
    {
        /*
        * 0 == position
        * 1 == radius
        * 3 == some other position nearby
        */
        public ObstacleParticleChakram(NavMeshPathfinding pathfinding, Entity owner, ParticleEffect particleEffect)
            : base(0, owner, particleEffect)
        {
            var ability =
                ObjectManager.GetEntities<Ability>()
                    .FirstOrDefault(x => x.ClassID == ClassID.CDOTA_Ability_Shredder_Chakram);

            Radius = ability?.GetRadius(ability.Name) ?? 675;

            ID = pathfinding.AddObstacle(Position, Radius);
            Debugging.WriteLine("Adding Chakram particle: {0}",Radius);

        }

        public override bool IsLine => false;

        public override Vector3 Position => ParticleEffect.GetControlPoint(0);

        public override float Radius { get; }

        public override float TimeLeft => 10;
    }
}
