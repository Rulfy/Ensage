namespace SpacebarToFarm
{
    using Ensage;

    public class HealthEntry
    {
        #region Constructors and Destructors

        public HealthEntry(int health)
        {
            Health = health;
            Time = Game.RawGameTime;
        }

        public HealthEntry(int health, float time)
        {
            Health = health;
            Time = Game.RawGameTime + time;
        }

        #endregion

        #region Public Properties

        public int Health { get; }

        public float Time { get; }

        #endregion
    }
}