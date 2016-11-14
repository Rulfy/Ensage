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

namespace InvokerReborn
{
    public class Core : IHandle<ChangedKeyMessage>
    {
        private static readonly ILog Log = AssemblyLogs.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private readonly Dictionary<string, InvokerCombo> _availableCombos = new Dictionary<string, InvokerCombo>();
        private readonly GhostWalk _ghostWalk;
        private readonly Ability _invokedBase;
        private readonly Hero _me;
        private readonly Sunstrike _sunstrike;
        private InvokerCombo _activeCombo;

        public Core()
        {
            //AssemblyLogs.GLboal..

            _me = ObjectManager.LocalHero;

            InvokerMenu.BuildMenu();
            InvokerMenu.EventAggregator.Subscribe(this);
            InvokerMenu.ActiveComboChanged += InvokerMenu_ActiveComboChanged;
            InvokerMenu.GhostWalkKeyPressed += InvokerMenu_GhostWalkKeyPressed;

            _sunstrike = new Sunstrike(_me);
            _ghostWalk = new GhostWalk(_me);

            _invokedBase =
                _me.Spellbook.Spells.FirstOrDefault(x => x.ClassID == ClassID.CDOTA_Ability_Invoker_InvokedBase);

            // add combos
            _availableCombos.Add("EulsSSMeteorBlast", new EulsSSMeteorBlast(_me, InvokerMenu.ComboKey));
            _availableCombos.Add("AlaForSnap", new AlaForSnap(_me, InvokerMenu.ComboKey));
            _availableCombos.Add("Assassination", new AssassinationCombo(_me, InvokerMenu.ComboKey));
            _availableCombos.Add("ToEMPMetBlastRefMetBlastEMP",
                new ToEMPMetBlastRefMetBlastEMP(_me, InvokerMenu.ComboKey));

            _activeCombo = _availableCombos[InvokerMenu.CurrentlyActiveCombo];
            _activeCombo.Activate();

            CheckCombos();

            Entity.OnInt32PropertyChange += Entity_OnInt32PropertyChange;
            Entity.OnHandlePropertyChange += Entity_OnHandlePropertyChange;

            GameDispatcher.OnIngameUpdate += GameDispatcher_OnIngameUpdate;
        }

        public void Handle(ChangedKeyMessage message)
        {
            if (message.MenuItemName == InvokerMenu.ComboName)
                foreach (var availableCombo in _availableCombos)
                    availableCombo.Value.SetKey(message.Key);
        }

        private void InvokerMenu_GhostWalkKeyPressed(object sender, BoolEventArgs e)
        {
            if (!e.Value)
                return;
            if (_me.IsAlive)
                return;
            if (_ghostWalk.IsSkilled && _ghostWalk.Ability.CanBeCasted() && (!_ghostWalk.Ability.IsHidden || _ghostWalk.IsInvokeReady))
#pragma warning disable 4014
                _ghostWalk.ExecuteAsync(null);
#pragma warning restore 4014

        }

        private async void GameDispatcher_OnIngameUpdate(EventArgs args)
        {
            if (!Utils.SleepCheck("coreTick") || Game.IsPaused || _activeCombo.IsRunning)
                return;
            Utils.Sleep(125, "coreTick");

            var killSteal = InvokerMenu.SunStrikeKillSteal;
            var autoKill = InvokerMenu.SunStrikeAutoKill;
            var safeCast = InvokerMenu.SunStrikeSafeCast;

            if (!killSteal && !autoKill && !safeCast)
                return;

            if (!_me.IsAlive || !_sunstrike.IsSkilled || (_sunstrike.Ability.Cooldown > 0))
                return;

            var heroes =
                ObjectManager.GetEntitiesParallel<Hero>()
                    .Where(x => x.IsAlive && x.IsVisible && (x.Team != _me.Team));

            if (killSteal)
            {
                var target = heroes.Where(x => x.Health < _sunstrike.Damage).OrderBy(x => x.Health).FirstOrDefault();
                if (target != null)
                {
                    await UseSunstrike(target);
                    return;
                }
            }
            else if (autoKill)
            {
                var allies =
                    ObjectManager.GetEntitiesParallel<Hero>()
                        .Where(x => x.IsAlive && (x.Team == _me.Team) && (x != _me));

                var target =
                    heroes.Where(x => (x.Health < _sunstrike.Damage) && allies.All(y => x.Distance2D(y) > 500))
                        .OrderBy(x => x.Health)
                        .FirstOrDefault();
                if (target != null)
                {
                    await UseSunstrike(target);
                    return;
                }
            }
            if (safeCast)
            {
                var target =
                    heroes.Where(
                        x =>
                            Utils.DisableDuration(x, "modifier_invoker_deafening_blast_knockback")*1000 >
                            _sunstrike.Delay).OrderBy(x => x.Health).FirstOrDefault();
                if (target != null)
                    await
                        UseSunstrike(target,
                            (int) (Utils.DisableDuration(target, "modifier_invoker_deafening_blast_knockback")*1000) -
                            _sunstrike.Delay);
                        // TODO: most stuns don't need to wait until they're over (but astral, disruption) etc..)
            }
        }

        private void InvokerMenu_ActiveComboChanged(object sender, StringEventArgs e)
        {
            _activeCombo.Deactivate();
            _activeCombo = _availableCombos[e.Value];
            _activeCombo.Activate();
        }


        private void Entity_OnHandlePropertyChange(Entity sender, HandlePropertyChangeEventArgs args)
        {
            var hero = sender as Hero;
            // check if we bought a new item
            if ((hero != null) && (hero == _me) && (args.PropertyName == "m_hItems"))
                DelayAction.Add(250, CheckCombos);
        }

        private void Entity_OnInt32PropertyChange(Entity sender, Int32PropertyChangeEventArgs args)
        {
            var ability = sender as Ability;
            // check if Q,W,E has been skilled the first time
            if ((ability != null) && (args.OldValue == -1) && (ability == _invokedBase)
                
                /*&& (args.PropertyName == "m_nQuasLevel" || args.PropertyName == "m_nWexLevel" || args.PropertyName == "m_nExortLevel")*/)
                DelayAction.Add(250, CheckCombos);
        }

        private void CheckCombos()
        {
            foreach (var availableCombo in _availableCombos)
                availableCombo.Value.OnComboCheck();
        }

        private async Task UseSunstrike(Unit target, int extraDelay = 0)
        {
            Utils.Sleep(125 + extraDelay, "coreTick");

            Log.Debug($"Use Auto Sunstrike {target.Name} | {extraDelay}");
            _sunstrike.ExtraDelay = () => extraDelay;
            await _sunstrike.ExecuteAsync(target);
        }
    }
}