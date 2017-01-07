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
    [Hero(ClassID.CDOTA_Unit_Hero_Legion_Commander)]
    internal class LegionCommander : ComboHero
    {
        private static readonly ILog Log = AssemblyLogs.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private static readonly string[] SupportedAbilities =
        {
            "legion_commander_overwhelming_odds",
            "legion_commander_press_the_attack",
            "legion_commander_duel",
            "item_blade_mail",
            "item_armlet"
        };

        public override void OnLoad()
        {
            base.OnLoad();

            var heroMenu = new Menu("Legion", "zaioLegion", false, "npc_dota_hero_legion_commander", true);

            heroMenu.AddItem(new MenuItem("zaioLegionAbilitiesText", "Supported Abilities"));
            var supportedStuff = new MenuItem("zaioLegionAbilities", string.Empty);
            supportedStuff.SetValue(new AbilityToggler(SupportedAbilities.ToDictionary(x => x, y => true)));
            heroMenu.AddItem(supportedStuff);

            ZaioMenu.LoadHeroSettings(heroMenu);
        }

        public override async Task ExecuteComboAsync(Unit target, CancellationToken tk = new CancellationToken())
        {
            if (MyHero.HasModifier("modifier_legion_commander_duel"))
            {
                return;
            }
            // maybe got some pre damage
            var odds = MyHero.Spellbook.SpellQ;
            if (odds.CanBeCasted() && MyHero.Mana > 300)
            {
                if (MyHero.Distance2D(Target) < odds.CastRange)
                {
                    var radius = odds.AbilitySpecialData.First(x => x.Name == "radius").Value;
                    var targets =
                        ObjectManager.GetEntitiesParallel<Unit>()
                                     .Where(
                                         x =>
                                             x.IsAlive && x.Team != MyHero.Team && x != Target && !x.IsMagicImmune() &&
                                             x.Distance2D(Target) <= radius);
                    var heroes = targets.Where(x => x is Hero);
                    if (heroes.Any() || targets.Count() >= 5)
                    {
                        Log.Debug($"Using Q with {heroes.Count()} heroes and {targets.Count()} targets");
                        odds.UseAbility(Target.NetworkPosition);
                        await Await.Delay((int) (odds.FindCastPoint() * 1000.0 + Game.Ping), tk);
                    }
                    else
                    {
                        Log.Debug($"NOT using Q sionce only {heroes.Count()} heroes and {targets.Count()} targets");
                    }
                }
            }

            // press the attack for teh damage
            var duel = MyHero.Spellbook.SpellR;
            if (IsInRange(duel.CastRange))
            {
                var enemyHealth = (float) Target.Health / Target.MaximumHealth;
                if (!MyHero.HasModifier("modifier_press_the_attack") && enemyHealth > 0.33f)
                {
                    var pressTheAttack = MyHero.Spellbook.SpellW;
                    if (pressTheAttack.CanBeCasted())
                    {
                        pressTheAttack.UseAbility(MyHero);
                        await Await.Delay((int) (pressTheAttack.FindCastPoint() * 1000.0 + Game.Ping), tk);
                    }
                }
                var armlet = MyHero.FindItem("item_armlet");
                if (armlet != null && !armlet.IsToggled)
                {
                    Log.Debug($"toggling armlet");
                    armlet.ToggleAbility();
                    await Await.Delay(1, tk);
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

            var bladeMail = MyHero.FindItem("item_blade_mail");
            if (bladeMail != null && bladeMail.CanBeCasted())
            {
                Log.Debug($"using blademail");
                bladeMail.UseAbility();
                await Await.Delay(1, tk);
            }
            // test if ulti is good
            var hasLinkens = Target.IsLinkensProtected();
            if (hasLinkens)
            {
                var heavens = MyHero.FindItem("item_heavens_halberd");
                if (heavens != null && heavens.CanBeCasted())
                {
                    heavens.UseAbility(Target);
                    await Await.Delay(1, tk);
                    hasLinkens = false;
                }
                else
                {
                    var orchid = MyHero.FindItem("item_orchid");
                    if (orchid != null && orchid.CanBeCasted())
                    {
                        orchid.UseAbility(Target);
                        await Await.Delay(1, tk);
                        hasLinkens = false;
                    }
                    else
                    {
                        var bloodthorn = MyHero.FindItem("item_bloodthorn");
                        if (bloodthorn != null && bloodthorn.CanBeCasted())
                        {
                            bloodthorn.UseAbility(Target);
                            await Await.Delay(1, tk);
                            hasLinkens = false;
                        }
                    }
                }
            }
            if (duel.CanBeCasted(Target) && !hasLinkens)
            {
                Log.Debug($"using duel");
                duel.UseAbility(Target);
                await Await.Delay((int) (duel.FindCastPoint() * 1000.0 + Game.Ping), tk);
                return;
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