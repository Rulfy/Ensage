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
using AbilityId = Ensage.Common.Enums.AbilityId;

namespace Zaio.Heroes
{
    [Hero(ClassID.CDOTA_Unit_Hero_QueenOfPain)]
    internal class QueenOfPain : ComboHero
    {
        private static readonly ILog Log = AssemblyLogs.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private static readonly string[] SupportedAbilities =
        {
            "queenofpain_shadow_strike",
            "queenofpain_blink",
            "queenofpain_scream_of_pain",
            "queenofpain_sonic_wave"
        };

        private static readonly string[] KillstealAbilities =
{
            "queenofpain_scream_of_pain",
            "queenofpain_shadow_strike",
            "queenofpain_sonic_wave"
        };

        private Ability _qAbility;
        private Ability _wAbility;
        private Ability _eAbility;
        private Ability _ultAbility;

        public override void OnLoad()
        {
            base.OnLoad();

            var heroMenu = new Menu("QueenOfPain", "zaioQueenOfPain", false, "npc_dota_hero_queenofpain", true);

            heroMenu.AddItem(new MenuItem("zaioQueenOfPainAbilitiesText", "Supported Abilities"));
            var supportedStuff = new MenuItem("zaioQueenOfPainAbilities", string.Empty);
            supportedStuff.SetValue(new AbilityToggler(SupportedAbilities.ToDictionary(x => x, y => true)));
            heroMenu.AddItem(supportedStuff);

            heroMenu.AddItem(new MenuItem("zaioQueenOfPainKillstealAbilitiesText", "Supported Killsteal Abilities"));
            var supportedKillsteal = new MenuItem("zaioQueenOfPainKillstealAbilities", string.Empty);
            supportedKillsteal.SetValue(new AbilityToggler(KillstealAbilities.ToDictionary(x => x, y => true)));
            heroMenu.AddItem(supportedKillsteal);


            OnLoadMenuItems(supportedStuff, supportedKillsteal);

            ZaioMenu.LoadHeroSettings(heroMenu);

            _qAbility = MyHero.GetAbilityById(AbilityId.queenofpain_shadow_strike);
            _wAbility = MyHero.GetAbilityById(AbilityId.queenofpain_blink);
            _eAbility = MyHero.GetAbilityById(AbilityId.queenofpain_scream_of_pain);
            _ultAbility = MyHero.GetAbilityById(AbilityId.queenofpain_sonic_wave);
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

          if (_eAbility.IsKillstealAbilityEnabled() && _eAbility.CanBeCasted())
          {
            var damage = (float) _eAbility.GetDamage(_eAbility.Level -1);
            damage *= GetSpellAmp();
            var enemy =
                ObjectManager.GetEntitiesParallel<Hero>()
                             .FirstOrDefault(
                                 x =>
                                     x.IsAlive && x.Team != MyHero.Team && !x.IsIllusion &&
                                     _eAbility.CanBeCasted() &&
                                     _eAbility.CanHit(x) && !x.IsMagicImmune() &&
                                     x.Health < damage *(1 - x.MagicResistance()) &&
                                     !x.IsLinkensProtected() && !x.CantBeAttacked() && !x.CantBeKilled());
            if (enemy != null)
            {
              Log.Debug($"killsteal with scream based on enemy hp {enemy.Health} <= {damage} ");
              _eAbility.UseAbility();
              var dist = (int) enemy.Distance2D(MyHero)/900 *1000;
              await Await.Delay(GetAbilityDelay(enemy, _eAbility) + dist);
              return true;
            }
          }

          if (_ultAbility.IsKillstealAbilityEnabled() && _ultAbility.CanBeCasted())
          {
            var hasAgha = MyHero.HasItem(ClassID.CDOTA_Item_UltimateScepter);
            var damage = _ultAbility.GetAbilityData("damage");

            if (hasAgha)
            {
              damage = _ultAbility.GetAbilityData("damage_scepter");
            }

            damage *= GetSpellAmp();
            var enemy =
              ObjectManager.GetEntitiesParallel<Hero>()
                            .FirstOrDefault(
                                x =>
                                    x.IsAlive && x.Team != MyHero.Team && !x.IsIllusion &&
                                    _ultAbility.CanBeCasted(x) &&
                                    _ultAbility.CanHit(x) &&
                                    x.Health < damage &&
                                    !x.CantBeAttacked() && !x.CantBeKilled());
                if (enemy != null)
                {
                    Log.Debug($"killsteal with ult scream based on enemy hp {enemy.Health} <= {damage} ");
                    _ultAbility.UseAbility(enemy.NetworkPosition);
                    await Await.Delay(GetAbilityDelay(enemy.NetworkPosition, _ultAbility));
                    return true;
                }
          }

          if (_qAbility.IsKillstealAbilityEnabled() && _qAbility.CanBeCasted())
          {
            var damage = _qAbility.GetAbilityData("strike_damage");
            damage *= GetSpellAmp();
            var enemy =
                ObjectManager.GetEntitiesParallel<Hero>()
                             .FirstOrDefault(
                                 x =>
                                     x.IsAlive && x.Team != MyHero.Team && !x.IsIllusion &&
                                     _qAbility.CanBeCasted() &&
                                     _qAbility.CanHit(x) && !x.IsMagicImmune() &&
                                     x.Health < damage *(1 - x.MagicResistance()) &&
                                     !x.IsLinkensProtected() && !x.CantBeAttacked() && !x.CantBeKilled());
            if (enemy != null)
            {
              Log.Debug($"killsteal with ss based on enemy hp {enemy.Health} <= {damage} ");
              _qAbility.UseAbility(enemy);
              await Await.Delay(GetAbilityDelay(enemy, _qAbility));
              return true;
            }
          }
          return false;
        }

