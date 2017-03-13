using Ensage;
using SharpDX;

namespace Zaio.Helpers
{
    public class UnitOrPosition
    {
        private Unit _unit;
        private Vector3 _position;
        public UnitOrPosition(Unit unit)
        {
            _unit = unit;
            _position = unit.NetworkPosition;
        }

        public UnitOrPosition(Vector3 position)
        {
            _position = position;
        }

        public Vector3 Position
        {
            get
            {
                if (_unit == null )
                {
                    return _position;
                }
                else if (!_unit.IsValid)
                {
                    _unit = null;
                    return _position;
                }
                else
                {
                    _position = _unit.NetworkPosition;
                    return _position;
                }
                
            }
        }

        public bool HasUnit => _unit != null;
    }
}
