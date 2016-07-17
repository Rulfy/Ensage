namespace Evade
{
    using Ensage;

    public static class UnitExtensions
    {
        public static float GetPossibleTravelDistance(this Unit entity, float time)
        {
            return time * entity.MovementSpeed;
        }
    }
}
