namespace SpacebarToFarm.Interfaces.Units
{
    using System.Linq;

    using Ensage;

    class FarmForgeSpirit : FarmUnitRanged
    {
        #region Fields

        private readonly Ability _ability;

        private readonly Ability _exortAbility;

        private readonly Ability _quasAbility;

        private readonly float[] _staticDamage = { 22, 32, 42, 52, 62, 72, 82, 92 };

        private readonly float[] _staticRange = { 300, 365, 430, 495, 560, 625, 690, 755 };

        #endregion

        #region Constructors and Destructors

        public FarmForgeSpirit(Unit controlledUnit)
            : base(controlledUnit)
        {
            _ability = ObjectManager.GetEntitiesParallel<Ability>()
                .FirstOrDefault(x => x.Name == "invoker_forge_spirit");
            _quasAbility = ObjectManager.GetEntitiesParallel<Ability>().FirstOrDefault(x => x.Name == "invoker_quas");
            _exortAbility = ObjectManager.GetEntitiesParallel<Ability>().FirstOrDefault(x => x.Name == "invoker_exort");
        }

        #endregion

        #region Properties

        protected override float AttackRange
        {
            get
            {
                if (_ability == null || _quasAbility == null) return 0;

                var special = _ability.AbilitySpecialData.FirstOrDefault(x => x.Name == "spirit_attack_range");
                return special?.GetValue(_quasAbility.Level - 1) ?? _staticRange[_quasAbility.Level - 1];
            }
        }

        #endregion

        #region Methods

        protected override float GetAttackDamage(Unit target)
        {
            if (_ability == null || _quasAbility == null) return 0;

            var special = _ability.AbilitySpecialData.FirstOrDefault(x => x.Name == "spirit_damage");
            var attackDamage = special?.GetValue(_exortAbility.Level - 1) ?? _staticDamage[_exortAbility.Level - 1];

            // armor
            attackDamage *= (1.0f - target.DamageResist);

            return attackDamage;
        }

        #endregion
    }
}