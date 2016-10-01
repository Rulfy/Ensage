using Ensage;

namespace SpacebarToFarm
{
    public class HealthEntry
    {
        public HealthEntry(int health)
        {
            Health = health;
            Time = Game.RawGameTime;
        }

        public int Health { get; }
        public float Time { get; }
    }
}
