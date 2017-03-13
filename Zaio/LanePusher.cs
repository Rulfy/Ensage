using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Ensage;
using Ensage.Common.Threading;
using SharpDX;
using Zaio.Helpers;

namespace Zaio
{
    public class LanePusher : IDisposable
    {
        private readonly ParticleEffect _effect;

        private float _gameTime;

        public UnitOrPosition CurrentTargetPosition;

        public LanePusher(Unit unit, UnitOrPosition currentTarget, List<Vector3> creepPositions)
        {
            Unit = unit;
            CurrentTargetPosition = currentTarget;
            LaneRoute = creepPositions;

            _effect = Unit.AddParticleEffect(@"particles\ui_mouseactions\range_finder_tower_aoe.vpcf");
            _effect.SetControlPoint(2, Unit.NetworkPosition); //start point XYZ
            _effect.SetControlPoint(6, new Vector3(1, 0, 0)); // 1 means the particle is visible
            _effect.SetControlPoint(7, currentTarget.Position); //end point XYZ  

            _gameTime = Game.GameTime;
        }

        public Vector3 TargetPosition => CurrentTargetPosition.Position;

        public List<Vector3> LaneRoute { get; }

        public Vector3 NextLanePosition
        {
            get
            {
                var nextLanePos = TargetPosition;
                var minDistance = float.MaxValue;
                for (var i = 0; i < LaneRoute.Count - 1; ++i)
                {
                    var distance = Unit.GetShortestDistance(LaneRoute[i], LaneRoute[i + 1]);
                    if (distance < minDistance)
                    {
                        nextLanePos = LaneRoute[i];
                        minDistance = distance;
                    }
                }
                return nextLanePos;
            }
        }

        public Unit Unit { get; }

        public void Dispose()
        {
            _effect.Dispose();
        }

        public async Task RefreshCommand()
        {
            if (!CurrentTargetPosition.HasUnit)
            {
                return;
            }

            var gameTime = Game.GameTime;
            if (gameTime - _gameTime <= 3)
            {
                return;
            }

            _gameTime = gameTime;
            Unit.Move(CurrentTargetPosition.Position);
            Unit.Attack(CurrentTargetPosition.Position, true);
            await Await.Delay(50);
        }

        public void UpdateParticleEffect()
        {
            _effect.SetControlPoint(2, Unit.NetworkPosition);
            _effect.SetControlPoint(7, CurrentTargetPosition.Position);
        }
    }
}