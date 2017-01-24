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
    [Hero(ClassID.CDOTA_Unit_Hero_Skywrath_Mage)]
    internal class SkywrathMage : ComboHero
    {
        private static readonly ILog Log = AssemblyLogs.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private static readonly string[] SupportedAbilities =
        {
            "skywrath_mage_arcane_bolt",
            "skywrath_mage_concussive_shot",
            "skywrath_mage_ancient_seal",
            "skywrath_mage_mystic_flare"
        };

        public override void OnLoad()
        {
            base.OnLoad();

            var heroMenu = new Menu("SkywrathMage", "zaioSkywrathMage", false, "npc_dota_hero_skywrath_mage", true);

            heroMenu.AddItem(new MenuItem("zaioSpiritBreakerAbilitiesText", "Supported Abilities"));
            var supportedStuff = new MenuItem("zaioSkywrathMageAbilities", string.Empty);
            supportedStuff.SetValue(new AbilityToggler(SupportedAbilities.ToDictionary(x => x, y => true)));
            heroMenu.AddItem(supportedStuff);

            ZaioMenu.LoadHeroSettings(heroMenu);
        }

        public override async Task ExecuteComboAsync(Unit target, CancellationToken tk = new CancellationToken())
        {
            var w = MyHero.Spellbook.SpellW;
            if (w.CanBeCasted(target) && w.CanHit(target))
            {
                Log.Debug($"use W");
                w.UseAbility();
                await Await.Delay(100, tk);
            }

            // check if we are near the enemy
            if (!await MoveOrBlinkToEnemy(tk, 200, 700))
            {
                return;
            }
            await HasNoLinkens(target, tk);
            await UseItems(tk);

            if (await DisableEnemy(tk) == DisabledState.UsedAbilityToDisable)
            {
                Log.Debug($"disabled!");
                // return;
            }

            var q = MyHero.Spellbook.SpellQ;
            if (q.CanBeCasted(target))
            {
                Log.Debug($"use Q");
                q.UseAbility(target);
                await Await.Delay(GetAbilityDelay(target, q), tk);
            }
            var e = MyHero.Spellbook.SpellE;
            if (e.CanBeCasted(target))
            {
                Log.Debug($"use e");
                e.UseAbility(target);
                await Await.Delay(GetAbilityDelay(target, e), tk);
            }

            var ult = MyHero.Spellbook.SpellR;
            if (ult.CanBeCasted(target) && (target.IsRooted() || target.MovementSpeed < 200 || !target.IsMoving))
            {
                Log.Debug($"use ult {target.IsRooted()} | {target.IsMoving} | {target.MovementSpeed}");
                var castPoint = (float) ult.FindCastPoint();
                var pos = Prediction.Prediction.PredictPosition(target, (int) (castPoint * target.MovementSpeed));
                ult.UseAbility(pos);
                await Await.Delay(GetAbilityDelay(pos, ult), tk);
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