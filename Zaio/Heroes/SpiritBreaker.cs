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
    [Hero(ClassID.CDOTA_Unit_Hero_SpiritBreaker)]
    internal class SpiritBreaker : ComboHero
    {
        private static readonly ILog Log = AssemblyLogs.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private static readonly string[] SupportedAbilities =
        {
            "spirit_breaker_charge_of_darkness",
            "spirit_breaker_nether_strike",
            "item_invis_sword",
            "item_silver_edge"
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
                    var shadowBlade = MyHero.GetItemById(ItemId.item_invis_sword) ??
                                      MyHero.GetItemById(ItemId.item_silver_edge);
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
                if (charge.CanBeCasted())
                {
                    Log.Debug($"charging enemy since too far");
                    charge.UseAbility(Target);
                    await Await.Delay((int) (charge.FindCastPoint() * 1000.0 + Game.Ping), tk);
                }

                return;
            }
            HasNoLinkens(Target);
            await UseItems(tk);

            var ult = MyHero.Spellbook.SpellR;
            // make him disabled
            if (await DisableEnemy(tk, ult.CanBeCasted(Target) ? (float) ult.FindCastPoint() : 0) ==
                DisabledState.UsedAbilityToDisable)
            {
                Log.Debug($"disabled enemy");
                //return;
            }

            var armlet = MyHero.GetItemById(ItemId.item_armlet);
            if (armlet != null && !armlet.IsToggled)
            {
                Log.Debug($"toggling armlet");
                armlet.ToggleAbility();
            }

            if (ult.CanBeCasted() && ult.CanHit(Target))
            {
                Log.Debug($"using ult on target");
                ult.UseAbility(Target);
                await Await.Delay((int) (ult.FindCastPoint() * 1000.0 + Game.Ping), tk);
            }

            var mask = MyHero.GetItemById(ItemId.item_mask_of_madness);
            if (mask != null && mask.CanBeCasted() && ult.Cooldown > 0)
            {
                Log.Debug($"using mask");
                mask.UseAbility();
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