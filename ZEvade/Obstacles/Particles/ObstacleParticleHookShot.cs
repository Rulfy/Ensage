using System;
using System.Linq;

namespace Evade.Obstacles.Particles
{
    using Ensage;
    using Ensage.Common.Extensions;

    using SharpDX;

    public sealed class ObstacleParticleHookShot : ObstacleParticle
    {
        /*
         * Control Points
         * 0 == StartPosition
         * 1 == EndPosition
         * 2 == Speed in X
         */
        public ObstacleParticleHookShot( NavMeshPathfinding pathfinding, Entity owner, ParticleEffect particleEffect)
            : base(0, owner, particleEffect)
        {
            var ability =
                ObjectManager.GetEntities<Ability>()
                    .FirstOrDefault(x => x.ClassID == ClassID.CDOTA_Ability_Rattletrap_Hookshot);
            Radius = ability?.GetRadius(ability.Name) ?? 125;
            if (ability != null && ability.Level > 0)
            {
                _range = ability.GetRange(ability.Level - 1) + Radius;
            }
            
            var special = ability?.AbilitySpecialData.FirstOrDefault(x => x.Name == "speed");
            if (special != null)
            {
                _speed = special.Value;
            }
            _delay = _range / _speed;
            ID = pathfinding.AddObstacle(Position, EndPosition, Radius);
            Debugging.WriteLine("Adding Hook particle: {0} - {1}", Radius, _range);
        }

        private readonly float _delay;
        private readonly float _speed = 4000;
        private readonly float _range = 2000;

        public override bool IsLine => true;

        public override Vector3 Position => ParticleEffect.GetControlPoint(0);

        public override Vector3 EndPosition
        {
            get
            {
                var result = ParticleEffect.GetControlPoint(1);
                var direction = result - Position;
                direction.Normalize();
                direction *= Radius;
                return result + direction;
            }
        }

        public override Vector3 CurrentPosition
        {
            get
            {
                var result = Position;
                var end = EndPosition;
                var direction = end - result;
                direction.Normalize();
                direction *= _speed * (Game.RawGameTime - Started);
                result += direction;

                return (result - Position).LengthSquared() <= (EndPosition - Position).LengthSquared() ? result : EndPosition;
            }
        }

        public override float Radius { get; }

        public override float TimeLeft => Math.Max(0, (Started + _range/_speed) - Game.RawGameTime);

        public override bool IsValid => base.IsValid && Game.RawGameTime < Started + _delay;
    }
}
