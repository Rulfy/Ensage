using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using Ensage;
using Ensage.Common.Combo;
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

        private Ability _chargeAbility;

        private Combo _chargeAway;

        private MenuItem _chargeAwayKey;
        private Ability _ultAbility;

        public override void OnLoad()
        {
            base.OnLoad();

            var heroMenu = new Menu("SpiritBreaker", "zaioSpiritBreaker", false, "npc_dota_hero_spirit_breaker", true);

            heroMenu.AddItem(new MenuItem("zaioSpiritBreakerAbilitiesText", "Supported Abilities"));
            var supportedStuff = new MenuItem("zaioSpiritBreakerAbilities", string.Empty);
            supportedStuff.SetValue(new AbilityToggler(SupportedAbilities.ToDictionary(x => x, y => true)));
            heroMenu.AddItem(supportedStuff);

            _chargeAwayKey =
                new MenuItem("zaioSpiritBreakerChargeAway", "Charge Away").SetValue(new KeyBind(0, KeyBindType.Press));
            _chargeAwayKey.Tooltip = "Hotkey for charging away.";
            _chargeAwayKey.ValueChanged += _chargeAwayKey_ValueChanged;
            ;
            heroMenu.AddItem(_chargeAwayKey);

            ZaioMenu.LoadHeroSettings(heroMenu);

            _chargeAbility = MyHero.GetAbilityById(AbilityId.spirit_breaker_charge_of_darkness);
            _ultAbility = MyHero.GetAbilityById(AbilityId.spirit_breaker_nether_strike);

            _chargeAway = new Combo(ChargeAwayFunc,
                KeyInterop.KeyFromVirtualKey((int) _chargeAwayKey.GetValue<KeyBind>().Key));
            _chargeAway.Activate();
        }

        public override void OnClose()
        {
            _chargeAway.Deactivate();
            base.OnClose();
        }

        private async Task ChargeAwayFunc(CancellationToken tk)
        {
            if (!MyHero.IsSilenced() && _chargeAbility.CanBeCasted() && !MyHero.IsChanneling())
            {
                var enemy = ObjectManager.GetEntitiesParallel<Unit>().Where(
                                             x =>
                                                 x.IsValid && x.IsAlive && x.Team != MyHero.Team && !(x is Building) &&
                                                 x.IsRealUnit() && _chargeAbility.CanBeCasted(x))
                                         .OrderByDescending(x => x.Distance2D(MyHero))
                                         .FirstOrDefault();
                if (enemy != null)
                {
                    Log.Debug($"Using charge away on {enemy.Name}");
                    _chargeAbility.UseAbility(enemy);
                    await Await.Delay((int) (_chargeAbility.FindCastPoint() * 1000.0 + Game.Ping), tk);
                }
            }
        }

        private void _chargeAwayKey_ValueChanged(object sender, OnValueChangeEventArgs e)
        {
            var newKey = e.GetNewValue<KeyBind>().Key;
            if (e.GetOldValue<KeyBind>().Key != newKey)
            {
                _chargeAway.Key = KeyInterop.KeyFromVirtualKey((int) newKey);
            }
        }

        public override async Task ExecuteComboAsync(Unit target, CancellationToken tk = new CancellationToken())
        {
            if (MyHero.HasModifier("modifier_spirit_breaker_charge_of_darkness"))
            {
                if (!MyHero.IsInvisible())
                {
                    var shadowBlade = MyHero.GetItemById(ItemId.item_invis_sword) ??
                                      MyHero.GetItemById(ItemId.item_silver_edge);
                    var distance = MyHero.Distance2D(target);
                    if (shadowBlade != null && shadowBlade.CanBeCasted() && !MyHero.IsVisibleToEnemies &&
                        distance > 1200 && distance < 6000)
                    {
                        Log.Debug($"using invis");
                        shadowBlade.UseAbility();
                        var fadeTime = shadowBlade.GetAbilityData("windwalk_fade_time") * 2 * 1000; // 0.3
                        await Await.Delay((int) fadeTime, tk);
                    }
                }
                return;
            }
            // check if we are near the enemy
            if (!await MoveOrBlinkToEnemy(tk))
            {
                if (!MyHero.IsSilenced() && _chargeAbility.CanBeCasted())
                {
                    Log.Debug($"charging enemy since too far");
                    _chargeAbility.UseAbility(target);
                    await Await.Delay(GetAbilityDelay(target, _chargeAbility) + 250, tk);
                }

                return;
            }
            await HasNoLinkens(target, tk);
            await UseItems(tk);

            // make him disabled
            if (await DisableEnemy(tk, _ultAbility.CanBeCasted(target) ? GetAbilityDelay(target, _ultAbility) : 0) ==
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

            if (!MyHero.IsSilenced() && _ultAbility.CanBeCasted() && _ultAbility.CanHit(target))
            {
                Log.Debug($"using ult on target");
                _ultAbility.UseAbility(target);
                await Await.Delay(GetAbilityDelay(target, _ultAbility), tk);
            }

            var mask = MyHero.GetItemById(ItemId.item_mask_of_madness);
            if (mask != null && mask.CanBeCasted() && _ultAbility.Cooldown > 0)
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
                MyHero.Attack(target);
                await Await.Delay(125, tk);
            }
        }
    }
}