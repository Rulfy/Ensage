using System;
using System.Linq;

namespace Evade.Obstacles.Particles
{
    using Ensage;
    using Ensage.Common.Extensions;

    using SharpDX;

    public sealed class ObstacleParticleCarrionSwarm : ObstacleParticle
    {
        /*
         * Control Points
         * 0 == StartPosition
         * 3 == CurrentPosition
         */
        public ObstacleParticleCarrionSwarm(NavMeshPathfinding pathfinding, Entity owner, ParticleEffect particleEffect)
            : base(0, owner, particleEffect)
        {
            var ability =
                ObjectManager.GetEntities<Ability>()
                    .FirstOrDefault(x => x.ClassID == ClassID.CDOTA_Ability_DeathProphet_CarrionSwarm);
            Radius = ability?.GetRadius(ability.Name) ?? 300;

            var special = ability?.AbilitySpecialData.FirstOrDefault(x => x.Name == "start_radius");
            if (special != null)
                _startRadius = special.Value;
        
            var special1 = ability?.AbilitySpecialData.FirstOrDefault(x => x.Name == "range");
            var special2 = ability?.AbilitySpecialData.FirstOrDefault(x => x.Name == "speed");
            if (special1 != null && special2 != null)
            {
                _range = special1.Value + Radius + _startRadius;

                _speed = special2.Value;
                _delay = _range / _speed;
            }

            ID = pathfinding.AddObstacle(Position, EndPosition, Radius);
            Debugging.WriteLine("Adding CarionSwarm particle: {0} - {1}", Radius,_delay);
        }

        private readonly float _startRadius = 110;
        private readonly float _speed = 1100;
        private readonly float _range = 810 + 300 + 110;
        private readonly float _delay = 810.0f / 1100.0f;

        public override bool IsLine => true;

        public override Vector3 Position
        {
            get
            {
                return ParticleEffect.GetControlPoint(0);
                //var startPosition = ParticleEffect.GetControlPoint(0);
                //var direction = CurrentPosition - startPosition;
                //direction.Normalize();
                //direction *= _speed * (Game.RawGameTime - Started);
                //return startPosition + direction;
            }
        }

        public override Vector3 CurrentPosition => ParticleEffect.GetControlPoint(3);

        public override Vector3 EndPosition
        {
            get
            {
                var startPosition = ParticleEffect.GetControlPoint(0);
                var direction = CurrentPosition - startPosition;
                direction.Normalize();
                direction *= _range;
                return startPosition + direction;
            }
        } 
        public override float Radius { get; }

        public override bool IsValid => base.IsValid && Game.RawGameTime < Started + _delay;

        public override float TimeLeft => Math.Max(0, (Started + _delay) - Game.RawGameTime);
    }
}
