using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

using Ensage;
using Ensage.Common;
using Ensage.Common.Extensions;
using Ensage.Common.Extensions.SharpDX;
using Ensage.Common.Menu;
using Ensage.Common.Threading;

using log4net;

using PlaySharp.Toolkit.Logging;

using Zaio.Helpers;
using Zaio.Interfaces;

using AbilityId = Ensage.Common.Enums.AbilityId;


namespace Zaio.Heroes
{
    [Hero(ClassID.CDOTA_Unit_Hero_AntiMage)]
    internal class AntiMage : ComboHero
    {
        private static readonly ILog Log = AssemblyLogs.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private static readonly string[] SupportedAbilities =
        {
            "antimage_blink",
            "antimage_mana_void"
        };

        private Ability _blinkAbility;

        private Ability _ultAbility;

        public override void OnLoad()
        {
            base.OnLoad();

            var heroMenu = new Menu("AntiMage", "zaioAntiMage", false, "npc_dota_hero_antimage", true);

            heroMenu.AddItem(new MenuItem("zaioAntiMageAbilitiesText", "Supported Abilities"));
            var supportedStuff = new MenuItem("zaioAntiMageAbilities", string.Empty);
            supportedStuff.SetValue(new AbilityToggler(SupportedAbilities.ToDictionary(x => x, y => true)));
            heroMenu.AddItem(supportedStuff);

            OnLoadMenuItems(supportedStuff);

            ZaioMenu.LoadHeroSettings(heroMenu);

            _blinkAbility = MyHero.GetAbilityById(AbilityId.antimage_blink);
            _ultAbility = MyHero.GetAbilityById(AbilityId.antimage_mana_void);
        }


        public override async Task ExecuteComboAsync(Unit target, CancellationToken tk = new CancellationToken())
        {

          if (!await this.MoveOrBlinkToEnemy(target, tk, 0, 150))
          {

            if (!this.MyHero.IsSilenced())
            {
                if (this._blinkAbility.CanHit(target)
                && this._blinkAbility.IsAbilityEnabled()
                && this._blinkAbility.CanBeCasted(target))
                {
                        bool usePrediction = false;

                        if (target.IsMoving)
                        {
                            usePrediction = true;
                        }
                        if (MyHero.Distance2D(target) >= 350)
                        {
                            if (!usePrediction)
                            {
                                var pos = (target.NetworkPosition - MyHero.NetworkPosition).Normalized();
                                pos *= 150;
                                pos = target.NetworkPosition - pos;
                                Log.Debug($"Jumping");
                                this._blinkAbility.UseAbility(pos);
                            }

                            if (usePrediction)
                            {
                                Log.Debug($"jumping");
                                var jump_pos = Ensage.Common.Prediction.InFront(target, 250);
                                this._blinkAbility.UseAbility(jump_pos);
                            }
                        }

                    
                  }

                
            }
          }

            await HasNoLinkens(target, tk);
            await UseItems(target, tk);
            await DisableEnemy(target, tk);

            if(!MyHero.IsSilenced())
            {
                if (_ultAbility.IsAbilityEnabled() && _ultAbility.CanBeCasted(target) && _ultAbility.CanHit(target))
                {
                    var damage = _ultAbility.GetAbilityData("mana_void_damage_per_mana");
                    var target_mana = target.Mana;
                    var total_mana = target.MaximumMana;
                    damage *= total_mana - target_mana;
                    damage = damage * (1 - target.MagicResistance());


                    if (damage > target.Health)
                    {
                        Log.Debug($"Kill it with fire");
                        this._ultAbility.UseAbility(target);
                    }
                }
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
