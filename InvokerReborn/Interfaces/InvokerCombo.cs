namespace InvokerReborn.Interfaces
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using System.Windows.Input;

    using Ensage;
    using Ensage.Common;
    using Ensage.Common.Combo;
    using Ensage.Common.Threading;

    using SharpDX;

    using Prediction = InvokerReborn.Prediction.Prediction;

    public abstract class InvokerCombo : ComboBase
    {
        protected Hero Me;

        protected Unit Target;

        private List<ISequenceEntry> abilitySequence;

        private bool executed;

        protected InvokerCombo(Hero me, Key key)
            : base(key)
        {
            this.AbilitySequence = new List<ISequenceEntry>();
            this.Me = me;

            Drawing.OnDraw += Drawing_OnDraw;
        }

        private void Drawing_OnDraw(EventArgs args)
        {
            if (this.Target == null)
            {
                return;
            }

            Vector2 screenPos;
            if (Drawing.WorldToScreen(this.Target.NetworkPosition, out screenPos))
            {
                Drawing.DrawCircle(screenPos, 100, 12, Color.Yellow);
                Console.WriteLine("Draw0 {0}",screenPos);
            }
            if (Drawing.WorldToScreen(Prediction.PredictPosition(this.Target, 400), out screenPos))
            {
                Drawing.DrawCircle(screenPos, 100, 12, Color.Red);
                Console.WriteLine("Draw1 {0}", screenPos);
            }
            if (Drawing.WorldToScreen(Prediction.PredictPosition(this.Target, 1700), out screenPos))
            {
                Drawing.DrawCircle(screenPos, 100, 12, Color.Blue);
                Console.WriteLine("Draw2 {0}", screenPos);
            }
        }

        public bool IsComboReady { get; private set; }

        public bool IsInvoked => this.Abilities.OfType<InvokerComboAbility>().Take(2).All(x => !x.Ability.IsHidden);

        public bool IsReady
            =>
            this.Abilities.TrueForAll(x => x.Ability.Cooldown <= 0)
            && (this.Me.Mana >= this.Abilities.Sum(x => x.Ability.ManaCost));

        public bool IsSkilled => this.Abilities.TrueForAll(x => x.IsSkilled);

        protected List<SequenceEntry> Abilities { get; set; }

        protected List<ISequenceEntry> AbilitySequence
        {
            get
            {
                return this.abilitySequence;
            }

            set
            {
                this.abilitySequence = value;
                this.Abilities = value.OfType<SequenceEntry>().ToList();
            }
        }

        protected virtual int EngageRange => (int)this.Abilities.First().Ability.CastRange;

        public void OnComboCheck()
        {
            this.IsComboReady = this.Abilities.TrueForAll(x => x.IsSkilled);
        }

        public async Task PrepareCombo(CancellationToken tk = default(CancellationToken))
        {
            var abilities = this.Abilities.OfType<InvokerComboAbility>().Take(2).ToList();

            // both skilles are already invoked
            var isPrepared = abilities.All(x => !x.Ability.IsHidden);
            if (isPrepared)
            {
                await Await.Delay(250, tk);
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
                await Await.Delay(250, tk);
                return;
            }

            Console.WriteLine("invoke 2 with true");

            // need to invoke both skills
            await abilities[0].InvokeAbility(true);
            await abilities[1].InvokeAbility(true);
            await Await.Delay(250, tk);
        }

        public void SetKey(Key key)
        {
            this.Key = key;
        }

        protected override bool CanExecute()
        {
            if (Game.IsPaused || !this.Me.IsAlive)
            {
                return false;
            }

            if (!base.CanExecute())
            {
                this.executed = false;
                return false;
            }

            return !this.executed;
        }

        protected override async Task Execute(CancellationToken token)
        {
            if (!this.IsComboReady)
            {
                return;
            }

            if (InvokerMenu.IsPrepareKeyPressed || !this.IsInvoked)
            {
                await this.PrepareCombo();
                return;
            }

            if (!this.IsReady)
            {
                return;
            }

            this.Target = TargetSelector.ClosestToMouse(this.Me);
            if (this.Target == null)
            {
                return;
            }



            foreach (var comboAbility in this.AbilitySequence)
            {
                await comboAbility.ExecuteAsync(this.Target, token);
            }

            await Await.Delay(250, token);
            this.executed = true;
        }
    }
}