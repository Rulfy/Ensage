using Ensage;

namespace Zaio.Helpers
{
    public static class MyUnitExtension
    {
        public static bool IsRealUnit2(this Unit unit)
        {
            return unit.UnitType != 0 && !unit.UnitState.HasFlag(UnitState.FakeAlly);
        }
    }
}