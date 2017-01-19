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
    [Hero(ClassID.CDOTA_Unit_Hero_Axe)]
    internal class Axe : ComboHero
    {
        private static readonly ILog Log = AssemblyLogs.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private static readonly string[] SupportedAbilities =
        {
            "axe_berserkers_call",
            "axe_culling_blade",
            "item_blade_mail"
        };

        private static readonly string[] KillstealAbilities =
        {
            "axe_culling_blade"
        };

        public override void OnLoad()
        {
            base.OnLoad();

            var heroMenu = new Menu("Axe", "zaioAxe", false, "npc_dota_hero_axe", true);

            heroMenu.AddItem(new MenuItem("zaioAxeAbilitiesText", "Supported Abilities"));
            var supportedStuff = new MenuItem("zaioAxeAbilities", string.Empty);
            supportedStuff.SetValue(new AbilityToggler(SupportedAbilities.ToDictionary(x => x, y => true)));
            heroMenu.AddItem(supportedStuff);

            heroMenu.AddItem(new MenuItem("zaioAxeKillstealAbilitiesText", "Supported Killsteal Abilities"));
            var supportedKillsteal = new MenuItem("zaioAxeKillstealAbilities", string.Empty);
            supportedKillsteal.SetValue(new AbilityToggler(KillstealAbilities.ToDictionary(x => x, y => true)));
            heroMenu.AddItem(supportedKillsteal);

            ZaioMenu.LoadHeroSettings(heroMenu);
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

            var ult = MyHero.Spellbook.SpellR;
            if (ult.CanBeCasted())
            {
                var threshold =
                    ult.GetAbilityData(MyHero.HasItem(ClassID.CDOTA_Item_UltimateScepter)
                        ? "kill_threshold_scepter"
                        : "kill_threshold");

                var enemy = ObjectManager.GetEntitiesParallel<Hero>().FirstOrDefault(
                    x =>
                        x.IsValid && x.IsAlive && x.Team != MyHero.Team && !x.IsIllusion &&
                        ult.CanBeCasted(x) && ult.CanHit(x) && x.Health < threshold && !x.IsLinkensProtected() &&
                        !x.CantBeAttacked() && !x.CantBeKilledByAxeUlt());
                if (enemy != null)
                {
                    Log.Debug($"using ult on {enemy.Name}: {enemy.Health} < {threshold}");
                    ult.UseAbility(enemy);
                    await Await.Delay((int) (ult.FindCastPoint() * 1000.0 + Game.Ping));
                }
            }
            return false;
        }


        public override async Task ExecuteComboAsync(Unit target, CancellationToken tk = new CancellationToken())
        {
            var ult = MyHero.Spellbook.SpellR;
            if (ult.CanBeCasted(Target) && ult.CanHit(Target) && await HasNoLinkens(Target, tk))
            {
                var threshold =
                    ult.GetAbilityData(MyHero.HasItem(ClassID.CDOTA_Item_UltimateScepter)
                        ? "kill_threshold_scepter"
                        : "kill_threshold");
                if (Target.Health < threshold)
                {
                    Log.Debug($"using ult {Target.Health} < {threshold}");
                    ult.UseAbility(Target);
                    await Await.Delay((int) (ult.FindCastPoint() * 1000.0 + Game.Ping), tk);
                }
            }

            HasNoLinkens(Target);
            await UseItems(tk);

            // make him disabled
            if (await DisableEnemy(tk) == DisabledState.UsedAbilityToDisable)
            {
                Log.Debug($"disabled!");
                // return;
            }

            // check if we are near the enemy
            if (!await MoveOrBlinkToEnemy(tk))
            {
                Log.Debug($"return because of blink");
                return;
            }

            var call = MyHero.Spellbook.SpellQ;
            if (call.CanBeCasted(Target) && call.CanHit(Target))
            {
                var bladeMail = MyHero.GetItemById(ItemId.item_blade_mail);
                if (bladeMail != null && bladeMail.CanBeCasted())
                {
                    Log.Debug($"using blademail before call");
                    bladeMail.UseAbility();
                }

                Log.Debug($"using call");
                call.UseAbility();
                await Await.Delay((int) (call.FindCastPoint() * 1000.0 + Game.Ping), tk);
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