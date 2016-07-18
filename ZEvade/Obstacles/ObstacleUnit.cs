namespace Evade.Obstacles
{
    using Ensage;

    public class ObstacleUnit : IObstacle
    {
        public ObstacleUnit(uint id, Unit unit)
        {
            ID = id;
            Unit = unit;
        }

        public uint ID { get; }
        public Unit Unit { get; }
        public uint GetHandle()
        {
            return Unit.Handle;
        }
    }
}
