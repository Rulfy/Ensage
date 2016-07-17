namespace Evade.Obstacles
{
    public interface IObstacle
    {
        uint GetHandle();

        // ReSharper disable once InconsistentNaming
        uint ID { get; }
    }
}
