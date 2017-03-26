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
    [Hero(ClassId.CDOTA_Unit_Hero_Skywrath_Mage)]
    internal class SkywrathMage : ComboHero
    {
        private static readonly ILog Log = AssemblyLogs.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private static readonly string[] SupportedAbilities =
        {
            "skywrath_mage_arcane_bolt",
            "skywrath_mage_concussive_shot",
            "skywrath_mage_ancient_seal",
            "skywrath_mage_mystic_flare"
        };

        private Ability _qAbility;
        private Ability _silenceAbility;

        private Ability _slowAbility;
        private Ability _ultAbility;

        public override void OnLoad()
        {
            base.OnLoad();

            var heroMenu = new Menu("SkywrathMage", "zaioSkywrathMage", false, "npc_dota_hero_skywrath_mage", true);

            heroMenu.AddItem(new MenuItem("zaioSpiritBreakerAbilitiesText", "Supported Abilities"));
            var supportedStuff = new MenuItem("zaioSkywrathMageAbilities", string.Empty);
            supportedStuff.SetValue(new AbilityToggler(SupportedAbilities.ToDictionary(x => x, y => true)));
            heroMenu.AddItem(supportedStuff);

            OnLoadMenuItems(supportedStuff);

            ZaioMenu.LoadHeroSettings(heroMenu);

            _slowAbility = MyHero.GetAbilityById(AbilityId.skywrath_mage_concussive_shot);
            _qAbility = MyHero.GetAbilityById(AbilityId.skywrath_mage_arcane_bolt);
            _silenceAbility = MyHero.GetAbilityById(AbilityId.skywrath_mage_ancient_seal);
            _ultAbility = MyHero.GetAbilityById(AbilityId.skywrath_mage_mystic_flare);
        }

        public override async Task ExecuteComboAsync(Unit target, CancellationToken tk = new CancellationToken())
        {
            if (!MyHero.IsSilenced())
            {
                if (_silenceAbility.IsAbilityEnabled() && _silenceAbility.CanBeCasted(target) && _silenceAbility.CanHit(target))
                {
                    Log.Debug($"use e");
                    _silenceAbility.UseAbility(target);
                    await Await.Delay(GetAbilityDelay(target, _silenceAbility), tk);
                }
                if (_slowAbility.IsAbilityEnabled() && _slowAbility.CanBeCasted(target) && _slowAbility.CanHit(target))
                {
                    Log.Debug($"use W");
                    _slowAbility.UseAbility();
                    await Await.Delay(100, tk);
                }
            }

            // check if we are near the enemy
            if (!await MoveOrBlinkToEnemy(target, tk, minimumRange: 200, maximumRange: 700))
            {
                return;
            }
            await HasNoLinkens(target, tk);
            await UseItems(target, tk);
            await DisableEnemy(target, tk);

            if (!MyHero.IsSilenced())
            {
                if (_qAbility.IsAbilityEnabled() && _qAbility.CanBeCasted(target) && _qAbility.CanHit(target))
                {
                    Log.Debug($"use Q");
                    _qAbility.UseAbility(target);
                    await Await.Delay(GetAbilityDelay(target, _qAbility), tk);
                }
                if (_silenceAbility.IsAbilityEnabled() && _silenceAbility.CanBeCasted(target) && _silenceAbility.CanHit(target))
                {
                    Log.Debug($"use e");
                    _silenceAbility.UseAbility(target);
                    await Await.Delay(GetAbilityDelay(target, _silenceAbility), tk);
                }

                if (_ultAbility.IsAbilityEnabled() && _ultAbility.CanBeCasted(target) && _ultAbility.CanHit(target) &&
                    (target.IsRooted() || target.MovementSpeed < 200 || !target.IsMoving))
                {
                    Log.Debug($"use ult {target.IsRooted()} | {target.IsMoving} | {target.MovementSpeed}");
                    var castPoint = (float) _ultAbility.FindCastPoint();
                    var pos = Prediction.Prediction.PredictPosition(target, (int) (castPoint * target.MovementSpeed));
                    _ultAbility.UseAbility(pos);
                    await Await.Delay(GetAbilityDelay(pos, _ultAbility), tk);
                }
            }

            if (ZaioMenu.ShouldUseOrbwalker)
            {
                Orbwalk();
            }
            else
            {
                MyHero.Attack(target);
                await Await.Delay(125, tk);
            }
        }
    }
}