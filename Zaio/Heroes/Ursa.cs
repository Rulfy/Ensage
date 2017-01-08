using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Ensage;
using Ensage.Common.Extensions;
using Ensage.Common.Menu;
using Ensage.Common.Threading;
using log4net;
using PlaySharp.Toolkit.Logging;
using Zaio.Helpers;
using Zaio.Interfaces;

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
            "ursa_enrage",
            "item_blade_mail"
        };

        public override void OnLoad()
        {
            base.OnLoad();

            var heroMenu = new Menu("Ursa", "zaioUrsa", false, "npc_dota_hero_ursa", true);

            heroMenu.AddItem(new MenuItem("zaioUrsaAbilitiesText", "Supported Abilities"));
            var supportedStuff = new MenuItem("zaioUrsaAbilities", string.Empty);
            supportedStuff.SetValue(new AbilityToggler(SupportedAbilities.ToDictionary(x => x, y => true)));
            heroMenu.AddItem(supportedStuff);

            ZaioMenu.LoadHeroSettings(heroMenu);
        }

        public override async Task ExecuteComboAsync(Unit target, CancellationToken tk = new CancellationToken())
        {
            // overpower for teh damage
            if (IsInRange(MyHero.AttackRange) && !MyHero.HasModifier("modifier_ursa_overpower"))
            {
                var overpower = MyHero.Spellbook.SpellW;
                if (overpower.CanBeCasted())
                {
                    overpower.UseAbility();
                    await Await.Delay((int) (overpower.FindCastPoint() * 1000.0 + Game.Ping), tk);
                }
            }
            // make him disabled
            if (DisableEnemy(tk) == DisabledState.UsedAbilityToDisable)
            {
                Log.Debug($"disabled!");
                // return;
            }
            if (!(Target.IsHexed() || Target.IsStunned()) && !Target.IsMagicImmune())
            {
                var healthPercentage = (float) Target.Health / Target.MaximumHealth;
                if (healthPercentage > 0.5)
                {
                    var earthshock = MyHero.Spellbook.SpellQ;
                    if (earthshock.CanBeCasted(Target) && earthshock.CanHit(Target))
                    {
                        earthshock.UseAbility();
                        await Await.Delay((int) (earthshock.FindCastPoint() * 1000.0 + Game.Ping), tk);
                    }
                }
            }
            // check if we are near the enemy
            if (!await MoveOrBlinkToEnemy(tk))
            {
                return;
            }
            // test if ulti is good
            var enrage = MyHero.Spellbook.SpellR;
            if (enrage.CanBeCasted())
            {
                var enemies =
                    ObjectManager.GetEntitiesFast<Hero>()
                                 .Where(
                                     x =>
                                         x.IsAlive && x.Team != MyHero.Team && x != Target &&
                                         x.Distance2D(MyHero) < 600);
                bool? hasEnemies = enemies.Any();
                if (MyHero.IsStunned() || hasEnemies == true || (float) MyHero.Health / MyHero.MaximumHealth <= 0.25f)
                {
                    enrage.UseAbility();
                    await Await.Delay(125, tk);
                }
            }

            if (ZaioMenu.ShouldUseOrbwalker)
            {
                Orbwalk();
            }
            else
            {
                MyHero.Attack(Target);
                await Await.Delay(125, tk);
            }
        }
    }
}