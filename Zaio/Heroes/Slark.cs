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
using SharpDX;
using Zaio.Helpers;
using Zaio.Interfaces;
using AbilityId = Ensage.AbilityId;
using Zaio.Prediction;


namespace Zaio.Heroes
{
    [Hero(HeroId.npc_dota_hero_slark)]
    internal class Slark : ComboHero
    {
        private static readonly ILog Log = AssemblyLogs.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private static readonly string[] SupportedAbilities =
        {
            "slark_dark_pact",
            "slark_pounce"
        };

        private Ability _jumpAbility;

        private Ability _purgeAbility;

        public override void OnLoad()
        {
            base.OnLoad();

            var heroMenu = new Menu("Slark", "zaioSlark", false, "npc_dota_hero_slark", true);

            heroMenu.AddItem(new MenuItem("zaioSlarkAbilitiesText", "Supported Abilities"));
            var supportedStuff = new MenuItem("zaioSlarkAbilities", string.Empty);
            supportedStuff.SetValue(new AbilityToggler(SupportedAbilities.ToDictionary(x => x, y => true)));
            heroMenu.AddItem(supportedStuff);

            OnLoadMenuItems(supportedStuff);

            ZaioMenu.LoadHeroSettings(heroMenu);

            _purgeAbility = MyHero.GetAbilityById(AbilityId.slark_dark_pact);
            _jumpAbility = MyHero.GetAbilityById(AbilityId.slark_pounce);
        }


        public override async Task ExecuteComboAsync(Unit target, CancellationToken tk = new CancellationToken())
        {
            if (!MyHero.IsSilenced() && _jumpAbility.IsAbilityEnabled() && _jumpAbility.CanBeCasted(target))
            {
                var radius = _jumpAbility.GetAbilityData("pounce_radius");
                var range = _jumpAbility.GetAbilityData("pounce_distance");
                var time = MyHero.Distance2D(target) / _jumpAbility.GetAbilityData("pounce_speed");
                var pos = Prediction.Prediction.PredictPosition(target, (int) (time * 1000.0f), true);
                var rec = new Geometry.Polygon.Rectangle(MyHero.NetworkPosition, MyHero.InFront(range), radius);
                if (pos != Vector3.Zero && pos.Distance2D(MyHero) <= range && rec.IsInside(pos))
                {
                    Log.Debug($"using jump");
                    _jumpAbility.UseAbility();
                    await Await.Delay((int) (_jumpAbility.FindCastPoint() * 1000.0f + Game.Ping), tk);
                }
            }
            if (!MyHero.IsSilenced() && _purgeAbility.IsAbilityEnabled() && _purgeAbility.CanBeCasted(target) && _purgeAbility.CanHit(target) ||
                MyHero.IsRooted())
            {
                Log.Debug($"using Q");
                _purgeAbility.UseAbility();
                await Await.Delay((int) (_purgeAbility.FindCastPoint() * 1000.0f + Game.Ping), tk);
            }

            await HasNoLinkens(target, tk);
            await UseItems(target, tk);

            // make him disabled
            if (await DisableEnemy(target, tk) == DisabledState.UsedAbilityToDisable)
            {
                Log.Debug($"disabled!");
                // return;
            }

            // check if we are near the enemy
            if (!await MoveOrBlinkToEnemy(target, tk))
            {
                Log.Debug($"return because of blink");
                return;
            }

            if (ZaioMenu.ShouldUseOrbwalker)
            {
                Orbwalk();
            }
        }
    }
}