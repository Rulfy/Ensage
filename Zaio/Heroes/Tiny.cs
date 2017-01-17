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
    [Hero(ClassID.CDOTA_Unit_Hero_Tiny)]
    internal class Tiny : ComboHero
    {
        private static readonly ILog Log = AssemblyLogs.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private static readonly string[] SupportedAbilities =
        {
            "tiny_avalanche",
            "tiny_toss"
        };

        public override void OnLoad()
        {
            base.OnLoad();

            var heroMenu = new Menu("Tiny", "zaioTiny", false, "npc_dota_hero_tiny", true);

            heroMenu.AddItem(new MenuItem("zaioTinyAbilitiesText", "Supported Abilities"));
            var supportedStuff = new MenuItem("zaioTinyAbilities", string.Empty);
            supportedStuff.SetValue(new AbilityToggler(SupportedAbilities.ToDictionary(x => x, y => true)));
            heroMenu.AddItem(supportedStuff);

            ZaioMenu.LoadHeroSettings(heroMenu);
        }

        public override async Task ExecuteComboAsync(Unit target, CancellationToken tk = new CancellationToken())
        {
            HasNoLinkens(Target);
            await UseItems(tk);

            // make him disabled
            if (await DisableEnemy(tk) == DisabledState.UsedAbilityToDisable)
            {
                Log.Debug($"disabled!");
                // return;
            }

            var manta = MyHero.GetItemById(ItemId.item_manta);
            if (manta != null && manta.CanBeCasted() && MyHero.IsSilenced())
            {
                Log.Debug($"use manta 1 because silenced");
                manta.UseAbility();
                await Await.Delay(125, tk);
                manta = null;
            }

            // test if toss/av combo is working
            var avalanche = MyHero.Spellbook.SpellQ;
            var toss = MyHero.Spellbook.SpellW;
            if (toss.CanBeCasted(Target) && toss.CanHit(Target))
            {
                Log.Debug($"use toss");
                var blink = MyHero.Inventory.Items.FirstOrDefault(x => x.ClassID == ClassID.CDOTA_Item_BlinkDagger);
                var grab = toss.GetAbilityData("grab_radius");
                var closestUnit =
                    ObjectManager.GetEntitiesFast<Unit>()
                                 .Where(x => x != MyHero && x.IsAlive && x.Distance2D(MyHero) <= grab)
                                 .OrderBy(x => x.Distance2D(MyHero))
                                 .FirstOrDefault();
                Log.Debug($"Closest unit for toss: {closestUnit?.Name}");
                if (closestUnit == Target || blink.Cooldown > 0)
                {
                    toss.UseAbility(Target);
                    Log.Debug($"use toss!!");
                    await Await.Delay(100, tk);
                }
            }
            if (avalanche.CanBeCasted(Target) && avalanche.CanHit(Target))
            {
                Log.Debug($"use avalanche");
                avalanche.UseAbility(Target.NetworkPosition);
                await Await.Delay(100, tk);
            }

            // check if we are near the enemy
            if (!await MoveOrBlinkToEnemy(tk))
            {
                Log.Debug($"return because of blink");
                return;
            }

            if (manta != null && manta.CanBeCasted())
            {
                Log.Debug($"Use manta");
                manta.UseAbility();
                await Await.Delay(250, tk);
            }

            if (ZaioMenu.ShouldUseOrbwalker && !Target.HasModifier("modifier_tiny_toss"))
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