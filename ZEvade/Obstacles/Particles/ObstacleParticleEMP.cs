using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Evade.Obstacles.Particles
{
    using Ensage;
    using Ensage.Common.Extensions;

    using SharpDX;

    public sealed class ObstacleParticleEMP : ObstacleParticle
    {
        public ObstacleParticleEMP(NavMeshPathfinding pathfinding, Entity owner, ParticleEffect particleEffect)
            : base(0, owner, particleEffect)
        {
            var ability =
                ObjectManager.GetEntities<Ability>()
                    .FirstOrDefault(x => x.ClassID == ClassID.CDOTA_Ability_Invoker_EMP);
            Radius = ability?.GetRadius(ability.Name) ?? 675;

            startTime = Game.RawGameTime;
            delay = 2.9f;
            delay = ability?.AbilitySpecialData.FirstOrDefault(x => x.Name == "delay")?.Value ?? 2.9f;
           
            ID = pathfinding.AddObstacle(Position, Radius);
            Debugging.WriteLine("Adding EMP particle: {0} {1}",Radius, delay);

        }

        private readonly float startTime;
        private readonly float delay;
        public override bool IsLine => false;

        public override Vector3 Position => ParticleEffect.GetControlPoint(0);

        public override float Radius { get; }

        public override bool IsValid => base.IsValid && Game.RawGameTime <= (startTime + delay);
    }
}
