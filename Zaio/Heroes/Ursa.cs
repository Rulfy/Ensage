using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Ensage;
using Ensage.Common.Extensions;
using Ensage.Common.Menu;
using Ensage.Common.Threading;
using Zaio.Helpers;
using Zaio.Interfaces;

namespace Zaio.Heroes
{
    [Hero(ClassID.CDOTA_Unit_Hero_Ursa)]
    internal class Ursa : ComboHero
    {
        private static readonly string[] SupportedAbilities =
        {
            "ursa_earthshock",
            "ursa_overpower",
            "ursa_enrage",
            "item_blink",
            "item_phase_boots",
            "item_blade_mail",
            "item_sheepstick",
            "item_abyssal_blade"
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
            // check if we are near the enemy
            if (!await MoveOrBlinkToEnemy(tk))
            {
                return;
            }
            // make him disabled
            if (await DisableEnemy(tk))
            {
                return;
            }
            if (!(Target.IsHexed() || Target.IsStunned()) && !Target.IsMagicImmune())
            {
                var healthPercentage = (float) Target.Health / Target.MaximumHealth;
                if (healthPercentage > 0.5)
                {
                    var earthshock = MyHero.Spellbook.SpellQ;
                    if (earthshock.CanBeCasted())
                    {
                        earthshock.UseAbility();
                        await Await.Delay((int) (earthshock.FindCastPoint() * 1000.0 + Game.Ping), tk);
                    }
                }
            }
            // test if ulti is good
            var enrage = MyHero.Spellbook.SpellR;
            bool? hasEnemies = null;
            if (enrage.CanBeCasted())
            {
                var enemies =
                    ObjectManager.GetEntitiesFast<Hero>()
                                 .Where(
                                     x =>
                                         x.IsAlive && x.Team != MyHero.Team && x != Target &&
                                         x.Distance2D(MyHero) < 600);
                hasEnemies = enemies.Any();
                if (MyHero.IsStunned() || hasEnemies == true || (float) MyHero.Health / MyHero.MaximumHealth <= 0.25f)
                {
                    enrage.UseAbility();
                    await Await.Delay(125, tk);
                }
            }
            var bladeMail = MyHero.FindItem("item_blade_mail");
            if (bladeMail != null && bladeMail.CanBeCasted())
            {
                if (hasEnemies == null)
                {
                    var enemies =
                        ObjectManager.GetEntitiesFast<Hero>()
                                     .Where(
                                         x =>
                                             x.IsAlive && x.Team != MyHero.Team && x != Target &&
                                             x.Distance2D(MyHero) < 600);
                    hasEnemies = enemies.Any();
                }
                if (hasEnemies == true)
                {
                    bladeMail.UseAbility();
                    await Await.Delay(125, tk);
                }
            }

            if (ZaioMenu.ShouldUseOrbwalker)
            {
                Orbwalker.Attack(Target, false);
            }
            else
            {
                MyHero.Attack(Target);
            }
            await Await.Delay(125, tk);
        }
    }
}