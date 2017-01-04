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
    [Hero(ClassID.CDOTA_Unit_Hero_Tiny)]
    class Tiny : ComboHero
    {
        private static readonly ILog Log = AssemblyLogs.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private static readonly string[] SupportedAbilities = { "tiny_avalanche", "tiny_toss", "item_blink", "item_phase_boots", "item_blade_mail", "item_sheepstick", "item_abyssal_blade", "item_manta" };

        public override void OnLoad()
        {
            base.OnLoad();

            var heroMenu = new Menu("Tiny", "zaioTiny", false, "npc_dota_hero_tiny", true);

            heroMenu.AddItem(new MenuItem("zaioTinyAbilitiesText","Supported Abilities"));
            var supportedStuff = new MenuItem("zaioTinyAbilities", string.Empty);
            supportedStuff.SetValue(new AbilityToggler(SupportedAbilities.ToDictionary(x => x, y => true)));
            heroMenu.AddItem(supportedStuff);

            ZaioMenu.LoadHeroSettings(heroMenu);
        }

        public override async Task ExecuteComboAsync(Unit target, CancellationToken tk = new CancellationToken())
        {
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

            var manta = MyHero.FindItem("item_manta");
            if (manta != null && manta.CanBeCasted() && MyHero.IsSilenced())
            {
                manta.UseAbility();
                await Await.Delay(250, tk);
                manta = null;
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
                    await Await.Delay(125, tk);
                }
            }

            // test if toss/av combo is working
            var avalanche = MyHero.Spellbook.SpellQ;
            var toss = MyHero.Spellbook.SpellW;
            if (toss.CanBeCasted())
            {
                var closestUnit =
                    ObjectManager.GetEntitiesFast<Unit>()
                                 .Where(x => x != MyHero && x.IsAlive && x.Distance2D(MyHero) <= toss.CastRange)
                                 .OrderBy(x => x.Distance2D(MyHero))
                                 .FirstOrDefault();
                Log.Debug($"Closest unit for toss: {closestUnit?.Name}");
                if (closestUnit == target)
                {
                    toss.UseAbility(Target);
                    await Await.Delay(125, tk);
                }
            }
            if (avalanche.CanBeCasted())
            {
                avalanche.UseAbility(Target.NetworkPosition);
                await Await.Delay(125, tk);
            }

            if (manta != null && manta.CanBeCasted())
            {
                manta.UseAbility();
                await Await.Delay(250, tk);
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
