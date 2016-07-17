namespace Evade.Obstacles
{
    using Ensage;

    public class ObstacleModifier : IObstacle
    {
        public ObstacleModifier(uint id, Unit sender, Modifier modifier)
        {
            ID = id;
            Owner = sender;
            Modifier = modifier;
        }

        // ReSharper disable once InconsistentNaming
        public uint ID { get; }
        public Unit Owner { get; }

        public Modifier Modifier { get; }

        public uint GetHandle()
        {
            return Owner.Handle;
        }
    }
}
