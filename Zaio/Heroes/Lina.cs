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
    [Hero(ClassId.CDOTA_Unit_Hero_Lina)]
    internal class Lina : ComboHero
    {
        private static readonly ILog Log = AssemblyLogs.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private static readonly string[] SupportedAbilities =
        {
            "lina_dragon_slave",
            "lina_light_strike_array",
            "lina_laguna_blade",
            "item_cyclone"
        };

        private static readonly string[] KillstealAbilities =
        {
            "lina_dragon_slave",
            "lina_laguna_blade"
        };

        private Ability _slaveAbility;

        private Ability _stunAbility;
        private Ability _ultAbility;

        public override void OnLoad()
        {
            base.OnLoad();

            var heroMenu = new Menu("Lina", "zaioLinak", false, "npc_dota_hero_lina", true);

            heroMenu.AddItem(new MenuItem("zaioLinaAbilitiesText", "Supported Abilities"));
            var supportedStuff = new MenuItem("zaioLinaAbilities", string.Empty);
            supportedStuff.SetValue(new AbilityToggler(SupportedAbilities.ToDictionary(x => x, y => true)));
            heroMenu.AddItem(supportedStuff);

            heroMenu.AddItem(new MenuItem("zaioLinaKillstealAbilitiesText", "Supported Killsteal Abilities"));
            var supportedKillsteal = new MenuItem("zaioLinaKillstealAbilities", string.Empty);
            supportedKillsteal.SetValue(new AbilityToggler(KillstealAbilities.ToDictionary(x => x, y => true)));
            heroMenu.AddItem(supportedKillsteal);

            OnLoadMenuItems(supportedStuff, supportedKillsteal);

            ZaioMenu.LoadHeroSettings(heroMenu);

            _stunAbility = MyHero.GetAbilityById(AbilityId.lina_light_strike_array);
            _slaveAbility = MyHero.GetAbilityById(AbilityId.lina_dragon_slave);
            _ultAbility = MyHero.GetAbilityById(AbilityId.lina_laguna_blade);
        }

        protected override async Task<bool> Killsteal()
        {
            if (await base.Killsteal())
            {
                return true;
            }

            if (MyHero.IsSilenced())
            {
                return false;
            }

            if (_ultAbility.IsKillstealAbilityEnabled() &&_ultAbility.CanBeCasted())
            {
                var damage = _ultAbility.GetAbilityData("damage");
                var hasScepter = MyHero.HasItem(ItemId.item_ultimate_scepter);
                damage *= GetSpellAmp();
                var enemy =
                    ObjectManager.GetEntitiesParallel<Hero>()
                                 .FirstOrDefault(
                                     x =>
                                         x.IsAlive && x.Team != MyHero.Team && !x.IsIllusion &&
                                         _ultAbility.CanBeCasted(x) &&
                                         _ultAbility.CanHit(x) && (hasScepter || !x.IsMagicImmune()) &&
                                         x.Health < damage * (hasScepter ? 1 : 1 - x.MagicResistance()) &&
                                         !x.IsLinkensProtected() && !x.CantBeAttacked() && !x.CantBeKilled());
                if (enemy != null)
                {
                    Log.Debug(
                        $"use killsteal ult because enough damage {enemy.Health} <= {damage * (hasScepter ? 1 : 1 - enemy.MagicResistance())} ");
                    _ultAbility.UseAbility(enemy);
                    await Await.Delay(GetAbilityDelay(enemy, _ultAbility));
                    return true;
                }
            }

            if (_slaveAbility.IsKillstealAbilityEnabled() &&_slaveAbility.CanBeCasted())
            {
                var damage = (float) _slaveAbility.GetDamage(_slaveAbility.Level - 1);
                damage *= GetSpellAmp();

                var enemy =
                    ObjectManager.GetEntitiesParallel<Hero>()
                                 .FirstOrDefault(
                                     x =>
                                         x.IsAlive && x.Team != MyHero.Team && !x.IsIllusion &&
                                         _slaveAbility.CanBeCasted(x) &&
                                         _slaveAbility.CanHit(x) && !x.IsMagicImmune() &&
                                         x.Health < damage * (1 - x.MagicResistance()) && !x.CantBeAttacked() &&
                                         !x.CantBeKilled());
                if (enemy != null)
                {
                    var castPoint = _slaveAbility.FindCastPoint();
                    var speed = _slaveAbility.GetAbilityData("dragon_slave_speed");
                    var time = (castPoint + enemy.Distance2D(MyHero) / speed) * 1000.0f;

                    var predictedPos = Prediction.Prediction.PredictPosition(enemy, (int) time);
                    Log.Debug(
                        $"use killsteal Q because enough damage {enemy.Health} <= {damage * (1.0f - enemy.MagicResistance())} ");
                    _slaveAbility.UseAbility(predictedPos);

                    await Await.Delay(GetAbilityDelay(enemy, _slaveAbility));
                    return true;
                }
            }

            return false;
        }

        public override async Task ExecuteComboAsync(Unit target, CancellationToken tk = new CancellationToken())
        {
            var eulsModifier = target.FindModifier("modifier_eul_cyclone");
            if (_stunAbility.IsAbilityEnabled() && (_stunAbility.CanBeCasted(target) || eulsModifier != null && _stunAbility.CanBeCasted()) &&
                _stunAbility.CanHit(target))
            {
                var stunCastpoint = _stunAbility.FindCastPoint();
                var delay = _stunAbility.GetAbilityData("light_strike_array_delay_time");

                if (eulsModifier != null)
                {
                    Log.Debug($"has euls {eulsModifier.RemainingTime}");
                    if (!MyHero.IsSilenced() && eulsModifier.RemainingTime < stunCastpoint + delay)
                    {
                        Log.Debug($"using stun on cycloned target");
                        _stunAbility.UseAbility(target.NetworkPosition);
                        await Await.Delay(GetAbilityDelay(target, _stunAbility), tk);
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
                            await Await.Delay(GetAbilityDelay(target, _stunAbility), tk);
                        }
                        else
                        {
                            var predictedPos = Prediction.Prediction.PredictPosition(target, (int) time * -1000);

                            Log.Debug($"using stun on disabled target {time} with predicted pos {predictedPos}");
                            _stunAbility.UseAbility(predictedPos);
                            await Await.Delay(GetAbilityDelay(target, _stunAbility), tk);
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
                        }

                        var predictedPos = Prediction.Prediction.PredictPosition(target,
                            (int) ((stunCastpoint + delay) * 1000), true);
                        if (!MyHero.IsSilenced() && predictedPos != Vector3.Zero)
                        {
                            Log.Debug($"using stun on target with predicted pos {predictedPos}");
                            _stunAbility.UseAbility(predictedPos);
                            await Await.Delay(GetAbilityDelay(target, _stunAbility), tk);
                        }
                        else
                        {
                            Log.Debug($"Not using stun due to enemy turning!");
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
                if (_slaveAbility.IsAbilityEnabled() && _slaveAbility.CanBeCasted(target) && _slaveAbility.CanHit(target) && !_stunAbility.CanBeCasted() &&
                    _stunAbility.CanHit(target))
                {
                    var castPoint = _slaveAbility.FindCastPoint();
                    var speed = _slaveAbility.GetAbilityData("dragon_slave_speed");
                    var time = (castPoint + target.Distance2D(MyHero) / speed) * 1000.0f;

                    var predictedPos = Prediction.Prediction.PredictPosition(target, (int) time);
                    Log.Debug($"using q");
                    _slaveAbility.UseAbility(predictedPos);

                    await Await.Delay(GetAbilityDelay(target, _slaveAbility), tk);
                }

                if (_ultAbility.IsAbilityEnabled() && _ultAbility.CanBeCasted(target) && _ultAbility.CanHit(target) && await HasNoLinkens(target, tk))
                {
                    if (target.IsHexed() || target.IsStunned() ||
                        (float) target.Health / target.MaximumHealth * (1.0f + target.MagicResistance()) < 0.5f)
                    {
                        Log.Debug($"using ult");
                        _ultAbility.UseAbility(target);
                        await Await.Delay(GetAbilityDelay(target, _ultAbility), tk);
                    }
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