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


namespace Zaio.Heroes
{
    [Hero(HeroId.npc_dota_hero_leshrac)]
    internal class Leshrac : ComboHero
    {
        private static readonly ILog Log = AssemblyLogs.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private static readonly string[] SupportedAbilities =
        {
            "leshrac_split_earth",
            "leshrac_diabolic_edict",
            "leshrac_lightning_storm",
            "leshrac_pulse_nova",
            "item_cyclone"
        };

        private Ability _edictAbility;
        private Ability _lightningAbility;

        private Ability _stunAbility;
        private Ability _ultAbility;

        public override void OnLoad()
        {
            base.OnLoad();

            var heroMenu = new Menu("Leshrak", "zaioLeshrak", false, "npc_dota_hero_leshrak", true);

            heroMenu.AddItem(new MenuItem("zaioLeshrakAbilitiesText", "Supported Abilities"));
            var supportedStuff = new MenuItem("zaioLeshrakAbilities", string.Empty);
            supportedStuff.SetValue(new AbilityToggler(SupportedAbilities.ToDictionary(x => x, y => true)));
            heroMenu.AddItem(supportedStuff);

            OnLoadMenuItems(supportedStuff);

            ZaioMenu.LoadHeroSettings(heroMenu);

            _stunAbility = MyHero.GetAbilityById(AbilityId.leshrac_split_earth);
            _ultAbility = MyHero.GetAbilityById(AbilityId.leshrac_pulse_nova);
            _edictAbility = MyHero.GetAbilityById(AbilityId.leshrac_diabolic_edict);
            _lightningAbility = MyHero.GetAbilityById(AbilityId.leshrac_lightning_storm);
        }

        public override async Task ExecuteComboAsync(Unit target, CancellationToken tk = new CancellationToken())
        {
            await HasNoLinkens(target, tk);


            var eulsModifier = target.FindModifier("modifier_eul_cyclone");
            if (_stunAbility.IsAbilityEnabled() && (_stunAbility.CanBeCasted(target) || eulsModifier != null && _stunAbility.CanBeCasted()) &&
                _stunAbility.CanHit(target))
            {
                var stunCastpoint = _stunAbility.FindCastPoint();
                var delay = _stunAbility.GetAbilityData("delay");

                if (eulsModifier != null)
                {
                    Log.Debug($"has euls {eulsModifier.RemainingTime}");
                    if (!MyHero.IsSilenced() && eulsModifier.RemainingTime < stunCastpoint + delay)
                    {
                        Log.Debug($"using stun on cycloned target");
                        _stunAbility.UseAbility(target.NetworkPosition);
                        await Await.Delay(GetAbilityDelay(target, _stunAbility) + 250, tk);
                    }
                }
                else
                {
                    var disabled = 0.0f;
                    if (!MyHero.IsSilenced() && target.IsRooted(out disabled) || target.IsStunned(out disabled))
                    {
                        var time = disabled - stunCastpoint - delay;
                        if (time >= 0)
                        {
                            Log.Debug($"using stun on disabled target {time}");
                            _stunAbility.UseAbility(target.NetworkPosition);
                            await Await.Delay(GetAbilityDelay(target, _stunAbility) + 250, tk);
                        }
                        else
                        {
                            var predictedPos = Prediction.Prediction.PredictPosition(target, (int) time * -1000);

                            Log.Debug($"using stun on disabled target {time} with predicted pos {predictedPos}");
                            _stunAbility.UseAbility(predictedPos);
                            await Await.Delay(GetAbilityDelay(target, _stunAbility) + 250, tk);
                        }
                    }
                    else
                    {
                        var euls = MyHero.GetItemById(ItemId.item_cyclone);
                        if (euls != null && euls.IsAbilityEnabled() && euls.CanBeCasted(target))
                        {
                            if (euls.CanHit(target))
                            {
                                Log.Debug($"using euls to disable enemy before stun");
                                euls.UseAbility(target);
                                await Await.Delay(125, tk);
                                return;
                            }
                            // check if we are near the enemy
                            if (!await MoveOrBlinkToEnemy(target, tk, minimumRange: 250, maximumRange: euls.GetCastRange()))
                            {
                                Log.Debug($"return because of blink and euls ready");
                                return;
                            }
                            Log.Debug($"ELSE");
                        }


                        var predictedPos = Prediction.Prediction.PredictPosition(target,
                            (int) ((stunCastpoint + delay) * 1000), true);
                        if (!MyHero.IsSilenced() && predictedPos != Vector3.Zero)
                        {
                            Log.Debug($"using stun on target with predicted pos {predictedPos}");
                            _stunAbility.UseAbility(predictedPos);
                            await Await.Delay(GetAbilityDelay(target, _stunAbility) + 250, tk);
                        }
                        else
                        {
                            Log.Debug($"Not using stun due to enemy turning or silenced!");
                        }
                    }
                }
            }

            await UseItems(target, tk);

            // make him disabled
            if (await DisableEnemy(target, tk) == DisabledState.UsedAbilityToDisable)
            {
                Log.Debug($"disabled!");
                // return;
            }

            if (!MyHero.IsSilenced())
            {
                if (_ultAbility.IsAbilityEnabled() && !_ultAbility.IsToggled && _ultAbility.CanBeCasted(target) && _ultAbility.CanHit(target))
                {
                    Log.Debug($"using ult");
                    _ultAbility.ToggleAbility();
                    await Await.Delay((int) (_ultAbility.FindCastPoint() * 1000.0 + Game.Ping), tk);
                }

                if (_edictAbility.IsAbilityEnabled() && _edictAbility.CanBeCasted(target) && _edictAbility.CanHit(target))
                {
                    Log.Debug($"using edict");
                    _edictAbility.UseAbility();
                    await Await.Delay((int) (_edictAbility.FindCastPoint() * 1000.0 + Game.Ping), tk);
                }

                if (_lightningAbility.IsAbilityEnabled() && _lightningAbility.CanBeCasted(target) && _lightningAbility.CanHit(target))
                {
                    Log.Debug($"using lightning");
                    _lightningAbility.UseAbility(target);
                    await Await.Delay(GetAbilityDelay(target, _lightningAbility), tk);
                }
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