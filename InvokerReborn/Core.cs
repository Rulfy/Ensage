namespace InvokerReborn
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;
    using System.Threading.Tasks;

    using Ensage;
    using Ensage.Common;
    using Ensage.Common.Extensions;
    using Ensage.Common.Threading;

    using InvokerReborn.Abilities;
    using InvokerReborn.Combos;
    using InvokerReborn.Interfaces;

    using log4net;

    using PlaySharp.Toolkit.EventAggregator;
    using PlaySharp.Toolkit.Logging;

    using Prediction = InvokerReborn.Prediction.Prediction;


    public class Core : IHandle<ChangedKeyMessage>
    {
        private static readonly ILog Log = AssemblyLogs.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private readonly Dictionary<string, InvokerCombo> availableCombos = new Dictionary<string, InvokerCombo>();

        private readonly GhostWalk ghostWalk;

        private readonly Ability invokedBase;

        private readonly Hero me;

        private readonly Sunstrike sunstrike;

        private InvokerCombo activeCombo;

        public Core()
        {
            // AssemblyLogs.GLboal..
            this.me = ObjectManager.LocalHero;

            InvokerMenu.BuildMenu();
            InvokerMenu.EventAggregator.Subscribe(this);
            InvokerMenu.ActiveComboChanged += this.InvokerMenu_ActiveComboChanged;
            InvokerMenu.GhostWalkKeyPressed += this.InvokerMenu_GhostWalkKeyPressed;

            this.sunstrike = new Sunstrike(this.me);
            this.ghostWalk = new GhostWalk(this.me);

            this.invokedBase =
                this.me.Spellbook.Spells.FirstOrDefault(x => x.ClassID == ClassID.CDOTA_Ability_Invoker_InvokedBase);

            // add combos
            this.availableCombos.Add("EulsSSMeteorBlast", new EulsSSMeteorBlast(this.me, InvokerMenu.ComboKey));
            this.availableCombos.Add("AlaForSnap", new AlaForSnap(this.me, InvokerMenu.ComboKey));
            this.availableCombos.Add("Assassination", new AssassinationCombo(this.me, InvokerMenu.ComboKey));
            this.availableCombos.Add(
                    "ToEMPMetBlastRefMetBlastEMP",
                    new ToEMPMetBlastRefMetBlastEMP(this.me, InvokerMenu.ComboKey));

            this.activeCombo = this.availableCombos[InvokerMenu.CurrentlyActiveCombo];
            this.activeCombo.Activate();

            this.CheckCombos();

            Entity.OnInt32PropertyChange += this.Entity_OnInt32PropertyChange;
            Entity.OnHandlePropertyChange += this.Entity_OnHandlePropertyChange;

            GameDispatcher.OnIngameUpdate += this.GameDispatcher_OnIngameUpdate;
        }

        public void Handle(ChangedKeyMessage message)
        {
            if (message.MenuItemName == InvokerMenu.ComboName)
            {
                foreach (var availableCombo in this.availableCombos)
                {
                    availableCombo.Value.SetKey(message.Key);
                }
            }
        }

        private void CheckCombos()
        {
            foreach (var availableCombo in this.availableCombos)
            {
                availableCombo.Value.OnComboCheck();
            }
        }

        private void Entity_OnHandlePropertyChange(Entity sender, HandlePropertyChangeEventArgs args)
        {
            var hero = sender as Hero;

            // check if we bought a new item
            if ((hero != null) && (hero == this.me) && (args.PropertyName == "m_hItems"))
            {
                DelayAction.Add(250, this.CheckCombos);
            }
        }

        private void Entity_OnInt32PropertyChange(Entity sender, Int32PropertyChangeEventArgs args)
        {
            var ability = sender as Ability;

            // check if Q,W,E has been skilled the first time
            if ((ability != null) && (args.OldValue == -1) && (ability == this.invokedBase)
                
                /*&& (args.PropertyName == "m_nQuasLevel" || args.PropertyName == "m_nWexLevel" || args.PropertyName == "m_nExortLevel")*/)
            {
                DelayAction.Add(250, this.CheckCombos);
            }
        }

        private async void GameDispatcher_OnIngameUpdate(EventArgs args)
        {
            if (!Utils.SleepCheck("coreTick") || Game.IsPaused || this.activeCombo.IsRunning)
            {
                return;
            }

            Utils.Sleep(125, "coreTick");

            var killSteal = InvokerMenu.SunStrikeKillSteal;
            var autoKill = InvokerMenu.SunStrikeAutoKill;
            var safeCast = InvokerMenu.SunStrikeSafeCast;

            if (!killSteal && !autoKill && !safeCast)
            {
                return;
            }

            if (!this.me.IsAlive || !this.sunstrike.IsSkilled || (this.sunstrike.Ability.Cooldown > 0))
            {
                return;
            }

            var heroes =
                ObjectManager.GetEntitiesParallel<Hero>()
                             .Where(x => x.IsAlive && x.IsVisible && (x.Team != this.me.Team));

            if (killSteal)
            {
                var target =
                    heroes.Where(x => x.Health < this.sunstrike.Damage).OrderBy(x => x.Health).FirstOrDefault();
                if (target != null)
                {
                    await this.UseSunstrike(target);
                    return;
                }
            }
            else if (autoKill)
            {
                var allies =
                    ObjectManager.GetEntitiesParallel<Hero>()
                                 .Where(x => x.IsAlive && (x.Team == this.me.Team) && (x != this.me));

                var target =
                    heroes.Where(x => (x.Health < this.sunstrike.Damage) && allies.All(y => x.Distance2D(y) > 500))
                          .OrderBy(x => x.Health)
                          .FirstOrDefault();
                if (target != null)
                {
                    await this.UseSunstrike(target);
                    return;
                }
            }

            if (safeCast)
            {
                var target =
                    heroes.Where(
                              x =>
                                  Utils.DisableDuration(x, "modifier_invoker_deafening_blast_knockback") * 1000
                                  > this.sunstrike.Delay).OrderBy(x => x.Health).FirstOrDefault();
                if (target != null)
                {
                    await
                        this.UseSunstrike(
                            target,
                            (int)(Utils.DisableDuration(target, "modifier_invoker_deafening_blast_knockback") * 1000)
                            - this.sunstrike.Delay);
                }

                // TODO: most stuns don't need to wait until they're over (but astral, disruption) etc..)
            }
        }

        private void InvokerMenu_ActiveComboChanged(object sender, StringEventArgs e)
        {
            this.activeCombo.Deactivate();
            this.activeCombo = this.availableCombos[e.Value];
            this.activeCombo.Activate();
        }

        private void InvokerMenu_GhostWalkKeyPressed(object sender, BoolEventArgs e)
        {
            if (!e.Value)
            {
                return;
            }

            if (this.me.IsAlive)
            {
                return;
            }

            if (this.ghostWalk.IsSkilled && this.ghostWalk.Ability.CanBeCasted()
                && (!this.ghostWalk.Ability.IsHidden || this.ghostWalk.IsInvokeReady))
#pragma warning disable 4014
            {
                this.ghostWalk.ExecuteAsync(null);
            }

#pragma warning restore 4014
        }

        private async Task UseSunstrike(Unit target, int extraDelay = 0)
        {
            Utils.Sleep(125 + extraDelay, "coreTick");

            Log.Debug($"Use Auto Sunstrike {target.Name} | {extraDelay}");
            this.sunstrike.ExtraDelay = () => extraDelay;
            await this.sunstrike.ExecuteAsync(target);
        }
    }
}