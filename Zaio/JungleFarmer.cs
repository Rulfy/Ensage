using System;
using Ensage;
using Ensage.Common.Objects;
using SharpDX;

namespace Zaio
{
    public class JungleFarmer : IDisposable
    {
        private ParticleEffect _effect;

        private JungleCamp _camp;

        public JungleFarmer(Unit unit, JungleCamp camp)
        {
            Unit = unit;
            _camp = camp;

            _effect = Unit.AddParticleEffect(@"particles\ui_mouseactions\range_finder_tower_aoe.vpcf");
            _effect.SetControlPoint(2, Unit.NetworkPosition); //start point XYZ
            _effect.SetControlPoint(6, new Vector3(1, 0, 0)); // 1 means the particle is visible
            _effect.SetControlPoint(7, _camp.CampPosition); //end point XYZ  
        }

        public Unit Unit { get; }

        public JungleCamp JungleCamp
        {
            get { return _camp; }
            set
            {
                _camp = value;
                _effect.SetControlPoint(7, _camp.CampPosition);
            }
        }

        public void Dispose()
        {
            _effect.Dispose();
            _effect = null;
        }

        public void UpdateParticleEffect()
        {
            _effect.SetControlPoint(2, Unit.NetworkPosition);
        }
    }
}