using System;
using System.Linq;

namespace Evade.Obstacles.Particles
{
    using Ensage;
    using Ensage.Common.Extensions;

    using SharpDX;

    public sealed class ObstacleParticleCallDown : ObstacleParticle
    {
        /*
        * 0 == StartPosition
        * 1 == EndPosition
        * 3 == CurrenPosition
        */
        public ObstacleParticleCallDown(NavMeshPathfinding pathfinding, Entity owner, ParticleEffect particleEffect, bool first)
            : base(0, owner, particleEffect)
        {
            var ability =
                ObjectManager.GetEntities<Ability>()
                    .FirstOrDefault(x => x.ClassID == ClassID.CDOTA_Ability_Gyrocopter_Call_Down);

            Radius = ability?.GetRadius(ability.Name) ?? 600;
            if (first)
            {
                _delay = 2.0f;
                _delay = ability?.AbilitySpecialData.FirstOrDefault(x => x.Name == "slow_duration_first")?.Value ?? 2.0f;
            }
            else
            {
                _delay = 4.0f;
                _delay = ability?.AbilitySpecialData.FirstOrDefault(x => x.Name == "slow_duration_second")?.Value ?? 4.0f;
            }

            ID = pathfinding.AddObstacle(Position, Radius);
            Debugging.WriteLine("Adding CallDown particle: {0}",Radius);

        }

        private readonly float _delay;
        public override bool IsLine => false;

        public override Vector3 Position => ParticleEffect.GetControlPoint(1);

        public override float Radius { get; }

        public override float TimeLeft => Math.Max(0, (Started + _delay) - Game.RawGameTime);

        public override bool IsValid => base.IsValid && Game.RawGameTime < Started + _delay;
    }
}
