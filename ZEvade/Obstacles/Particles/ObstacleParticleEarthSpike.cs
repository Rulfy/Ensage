using System.Linq;
namespace Evade.Obstacles.Particles
{
    using System;

    using Ensage;
    using Ensage.Common.Extensions;
    using Ensage.Common.Extensions.SharpDX;

    using SharpDX;
    public sealed class ObstacleParticleEarthSpike: ObstacleParticle
    {
        public ObstacleParticleEarthSpike(NavMeshPathfinding pathfinding, Entity owner, ParticleEffect particleEffect)
            : base(0, owner, particleEffect)
        {
            var ability =
                ObjectManager.GetEntities<Ability>()
                    .FirstOrDefault(x => x.ClassID == ClassID.CDOTA_Ability_Lion_Impale);

            Radius = ability?.GetRadius(ability.Name) ?? 125;
            Debugging.WriteLine("Adding EarthSpike particle: {0}", Radius);
        }

        public override bool IsLine => true;
        public override Vector3 Position => ParticleEffect.Position;

        public override Vector3 EndPosition
        {
            get
            {
                Vector3 v1, v2, v3;
                if (ParticleEffect.GetControlPointOrientation(0, out v1, out v2, out v3))
                {
                    Vector3 result = Position;
                    result.Normalize();
                    var mtx = new Matrix3x3(v1.X,v1.Y,0,-v2.X,-v2.Y,0,v3.X,v3.Y,0);
                    result = Vector3.Transform(result, mtx);
                    result = result.Rotated(MathUtil.DegreesToRadians(-15));
                    result *= 700.0f;
                    return Position + result;
                }
                return Position;
            }
        }
        public override float Radius { get; }
    }
}