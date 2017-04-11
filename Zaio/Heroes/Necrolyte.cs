using System;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Ensage;
using Ensage.Common.Enums;
using Ensage.Common.Extensions;
using Ensage.Common.Menu;
using Ensage.Common.Threading;
using log4net;
using PlaySharp.Toolkit.Logging;
using Zaio.Helpers;
using Zaio.Interfaces;
using AbilityId = Ensage.AbilityId;


namespace Zaio.Heroes
{
    [Hero(ClassId.CDOTA_Unit_Hero_Necrolyte)]
    internal class Necrolyte : ComboHero
    {
        private static readonly ILog Log = AssemblyLogs.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private static readonly string[] SupportedAbilities =
        {
            "necrolyte_death_pulse",
            "necrolyte_sadist",
            "necrolyte_reapers_scythe"
        };

        private static readonly string[] KillstealAbilities =
        {
            "necrolyte_reapers_scythe"
        };


        private MenuItem _autoGhost;
        private Ability _ghostAbility;

        private Ability _pulseAbility;
        private Ability _ultAbility;

        private bool ShouldAutoGhost => _autoGhost.GetValue<bool>();

        public override void OnLoad()
        {
            base.OnLoad();

            var heroMenu = new Menu("Necrophos", "zaioNecrophos", false, "npc_dota_hero_necrolyte", true);

            heroMenu.AddItem(new MenuItem("zaioNecrophosAbilitiesText", "Supported Abilities"));
            var supportedStuff = new MenuItem("zaioNecrophosAbilities", string.Empty);
            supportedStuff.SetValue(new AbilityToggler(SupportedAbilities.ToDictionary(x => x, y => true)));
            heroMenu.AddItem(supportedStuff);

            heroMenu.AddItem(new MenuItem("zaioNecrophosKillstealAbilitiesText", "Supported Killsteal Abilities"));
            var supportedKillsteal = new MenuItem("zaioNecrophosKillstealAbilities", string.Empty);
            supportedKillsteal.SetValue(new AbilityToggler(KillstealAbilities.ToDictionary(x => x, y => true)));
            heroMenu.AddItem(supportedKillsteal);

            OnLoadMenuItems(supportedStuff, supportedKillsteal);

            _autoGhost = new MenuItem("zaioNecrophosAutoGhost", "Auto Ghost").SetValue(true);
            _autoGhost.Tooltip = "Will use Ghost Shroud when in danger.";
            heroMenu.AddItem(_autoGhost);

            ZaioMenu.LoadHeroSettings(heroMenu);

            _pulseAbility = MyHero.GetAbilityById(AbilityId.necrolyte_death_pulse);
            _ghostAbility = MyHero.GetAbilityById(AbilityId.necrolyte_sadist);
            _ultAbility = MyHero.GetAbilityById(AbilityId.necrolyte_reapers_scythe);


            GameDispatcher.OnIngameUpdate += GameDispatcher_OnIngameUpdate;
        }


        private async void GameDispatcher_OnIngameUpdate(EventArgs args)
        {
            if (!ShouldAutoGhost || !MyHero.IsAlive || MyHero.IsSilenced())
            {
                Await.Block("zaioNecrophosGhostSleep", MyAsyncHelpers.AsyncSleep);
                return;
            }


            if ( _ghostAbility.CanBeCasted() && !MyHero.IsMagicImmune())
            {
                var enemies =
                    ObjectManager.GetEntitiesParallel<Hero>()
                                 .Where(
                                     x =>
                                         x.IsAlive && x.Team != MyHero.Team &&
                                         x.CanAttack() && !x.CantBeKilled() &&
                                         x.Distance2D(MyHero) <= x.GetAttackRange() + 100 &&
                                         x.IsAttacking(MyHero));

                if (enemies.Count() >= 2 && MyHero.Health != MyHero.MaximumHealth)
                {
                    Log.Debug(
                        $"Using ghost to avoid damage/death by right click {enemies.Count()} >= 2 and {MyHero.Health} != {MyHero.MaximumHealth}");
                    _ghostAbility.UseAbility();
                    Await.Block("zaioNecrophosGhostSleep", MyAsyncHelpers.AsyncSleep);
                }
            }
        }

        protected override async Task<bool> Killsteal()
        {
            if (await base.Killsteal())
            {
                return true;
            }

            if (MyHero.IsSilenced())
            {
                return false;
            }


            if (Target != null)
            {
                return false;
            }

            if (_ultAbility.IsKillstealAbilityEnabled() && _ultAbility.CanBeCasted())
            {
                var damage = _ultAbility.GetAbilityData("damage_per_health");
                damage *= GetSpellAmp();

                var enemy =
                    ObjectManager.GetEntitiesParallel<Hero>()
                                 .FirstOrDefault(
                                     x =>
                                         x.IsAlive && x.Team != MyHero.Team && _ultAbility.CanBeCasted(x) &&
                                         _ultAbility.CanHit(x) && !x.IsIllusion &&
                                         (x.MaximumHealth - x.Health) * damage * (1 - x.MagicDamageResist) >= x.Health &&
                                         !x.CantBeAttacked() &&
                                         !x.CantBeKilled() && !x.IsLinkensProtected());

                if (enemy != null)
                {
                    Log.Debug(
                        $"use ulti for killsteal because {(enemy.MaximumHealth - enemy.Health) * damage * (1 - enemy.MagicDamageResist)} >= {enemy.Health}");
                    _ultAbility.UseAbility(enemy);
                    await Await.Delay(GetAbilityDelay(enemy, _ultAbility));
                    return true;
                }
            }

            return false;
        }

        public override async Task ExecuteComboAsync(Unit target, CancellationToken tk = new CancellationToken())
        {
            if (!await MoveOrBlinkToEnemy(target, tk)) //We want to initiate with blink first
            {
                return;
            }
            await HasNoLinkens(target, tk);
            await UseItems(target, tk); //then use items to maximize ulti damage
            await DisableEnemy(target, tk);

            if (!MyHero.IsSilenced())
            {
                if (_pulseAbility.CanBeCasted() && _pulseAbility.CanHit(target) &&
                    MyHero.Distance2D(target) <= _pulseAbility.GetAbilityData("area_of_effect") &&
                    (!_ultAbility.IsAbilityEnabled() || MyHero.Mana - _pulseAbility.ManaCost >= _ultAbility.ManaCost))
                {
                    Log.Debug($"using pulse to deal damage target");
                    _pulseAbility.UseAbility();
                    await Await.Delay(100, tk);
                }

                if (_ultAbility.IsAbilityEnabled() && _ultAbility.CanBeCasted(target) && _ultAbility.CanHit(target) &&
                    await HasNoLinkens(target, tk))
                {
                    var damage = _ultAbility.GetAbilityData("damage_per_health");
                    damage *= GetSpellAmp();
                    if (_ultAbility.CanHit(target) &&
                        (target.MaximumHealth - target.Health) * damage * (1 - target.MagicDamageResist) >=
                        target.Health) //Don't waste ulti if target is can't be killed by ulti
                    {
                        Log.Debug($"using ulti to kill enemy. {damage} > {target.Health}");
                        _ultAbility.UseAbility(target);
                        await Await.Delay(GetAbilityDelay(target, _ultAbility) + 250, tk);
                    }
                }
            }


            if (ZaioMenu.ShouldUseOrbwalker)
            {
                Orbwalk();
            }
        }
    }
}