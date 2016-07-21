using System;
using System.Linq;

namespace Evade.Obstacles.Particles
{
    using Ensage;
    using Ensage.Common.Extensions;

    using SharpDX;

    // ReSharper disable once InconsistentNaming
    public sealed class ObstacleParticleEMP : ObstacleParticle
    {
        public ObstacleParticleEMP(NavMeshPathfinding pathfinding, Entity owner, ParticleEffect particleEffect)
            : base(0, owner, particleEffect)
        {
            var ability =
                ObjectManager.GetEntities<Ability>()
                    .FirstOrDefault(x => x.ClassID == ClassID.CDOTA_Ability_Invoker_EMP);
            Radius = ability?.GetRadius(ability.Name) ?? 675;

            _delay = 2.9f;
            _delay = ability?.AbilitySpecialData.FirstOrDefault(x => x.Name == "delay")?.Value ?? 2.9f;
           
            ID = pathfinding.AddObstacle(Position, Radius);
            Debugging.WriteLine("Adding EMP particle: {0} {1}",Radius, _delay);

        }

        private readonly float _delay;
        public override bool IsLine => false;

        public override Vector3 Position => ParticleEffect.GetControlPoint(0);

        public override float Radius { get; }

        public override bool IsValid => base.IsValid && Game.RawGameTime <= (Started + _delay);

        public override float TimeLeft => Math.Max(0, (Started + _delay) - Game.RawGameTime);
    }
}
