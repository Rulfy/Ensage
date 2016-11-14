using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using Ensage;
using Ensage.Common;
using Ensage.Common.Combo;

namespace InvokerReborn.Interfaces
{
    public abstract class InvokerCombo : ComboBase
    {
        private List<ISequenceEntry> _abilitySequence;
        private bool _executed;

        protected Hero Me;
        protected Unit Target;

        protected InvokerCombo(Hero me, Key key) : base(key)
        {
            AbilitySequence = new List<ISequenceEntry>();
            Me = me;
        }

        protected List<ISequenceEntry> AbilitySequence
        {
            get { return _abilitySequence; }
            set
            {
                _abilitySequence = value;
                Abilities = value.OfType<SequenceEntry>().ToList();
            }
        }

        protected List<SequenceEntry> Abilities { get; set; }

        public bool IsComboReady { get; private set; }


        public bool IsSkilled => Abilities.TrueForAll(x => x.IsSkilled);

        public bool IsReady
            => Abilities.TrueForAll(x => x.Ability.Cooldown <= 0) && (Me.Mana >= Abilities.Sum(x => x.Ability.ManaCost))
            ;

        public bool IsInvoked => Abilities.OfType<InvokerComboAbility>().Take(2).All(x => !x.Ability.IsHidden);

        protected virtual int EngageRange => (int) Abilities.First().Ability.CastRange;

        public void OnComboCheck()
        {
            IsComboReady = Abilities.TrueForAll(x => x.IsSkilled);
        }

        public void SetKey(Key key)
        {
            Key = key;
        }

        public async Task PrepareCombo(CancellationToken tk = default(CancellationToken))
        {
            var abilities = Abilities.OfType<InvokerComboAbility>().Take(2).ToList();

            // both skilles are already invoked
            var isPrepared = abilities.All(x => !x.Ability.IsHidden);
            if (isPrepared)
            {
                await Program.AwaitPingDelay(250, tk);
                return;
            }


            // one skill is already invoked
            var isAnyPrepared = (abilities[0].Ability.IsHidden && !abilities[1].Ability.IsHidden)
                                || (!abilities[0].Ability.IsHidden && abilities[1].Ability.IsHidden);
            if (isAnyPrepared)
            {
                var invokedAbility = abilities.First(x => !x.Ability.IsHidden);
                var hiddenAbility = abilities.First(x => x.Ability.IsHidden);

                await invokedAbility.InvokeAbility();
                await hiddenAbility.InvokeAbility();
                await Program.AwaitPingDelay(250, tk);
                return;
            }
            Console.WriteLine("invoke 2 with true");
            // need to invoke both skills
            await abilities[0].InvokeAbility(true);
            await abilities[1].InvokeAbility(true);
            await Program.AwaitPingDelay(250, tk);
        }

        protected override bool CanExecute()
        {
            if (!base.CanExecute())
            {
                _executed = false;
                return false;
            }
            return !_executed;
        }

        protected override async Task Execute(CancellationToken token)
        {
            if (!IsComboReady)
                return;

            if (InvokerMenu.IsPrepareKeyPressed || !IsInvoked)
            {
                await PrepareCombo();
                return;
            }

            if (!IsReady)
                return;

            Target = TargetSelector.ClosestToMouse(Me);
            if (Target == null)
                return;

            foreach (var comboAbility in AbilitySequence)
                await comboAbility.ExecuteAsync(Target, token);

            await Program.AwaitPingDelay(250, token);
            _executed = true;
        }
    }
}