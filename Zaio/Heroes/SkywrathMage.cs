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
            if (w.CanBeCasted(Target) && w.CanHit(Target))
            {
                Log.Debug($"use W");
                w.UseAbility();
            }

            // check if we are near the enemy
            if (!await MoveOrBlinkToEnemy(tk, 200, 700))
            {
                return;
            }

            await UseItems(tk);

            if (DisableEnemy(tk) == DisabledState.UsedAbilityToDisable)
            {
                Log.Debug($"disabled!");
                // return;
            }

            var q = MyHero.Spellbook.SpellQ;
            if (q.CanBeCasted(Target))
            {
                Log.Debug($"use Q");
                q.UseAbility(Target);
                await Await.Delay((int) (q.FindCastPoint() * 1000.0 + Game.Ping), tk);
            }
            var e = MyHero.Spellbook.SpellE;
            if (e.CanBeCasted(Target))
            {
                Log.Debug($"use e");
                e.UseAbility(Target);
                await Await.Delay((int) (e.FindCastPoint() * 1000.0 + Game.Ping), tk);
            }

            var ult = MyHero.Spellbook.SpellR;
            if (ult.CanBeCasted(Target) && (Target.IsRooted() || Target.MovementSpeed < 200 || !Target.IsMoving))
            {
                Log.Debug($"use ult {Target.IsRooted()} | {Target.IsMoving} | {Target.MovementSpeed}");
                var castPoint = (float) ult.FindCastPoint();
                ult.UseAbility(Ensage.Common.Prediction.InFront(Target, castPoint * Target.MovementSpeed));
                await Await.Delay((int) (castPoint * 1000.0 + Game.Ping), tk);
            }

            if (ZaioMenu.ShouldUseOrbwalker)
            {
                Orbwalk(450);
            }
            else
            {
                MyHero.Attack(Target);
                await Await.Delay(125, tk);
            }
        }
    }
}