using System;
using System.Collections.Generic;
using Ensage;
using SharpDX;

namespace Zaio
{
    public class LanePusher : IDisposable
    {
        private readonly ParticleEffect _effect;

        public Vector3 CurrentTargetPosition;
        private List<Vector3> _creepPositions;

        public LanePusher(Unit unit, Vector3 currentTarget/*, List<Vector3> creepPositions*/)
        {
            Unit = unit;
            CurrentTargetPosition = currentTarget;
           // _creepPositions = creepPositions;

            _effect = Unit.AddParticleEffect(@"particles\ui_mouseactions\range_finder_tower_aoe.vpcf");
            _effect.SetControlPoint(2, Unit.NetworkPosition); //start point XYZ
            _effect.SetControlPoint(6, new Vector3(1, 0, 0)); // 1 means the particle is visible
            _effect.SetControlPoint(7, currentTarget); //end point XYZ  
        }

        public Unit Unit { get; }

        public void Dispose()
        {
            _effect.Dispose();
        }

        public void UpdateParticleEffect()
        {
            _effect.SetControlPoint(2, Unit.NetworkPosition);
        }
    }
}