        public override async Task ExecuteComboAsync(Unit target, CancellationToken tk = new CancellationToken())
        {


            // check if we are near the enemy
            if (!await MoveOrBlinkToEnemy(target, tk, minimumRange: 200, maximumRange: 475))
            {
              if (!MyHero.IsSilenced())
              {
                  if (_wAbility.IsAbilityEnabled() && _wAbility.CanBeCasted(target))
                  {
                      Log.Debug($"use w");
                      var castPoint = (float) _wAbility.FindCastPoint();
                      var prpos = Prediction.Prediction.PredictPosition(target, (int) (castPoint * target.MovementSpeed));
                      _wAbility.UseAbility(prpos);
                      await Await.Delay(GetAbilityDelay(target, _wAbility), tk);
                  }
              }
                return;
            }
            await HasNoLinkens(target, tk);
            await UseItems(target, tk);
            await DisableEnemy(target, tk);

            if (!MyHero.IsSilenced())
            {
                if (_eAbility.IsAbilityEnabled() && _eAbility.CanBeCasted(target) && _eAbility.CanHit(target))
                {
                    Log.Debug($"use e");
                    _eAbility.UseAbility();
                    await Await.Delay(GetAbilityDelay(target, _eAbility), tk);
                }

                if (_qAbility.IsAbilityEnabled() && _qAbility.CanBeCasted(target) && _qAbility.CanHit(target) && !target.HasModifier("modifier_queenofpain_shadow_strike"))
                {
                    Log.Debug($"use q");
                    _qAbility.UseAbility(target);
                    var dist = (int) target.Distance2D(MyHero)/900 *1000;
                    await Await.Delay((GetAbilityDelay(target, _qAbility) + dist), tk);
                }

                if (_ultAbility.IsAbilityEnabled() && _ultAbility.CanBeCasted(target) && _ultAbility.CanHit(target))
                {
                  var enemy =
                  ObjectManager.GetEntitiesParallel<Hero>()
                      .FirstOrDefault(
                          x =>
                              x.IsAlive && x.Team != MyHero.Team && !x.IsIllusion &&
                              _ultAbility.CanBeCasted(x) &&
                              _ultAbility.CanHit(x) &&
                              !x.CantBeKilled());
                  var numenemies =
                        ObjectManager.GetEntitiesParallel<Hero>()
                            .Count(
                            x =>
                              x.IsAlive && x.Team != MyHero.Team && !x.IsIllusion &&
                              _ultAbility.CanBeCasted(x) &&
                              _ultAbility.CanHit(x) &&
                              !x.CantBeKilled());
                            
                    if (numenemies >= 2)
                    {
                        Log.Debug($"use ult (two or more targets can be hit)");
                        _ultAbility.UseAbility(enemy.NetworkPosition);
                        await Await.Delay(GetAbilityDelay(enemy.NetworkPosition, _ultAbility), tk);
                    }

                }
            }

            if (ZaioMenu.ShouldUseOrbwalker)
            {
                Orbwalk();
            }
            else
            {
                MyHero.Attack(target);
                await Await.Delay(115, tk);
            }
        }
    }
}
