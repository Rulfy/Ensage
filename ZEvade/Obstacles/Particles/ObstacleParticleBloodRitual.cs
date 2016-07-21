using System;
using System.Linq;

namespace Evade.Obstacles.Particles
{
    using Ensage;
    using Ensage.Common.Extensions;

    using SharpDX;

    public sealed class ObstacleParticleBloodRitual : ObstacleParticle
    {
        /*
        * 0 == position
        */
        public ObstacleParticleBloodRitual(NavMeshPathfinding pathfinding, Entity owner, ParticleEffect particleEffect)
            : base(0, owner, particleEffect)
        {
            var ability =
                ObjectManager.GetEntities<Ability>()
                    .FirstOrDefault(x => x.ClassID == ClassID.CDOTA_Ability_Bloodseeker_Bloodbath);

            Radius = ability?.GetRadius(ability.Name) ?? 600;
            _delay = 2.6f;
            _delay = ability?.AbilitySpecialData.FirstOrDefault(x => x.Name == "delay")?.Value ?? 2.6f;

            ID = pathfinding.AddObstacle(Position, Radius);
            Debugging.WriteLine("Adding BloodRitual particle: {0}",Radius);

        }

        private readonly float _delay;
        public override bool IsLine => false;

        public override Vector3 Position => ParticleEffect.GetControlPoint(0);

        public override float Radius { get; }

        public override float TimeLeft => Math.Max(0, (Started + _delay) - Game.RawGameTime);
    }
}
