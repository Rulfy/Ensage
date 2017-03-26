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
    [Hero(ClassID.CDOTA_Unit_Hero_Ursa)]
    internal class Ursa : ComboHero
    {
        private static readonly ILog Log = AssemblyLogs.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private static readonly string[] SupportedAbilities =
        {
            "ursa_earthshock",
            "ursa_overpower",
            "ursa_enrage"
        };

        private Ability _earthshockAbility;

        private Ability _overpowerAbility;
        private Ability _ultAbility;

        public override void OnLoad()
        {
            base.OnLoad();

            var heroMenu = new Menu("Ursa", "zaioUrsa", false, "npc_dota_hero_ursa", true);

            heroMenu.AddItem(new MenuItem("zaioUrsaAbilitiesText", "Supported Abilities"));
            var supportedStuff = new MenuItem("zaioUrsaAbilities", string.Empty);
            supportedStuff.SetValue(new AbilityToggler(SupportedAbilities.ToDictionary(x => x, y => true)));
            heroMenu.AddItem(supportedStuff);

            OnLoadMenuItems(supportedStuff);

            ZaioMenu.LoadHeroSettings(heroMenu);

            _ultAbility = MyHero.GetAbilityById(AbilityId.ursa_enrage);
            _earthshockAbility = MyHero.GetAbilityById(AbilityId.ursa_earthshock);
            _overpowerAbility = MyHero.GetAbilityById(AbilityId.ursa_overpower);
        }

        public override async Task ExecuteComboAsync(Unit target, CancellationToken tk = new CancellationToken())
        {
            // overpower for teh damage
            if (_overpowerAbility.IsAbilityEnabled() && IsInRange(MyHero.AttackRange) && !MyHero.HasModifier("modifier_ursa_overpower"))
            {
                if (!MyHero.IsSilenced() && _overpowerAbility.CanBeCasted())
                {
                    _overpowerAbility.UseAbility();
                    await Await.Delay((int) (_overpowerAbility.FindCastPoint() * 1000.0 + Game.Ping), tk);
                }
            }
            await HasNoLinkens(target, tk);
            await UseItems(target, tk);

            // make him disabled
            if (await DisableEnemy(target, tk) == DisabledState.UsedAbilityToDisable)
            {
                Log.Debug($"disabled!");
                // return;
            }
            if (_earthshockAbility.IsAbilityEnabled() && !(target.IsHexed() || target.IsStunned()) && !target.IsMagicImmune())
            {
                var healthPercentage = (float) target.Health / target.MaximumHealth;
                if (healthPercentage > 0.5)
                {
                    if (!MyHero.IsSilenced() && _earthshockAbility.CanBeCasted(target) &&
                        _earthshockAbility.CanHit(target))
                    {
                        _earthshockAbility.UseAbility();
                        await Await.Delay((int) (_earthshockAbility.FindCastPoint() * 1000.0 + Game.Ping), tk);
                    }
                }
            }
            // check if we are near the enemy
            if (!await MoveOrBlinkToEnemy(target, tk))
            {
                return;
            }
            // test if ulti is good
            if (_ultAbility.IsAbilityEnabled() && _ultAbility.CanBeCasted())
            {
                var enemies =
                    ObjectManager.GetEntitiesFast<Hero>()
                                 .Where(
                                     x =>
                                         x.IsAlive && x.Team != MyHero.Team && x != target &&
                                         x.Distance2D(MyHero) < 600);
                bool? hasEnemies = enemies.Any();
                if (MyHero.IsStunned() || hasEnemies == true || (float) MyHero.Health / MyHero.MaximumHealth <= 0.25f)
                {
                    _ultAbility.UseAbility();
                    await Await.Delay(125, tk);
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