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

namespace Zaio.Heroes
{
    [Hero(ClassID.CDOTA_Unit_Hero_PhantomAssassin)]
    internal class PhantomAssassin : ComboHero
    {
        private static readonly ILog Log = AssemblyLogs.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private static readonly string[] SupportedAbilities =
        {
            "phantom_assassin_stifling_dagger",
            "phantom_assassin_phantom_strike"
        };

        private static readonly string[] KillstealAbilities =
        {
            "phantom_assassin_stifling_dagger"
        };

        private Ability _blinkAbility;

        private Ability _daggerAbility;

        public override void OnLoad()
        {
            base.OnLoad();

            var heroMenu = new Menu("Phantom Assassin", "zaioPhantomAssassink", false, "npc_dota_hero_phantom_assassin",
                true);

            heroMenu.AddItem(new MenuItem("zaioPhantomAssassinAbilitiesText", "Supported Abilities"));
            var supportedStuff = new MenuItem("zaioPhantomAssassinAbilities", string.Empty);
            supportedStuff.SetValue(new AbilityToggler(SupportedAbilities.ToDictionary(x => x, y => true)));
            heroMenu.AddItem(supportedStuff);

            heroMenu.AddItem(new MenuItem("zaioPhantomAssassinKillstealAbilitiesText", "Supported Killsteal Abilities"));
            var supportedKillsteal = new MenuItem("zaioPhantomAssassinKillstealAbilities", string.Empty);
            supportedKillsteal.SetValue(new AbilityToggler(KillstealAbilities.ToDictionary(x => x, y => true)));
            heroMenu.AddItem(supportedKillsteal);

            OnLoadMenuItems(supportedStuff, supportedKillsteal);

            ZaioMenu.LoadHeroSettings(heroMenu);

            _daggerAbility = MyHero.GetAbilityById(AbilityId.phantom_assassin_stifling_dagger);
            _blinkAbility = MyHero.GetAbilityById(AbilityId.phantom_assassin_phantom_strike);
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

            if (_daggerAbility.IsKillstealAbilityEnabled() && _daggerAbility.CanBeCasted())
            {
                var damage = _daggerAbility.GetAbilityData("base_damage");
                var factor = _daggerAbility.GetAbilityData("attack_factor");
                damage += (100.0f + factor) / 100.0f * (MyHero.MinimumDamage + MyHero.BonusDamage); // "-75 -60 -45 -30"
                damage *= GetSpellAmp();
                var enemy =
                    ObjectManager.GetEntitiesParallel<Hero>()
                                 .FirstOrDefault(
                                     x =>
                                         x.IsAlive && x.Team != MyHero.Team && !x.IsIllusion &&
                                         _daggerAbility.CanBeCasted(x) &&
                                         _daggerAbility.CanHit(x) && !x.IsMagicImmune() &&
                                         x.Health < damage * (1 - x.PhysicalResistance()) &&
                                         !x.IsLinkensProtected() && !x.CantBeAttacked() && !x.CantBeKilled());
                if (enemy != null)
                {
                    Log.Debug(
                        $"use killsteal dagger because enough damage {enemy.Health} <= {damage * (1 - enemy.PhysicalResistance())} ");
                    _daggerAbility.UseAbility(enemy);
                    await Await.Delay(GetAbilityDelay(enemy, _daggerAbility));
                    return true;
                }
            }

            return false;
        }

        public override async Task ExecuteComboAsync(Unit target, CancellationToken tk = new CancellationToken())
        {
            if (!MyHero.IsSilenced())
            {
                if (_daggerAbility.IsAbilityEnabled() && _daggerAbility.CanBeCasted(target) && _daggerAbility.CanHit(target))
                {
                    _daggerAbility.UseAbility(target);
                    Log.Debug($"using dagger!");
                    await Await.Delay(GetAbilityDelay(target, _daggerAbility), tk);
                }

                if (_blinkAbility.IsAbilityEnabled() && _blinkAbility.CanBeCasted(target) && _blinkAbility.CanHit(target))
                {
                    _blinkAbility.UseAbility(target);
                    Log.Debug($"using blink!");
                    await Await.Delay(GetAbilityDelay(target, _blinkAbility), tk);
                }
            }

            // make him disabled
            if (await DisableEnemy(target, tk) == DisabledState.UsedAbilityToDisable)
            {
                Log.Debug($"disabled!");
                // return;
            }

            await UseItems(target, tk);

            // check if we are near the enemy
            if (!await MoveOrBlinkToEnemy(target, tk))
            {
                Log.Debug($"return because of blink");
                return;
            }

            if (ZaioMenu.ShouldUseOrbwalker)
            {
                Orbwalk();
                Log.Debug($"orbwalking");
            }
            else
            {
                MyHero.Attack(target);
                await Await.Delay(125, tk);
            }
        }
    }
}