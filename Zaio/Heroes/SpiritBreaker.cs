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
    [Hero(ClassID.CDOTA_Unit_Hero_SpiritBreaker)]
    internal class SpiritBreaker : ComboHero
    {
        private static readonly ILog Log = AssemblyLogs.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private static readonly string[] SupportedAbilities =
        {
            "spirit_breaker_charge_of_darkness",
            "spirit_breaker_nether_strike",
            "item_invis_sword",
            "item_silver_edge",
            "item_urn_of_shadows",
            "item_armlet",
            "item_mask_of_madness"
        };

        public override void OnLoad()
        {
            base.OnLoad();

            var heroMenu = new Menu("SpiritBreaker", "zaioSpiritBreaker", false, "npc_dota_hero_spirit_breaker", true);

            heroMenu.AddItem(new MenuItem("zaioSpiritBreakerAbilitiesText", "Supported Abilities"));
            var supportedStuff = new MenuItem("zaioSpiritBreakerAbilities", string.Empty);
            supportedStuff.SetValue(new AbilityToggler(SupportedAbilities.ToDictionary(x => x, y => true)));
            heroMenu.AddItem(supportedStuff);

            ZaioMenu.LoadHeroSettings(heroMenu);
        }

        public override async Task ExecuteComboAsync(Unit target, CancellationToken tk = new CancellationToken())
        {
            if (MyHero.HasModifier("modifier_spirit_breaker_charge_of_darkness"))
            {
                if (!MyHero.IsInvisible())
                {
                    var shadowBlade = MyHero.FindItem("item_invis_sword") ?? MyHero.FindItem("item_silver_edge");
                    var distance = MyHero.Distance2D(Target);
                    if (shadowBlade != null && shadowBlade.CanBeCasted() && !MyHero.IsVisibleToEnemies &&
                        distance > 1200 && distance < 6000)
                    {
                        Log.Debug($"using invis");
                        shadowBlade.UseAbility();
                        await Await.Delay(500, tk);
                    }
                }
                return;
            }

            var charge = MyHero.Spellbook.SpellQ;
            // check if we are near the enemy
            if (!await MoveOrBlinkToEnemy(tk))
            {
                if (charge.CanBeCasted() && !MyHero.HasModifier("modifier_spirit_breaker_charge_of_darkness"))
                {
                    Log.Debug($"charging enemy since too far");
                    charge.UseAbility(Target);
                    await Await.Delay((int)(charge.FindCastPoint() * 1000.0 + Game.Ping), tk);
                }

                return;
            }
            var ult = MyHero.Spellbook.SpellR;
            // make him disabled
            if (await DisableEnemy(tk, ult.CanBeCasted(Target) ? (float)ult.FindCastPoint() : 0))
            {
                Log.Debug($"disabled enemy");
                return;
            }

            var armlet = MyHero.FindItem("item_armlet");
            if (armlet != null && !armlet.IsToggled)
            {
                Log.Debug($"toggling armlet");
                armlet.ToggleAbility();
                await Await.Delay(1, tk);
            }

            var bladeMail = MyHero.FindItem("item_blade_mail");
            if (bladeMail != null && bladeMail.CanBeCasted())
            {
                var enemies =
                    ObjectManager.GetEntitiesFast<Hero>()
                                 .Where(
                                     x =>
                                         x.IsAlive && x.Team != MyHero.Team && x != Target &&
                                         x.Distance2D(MyHero) < 600);
                if (enemies.Any())
                {
                    bladeMail.UseAbility();
                    await Await.Delay(1, tk);
                }
            }

            if (ult.CanBeCasted())
            {
                Log.Debug($"using ult on target");
                ult.UseAbility(Target);
                await Await.Delay((int)(ult.FindCastPoint() * 1000.0 + Game.Ping), tk);
            }

            var urn = MyHero.FindItem("item_urn_of_shadows");
            if (urn != null && urn.CanBeCasted(Target) && urn.CurrentCharges > 0 && !Target.HasModifier("modifier_item_urn_damage"))
            {
                Log.Debug($"using URN on target");
                urn.UseAbility(Target);
                await Await.Delay(1, tk);
            }

            var mask = MyHero.FindItem("item_mask_of_madness");
            if (mask != null && mask.CanBeCasted() )
            {
                Log.Debug($"using mask");
                mask.UseAbility();
                await Await.Delay(1, tk);
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