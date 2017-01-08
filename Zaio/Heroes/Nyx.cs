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
    [Hero(ClassID.CDOTA_Unit_Hero_Nyx_Assassin)]
    internal class Nyx : ComboHero
    {
        private static readonly ILog Log = AssemblyLogs.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private static readonly string[] SupportedAbilities =
        {
            "nyx_assassin_impale",
            "nyx_assassin_mana_burn",
            "nyx_assassin_spiked_carapace",
            "nyx_assassin_vendetta",
            "item_blade_mail",
            "item_ethereal_blade",
            "item_dagon"
        };

        public override void OnLoad()
        {
            base.OnLoad();

            var heroMenu = new Menu("Nyx", "zaioNyx", false, "npc_dota_hero_nyx_assassin", true);

            heroMenu.AddItem(new MenuItem("zaioNyxAbilitiesText", "Supported Abilities"));
            var supportedStuff = new MenuItem("zaioNyxAbilities", string.Empty);
            supportedStuff.SetValue(new AbilityToggler(SupportedAbilities.ToDictionary(x => x, y => true)));
            heroMenu.AddItem(supportedStuff);

            ZaioMenu.LoadHeroSettings(heroMenu);
        }

        public override async Task ExecuteComboAsync(Unit target, CancellationToken tk = new CancellationToken())
        {
            if (!MyHero.HasModifier("modifier_nyx_assassin_vendetta"))
            {
                var stun = MyHero.Spellbook.SpellQ;

                var manaNeeded = stun.CanBeCasted(Target) ? stun.ManaCost + 100 : 0;
                if (manaNeeded <= MyHero.Mana)
                {
                    // make him disabled
                    if (DisableEnemy(tk) == DisabledState.UsedAbilityToDisable)
                    {
                        Log.Debug($"disabled!");
                        // return;
                    }

                    var ethereal = MyHero.FindItem("item_ethereal_blade");
                    if (ethereal != null && ethereal.CanBeCasted(Target) && ethereal.CanHit(Target))
                    {
                        ethereal.UseAbility(Target);
                        var speed = ethereal.AbilitySpecialData.First(x => x.Name == "projectile_speed").Value;
                        var time = Target.Distance2D(MyHero) / speed;
                        Log.Debug($"waiting for eth {time}");
                        await Await.Delay((int) (time * 1000.0f + Game.Ping), tk);
                    }
                    var dagon = MyHero.Inventory.Items.FirstOrDefault(x => x.Name.StartsWith("item_dagon"));
                    if (dagon != null && dagon.CanBeCasted(Target) && dagon.CanHit(Target))
                    {
                        Log.Debug($"Use dagon");
                        dagon.UseAbility(Target);
                    }
                }
                if (stun.CanBeCasted(Target) && stun.CanHit(Target))
                {
                    var speed = stun.AbilitySpecialData.First(x => x.Name == "speed").Value;
                    var time = Target.Distance2D(MyHero) / speed * 1000.0f;

                    var predictedPos = Prediction.Prediction.PredictPosition(Target, (int) time);
                    stun.UseAbility(predictedPos);

                    Log.Debug($"Use stun");
                    await Await.Delay((int) (stun.FindCastPoint() * 1000.0 + Game.Ping), tk);
                }

                var manaBurn = MyHero.Spellbook.SpellW;
                Log.Debug($"Use manaburn {manaBurn.CanBeCasted(Target)} | {manaBurn.CanHit(Target)}");
                if (manaBurn.CanBeCasted(Target) && Target.Mana > 100 && manaBurn.CanHit(Target))
                {
                    manaBurn.UseAbility(Target);
                    Log.Debug($"Use manaburn");
                    await Await.Delay((int) (manaBurn.FindCastPoint() * 1000.0 + Game.Ping), tk);
                }
            }

            // check if we are near the enemy
            var ult = MyHero.Spellbook.Spells.First(x => x.AbilityData2.ID == 5465);
            if (!await MoveOrBlinkToEnemy(tk))
            {
                if (ult.CanBeCasted() && !MyHero.HasModifier("modifier_nyx_assassin_vendetta"))
                {
                    Log.Debug($"going invis boys since too far");
                    ult.UseAbility();
                    await Await.Delay(125, tk);
                }
                Log.Debug($"move or blink");
                return;
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