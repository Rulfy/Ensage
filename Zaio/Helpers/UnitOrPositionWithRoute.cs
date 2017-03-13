using System.Collections.Generic;
using SharpDX;

namespace Zaio.Helpers
{
    public class UnitOrPositionWithRoute
    {
        public UnitOrPosition UPos { get; private set; }
        public List<Vector3> Route { get; private set; }

        public UnitOrPositionWithRoute(UnitOrPosition up, List<Vector3> route)
        {
            UPos = up;
            Route = route;
        }
    }
}
