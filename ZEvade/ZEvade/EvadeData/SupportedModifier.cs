namespace Evade.EvadeData
{
    using Ensage;

    public class SupportedModifier
    {
        // ReSharper disable once InconsistentNaming
        public SupportedModifier(string modifierName, ClassID abilityClassID, bool enemyTeamOnly)
        {
            ModifierName = modifierName;
            AbilityClassID = abilityClassID;
            EnemyTeamOnly = enemyTeamOnly;
        }

        public string ModifierName { get; private set; }
        // ReSharper disable once InconsistentNaming
        public ClassID AbilityClassID { get; private set; }
        public bool EnemyTeamOnly { get; private set; }
    }
}
