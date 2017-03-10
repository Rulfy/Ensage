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
    [Hero(ClassID.CDOTA_Unit_Hero_Luna)]
    internal class Luna : ComboHero
    {
        private static readonly ILog Log = AssemblyLogs.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private static readonly string[] SupportedAbilities =
        {
            "luna_lucent_beam",
            "luna_eclipse"
        };

        private static readonly string[] KillstealAbilities =
        {
            "luna_lucent_beam"
        };

        private MenuItem _minimumEnemyUltCount;
        private int EnemyCountForUlt => _minimumEnemyUltCount.GetValue<Slider>().Value;

        private Ability _aoeAbility;

        private Ability _nukeAbility;

        public override void OnLoad()
        {
            base.OnLoad();

            var heroMenu = new Menu("Luna", "zaioLuna", false, "npc_dota_hero_luna",
                true);

            heroMenu.AddItem(new MenuItem("zaioLunaAbilitiesText", "Supported Abilities"));
            var supportedStuff = new MenuItem("zaioLunaAbilities", string.Empty);
            supportedStuff.SetValue(new AbilityToggler(SupportedAbilities.ToDictionary(x => x, y => true)));
            heroMenu.AddItem(supportedStuff);

            heroMenu.AddItem(new MenuItem("zaioLunaKillstealAbilitiesText", "Supported Killsteal Abilities"));
            var supportedKillsteal = new MenuItem("zaioLunaKillstealAbilities", string.Empty);
            supportedKillsteal.SetValue(new AbilityToggler(KillstealAbilities.ToDictionary(x => x, y => true)));
            heroMenu.AddItem(supportedKillsteal);

            OnLoadMenuItems(supportedStuff, supportedKillsteal);

            _minimumEnemyUltCount =
    new MenuItem("zaioLunaMinEnemyCount", "Minimum Enemies for Ult").SetValue(new Slider(1, 0, 4));
            _minimumEnemyUltCount.Tooltip = "Minimum enemies besides your target to use ult.";
            heroMenu.AddItem(_minimumEnemyUltCount);

            ZaioMenu.LoadHeroSettings(heroMenu);

            _nukeAbility = MyHero.GetAbilityById(AbilityId.luna_lucent_beam);
            _aoeAbility = MyHero.GetAbilityById(AbilityId.luna_eclipse);
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

            if (_nukeAbility.IsKillstealAbilityEnabled()&&_nukeAbility.CanBeCasted())
            {
                var damage = _nukeAbility.GetAbilityData("damage");
                damage *= GetSpellAmp();

                var enemy =
                    ObjectManager.GetEntitiesParallel<Hero>()
                                 .FirstOrDefault(
                                     x =>
                                         x.IsAlive && x.Team != MyHero.Team && !x.IsIllusion &&
                                         _nukeAbility.CanBeCasted(x) &&
                                         _nukeAbility.CanHit(x) && !x.IsMagicImmune() &&
                                         x.Health < damage * (1 - x.MagicResistance()) &&
                                         !x.IsLinkensProtected() && !x.CantBeAttacked() && !x.CantBeKilled());
                if (enemy != null)
                {
                    Log.Debug(
                        $"use killsteal lucent beam because enough damage {enemy.Health} <= {damage * (1 - enemy.MagicResistance())} ");
                    _nukeAbility.UseAbility(enemy);
                    await Await.Delay(GetAbilityDelay(enemy, _nukeAbility));
                    return true;
                }
            }

            return false;
        }

        public override async Task ExecuteComboAsync(Unit target, CancellationToken tk = new CancellationToken())
        {
            if (!MyHero.IsSilenced())
            {
                if (_nukeAbility.IsAbilityEnabled() && _nukeAbility.CanBeCasted(target) && _nukeAbility.CanHit(target))
                {
                    _nukeAbility.UseAbility(target);
                    Log.Debug($"using lucent beam!");
                    await Await.Delay(GetAbilityDelay(target, _nukeAbility), tk);
                }

                if (_aoeAbility.IsAbilityEnabled() && _aoeAbility.CanBeCasted() && _aoeAbility.CanHit(target))
                {
                    var hasScepter = MyHero.HasItem(ClassID.CDOTA_Item_UltimateScepter);
                    var radius = _aoeAbility.GetAbilityData("search_radius");

                    var enemiesNearCount =
                        ObjectManager.GetEntitiesParallel<Hero>()
                                     .Count(
                                         x =>
                                             x.IsValid && x != target && x.IsAlive && x.Team != MyHero.Team &&
                                             !x.IsIllusion && x.Distance2D(target) <= radius);
                    if (!hasScepter)
                    {
                        if (enemiesNearCount >= EnemyCountForUlt)
                        {
                            Log.Debug($"using ult since more enemies here");
                            
                            _aoeAbility.UseAbility();
                            await Await.Delay((int)(_aoeAbility.FindCastPoint() * 1000.0 + Game.Ping), tk);
                        }
                    }
                    if (hasScepter)
                    {
                        if (enemiesNearCount >= EnemyCountForUlt)
                        {
                            Log.Debug($"using ult since more enemies here");
                            _aoeAbility.UseAbility(MyHero);
                            await Await.Delay((int)(_aoeAbility.FindCastPoint() * 1000.0 + Game.Ping), tk);
                        }
                    }

                }

                // make him disabled
                if (await DisableEnemy(target, tk) == DisabledState.UsedAbilityToDisable)
                {
                    Log.Debug($"disabled!");
                    // return;
                }

                await UseItems(target, tk);

                //// check if we are near the enemy
                //if (!await MoveOrBlinkToEnemy(target, tk))
                //{
                //    Log.Debug($"return because of blink");
                //    return;
                //}

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
}
