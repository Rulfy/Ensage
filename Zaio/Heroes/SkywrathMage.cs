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
            "skywrath_mage_mystic_flare",
            "item_veil_of_discord",
            "item_ethereal_blade",
            "item_rod_of_atos",
            "item_dagon"
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
            // check if we are near the enemy
            if (!await MoveOrBlinkToEnemy(tk, 200, 700))
            {
                return;
            }

            var atos = MyHero.FindItem("item_rod_of_atos");
            if (atos != null && atos.CanBeCasted(Target))
            {
                Log.Debug($"use atos");
                atos.UseAbility(Target);
            }

            var w = MyHero.Spellbook.SpellW;
            if (w.CanBeCasted())
            {
                Log.Debug($"use W");
                w.UseAbility();
            }

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

            var veil = MyHero.FindItem("item_veil_of_discord");
            if (veil != null && veil.CanBeCasted(Target))
            {
                Log.Debug($"use veil");
                veil.UseAbility(Target.NetworkPosition);
            }

            var eth = MyHero.FindItem("item_ethereal_blade");
            if (eth != null && eth.CanBeCasted(Target))
            {
                var speed = eth.AbilitySpecialData.First(x => x.Name == "projectile_speed").Value;
                var time = Target.Distance2D(MyHero) / speed;
                Log.Debug($"waiting for eth {time}");
                eth.UseAbility(Target);
                await Await.Delay((int) (time * 1000.0f + Game.Ping), tk);
            }

            var ult = MyHero.Spellbook.SpellR;
            if (ult.CanBeCasted(Target) && (Target.IsRooted() || Target.MovementSpeed < 200 || !Target.IsMoving))
            {
                Log.Debug($"use ult {Target.IsRooted()} | {Target.IsMoving} | {Target.MovementSpeed}");
                var castPoint = (float) ult.FindCastPoint();
                ult.UseAbility(Ensage.Common.Prediction.InFront(Target, castPoint * Target.MovementSpeed));
                await Await.Delay((int) (castPoint * 1000.0 + Game.Ping), tk);
            }

            var dagon = MyHero.Inventory.Items.FirstOrDefault(x => x.Name.StartsWith("item_dagon"));
            if (dagon != null && dagon.CanBeCasted(Target))
            {
                Log.Debug($"Use dagon");
                dagon.UseAbility(Target);
            }

            if (ZaioMenu.ShouldUseOrbwalker)
            {
                Orbwalk(300);
            }
            else
            {
                MyHero.Attack(Target);
                await Await.Delay(125, tk);
            }
        }
    }
}