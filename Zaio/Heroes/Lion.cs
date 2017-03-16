using System;
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
using AbilityId = Ensage.Common.Enums.AbilityId;

namespace Zaio.Heroes
{
    [Hero(ClassID.CDOTA_Unit_Hero_Lion)]
    internal class Lion : ComboHero
    {
        private static readonly ILog Log = AssemblyLogs.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private static readonly string[] SupportedAbilities =
        {
            "lion_impale",
            "lion_voodoo",
            "lion_mana_drain",
            "lion_finger_of_death"
        };

        private static readonly string[] KillstealAbilities =
        {
            "lion_impale",
            "lion_finger_of_death"
        };

        private Ability _manaAbility;
        private Ability _stunAbility;
        private Ability _hexAbility;

        private Ability _ultAbility;

        public override void OnLoad()
        {
            base.OnLoad();

            var heroMenu = new Menu("Lion", "zaioLion", false, "npc_dota_hero_lion", true);

            heroMenu.AddItem(new MenuItem("zaioLionAbilitiesText", "Supported Abilities"));
            var supportedStuff = new MenuItem("zaioLionAbilities", string.Empty);
            supportedStuff.SetValue(new AbilityToggler(SupportedAbilities.ToDictionary(x => x, y => true)));
            heroMenu.AddItem(supportedStuff);

            heroMenu.AddItem(new MenuItem("zaioLionKillstealAbilitiesText", "Supported Killsteal Abilities"));
            var supportedKillsteal = new MenuItem("zaioLionKillstealAbilities", string.Empty);
            supportedKillsteal.SetValue(new AbilityToggler(KillstealAbilities.ToDictionary(x => x, y => true)));
            heroMenu.AddItem(supportedKillsteal);

            OnLoadMenuItems(supportedStuff, supportedKillsteal);

            ZaioMenu.LoadHeroSettings(heroMenu);

            _ultAbility = MyHero.GetAbilityById(AbilityId.lion_finger_of_death);
            _manaAbility = MyHero.GetAbilityById(AbilityId.lion_mana_drain);
            _stunAbility = MyHero.GetAbilityById(AbilityId.lion_impale);
            _hexAbility = MyHero.GetAbilityById(AbilityId.lion_voodoo);
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

            if (_ultAbility.IsKillstealAbilityEnabled() && _ultAbility.CanBeCasted())
            {
                var damage =
                    _ultAbility.GetAbilityData(MyHero.HasItem(ClassID.CDOTA_Item_UltimateScepter)
                        ? "damage_scepter"
                        : "damage");
                damage *= GetSpellAmp();
                var enemy =
                    ObjectManager.GetEntitiesParallel<Hero>()
                                 .FirstOrDefault(
                                     x =>
                                         x.IsAlive && x.Team != MyHero.Team && !x.IsIllusion &&
                                         _ultAbility.CanBeCasted(x) &&
                                         _ultAbility.CanHit(x) &&
                                         x.Health < damage * (1 - x.MagicResistance()) && !x.IsLinkensProtected() &&
                                         !x.CantBeAttacked() && !x.CantBeKilled());
                if (enemy != null)
                {
                    Log.Debug(
                        $"use killsteal ult because enough damage {enemy.Health} <= {damage * (1.0f - enemy.MagicResistance())} ");
                    _ultAbility.UseAbility(enemy);
                    await Await.Delay(GetAbilityDelay(enemy, _ultAbility));
                    return true;
                }
            }

            if (Target != null)
            {
                return false;
            }

            if (_stunAbility.IsKillstealAbilityEnabled() && _stunAbility.CanBeCasted())
            {
                var damage = (float)_stunAbility.GetDamage(_stunAbility.Level - 1);
                damage *= GetSpellAmp();

                var enemy =
                    ObjectManager.GetEntitiesParallel<Hero>()
                                 .FirstOrDefault(
                                     x =>
                                         x.IsAlive && x.Team != MyHero.Team && !x.IsIllusion &&
                                         _stunAbility.CanBeCasted(x) &&
                                         _stunAbility.CanHit(x) &&
                                         x.Health < damage * (1 - x.MagicResistance()) && !x.IsLinkensProtected() &&
                                         !x.CantBeAttacked() && !x.CantBeKilled());
                if (enemy != null)
                {
                    var castPoint = _stunAbility.FindCastPoint();
                    var speed = _stunAbility.GetAbilityData("speed");
                    var time = (castPoint + enemy.Distance2D(MyHero) / speed) * 1000.0f;

                    var predictedPos = Prediction.Prediction.PredictPosition(enemy, (int) time);
                    Log.Debug(
                        $"use killsteal stun because enough damage {enemy.Health} <= {damage * (1.0f - enemy.MagicResistance())} ");
                    _stunAbility.UseAbility(predictedPos);
                    await Await.Delay(GetAbilityDelay(predictedPos, _stunAbility));
                    return true;
                }
            }

            return false;
        }

        public override async Task ExecuteComboAsync(Unit target, CancellationToken tk = new CancellationToken())
        {
            await UseItems(target, tk);

            if (_ultAbility.IsAbilityEnabled() && !MyHero.IsSilenced() && _ultAbility.CanBeCasted(target) && _ultAbility.CanHit(target) &&
                await HasNoLinkens(target, tk))
            {
                var damage =
                    _ultAbility.GetAbilityData(MyHero.HasItem(ClassID.CDOTA_Item_UltimateScepter)
                        ? "damage_scepter"
                        : "damage");
                if (target.Health <= damage * (1.0f - target.MagicResistance()))
                {
                    Log.Debug(
                        $"use ult because enough damage {target.Health} <= {damage * (1.0f - target.MagicResistance())} ");
                    _ultAbility.UseAbility(target);
                    await Await.Delay(GetAbilityDelay(target, _ultAbility), tk);
                }
            }

            // make him disabled
            if (await DisableEnemy(target, tk) == DisabledState.UsedAbilityToDisable)
            {
                Log.Debug($"disabled!");
                // return;
            }
            float maxRange = 500;

            if (!MyHero.IsSilenced())
            {
                float duration;
                if (!(target.IsHexed(out duration) || target.IsStunned(out duration)) || duration < 1.2)
                {
                    maxRange = Math.Max(maxRange, _hexAbility.CastRange);
                    if (_hexAbility.IsAbilityEnabled() && _hexAbility.CanBeCasted(target))
                    {
                        if (_hexAbility.CanHit(target))
                        {
                            Log.Debug($"use hex {duration}");
                            _hexAbility.UseAbility(target);
                            await Await.Delay(GetAbilityDelay(target, _hexAbility), tk);
                            return;
                        }
                        if (!await MoveOrBlinkToEnemy(target, tk, minimumRange: 250, maximumRange: _hexAbility.GetCastRange()))
                        {
                            Log.Debug($"return because of blink and hex ready");
                            return;
                        }
                    }

                    maxRange = Math.Max(maxRange, _stunAbility.CastRange);
                    if (_stunAbility.IsAbilityEnabled() && _stunAbility.CanBeCasted(target))
                    {
                        if (_stunAbility.CanHit(target))
                        {
                            var castPoint = _stunAbility.FindCastPoint();
                            var speed = _stunAbility.GetAbilityData("speed");
                            var time = (castPoint + target.Distance2D(MyHero) / speed) * 1000.0f;

                            var predictedPos = Prediction.Prediction.PredictPosition(target, (int) time);
                            if (MyHero.Distance2D(predictedPos) <= _stunAbility.GetCastRange())
                            {
                                Log.Debug($"use stun {duration} | {time}");
                                _stunAbility.UseAbility(predictedPos);
                                await Await.Delay(GetAbilityDelay(predictedPos, _stunAbility), tk);
                                return;
                            }
                        }
                        else
                        {
                            if (!await MoveOrBlinkToEnemy(target, tk, minimumRange: 250, maximumRange: _stunAbility.GetCastRange()))
                            {
                                Log.Debug($"return because of blink and stun ready");
                                return;
                            }
                        }
                    }
                }

                if (_ultAbility.IsAbilityEnabled() && _ultAbility.CanBeCasted(target) && _ultAbility.CanHit(target) && await HasNoLinkens(target, tk))
                {
                    if (target.IsHexed() || target.IsStunned() ||
                        (float) target.Health / target.MaximumHealth * (1.0f + target.MagicResistance()) < 0.5f)
                    {
                        Log.Debug($"use ult");
                        _ultAbility.UseAbility(target);
                        await Await.Delay(GetAbilityDelay(target, _ultAbility), tk);
                    }
                }

                if (_manaAbility.IsAbilityEnabled() && _manaAbility.CanBeCasted())
                {
                    var illusion =
                        ObjectManager.GetEntitiesFast<Unit>()
                                     .FirstOrDefault(
                                         x =>
                                             x.IsAlive && x.IsIllusion && x.Team != MyHero.Team &&
                                             x.Distance2D(MyHero) <= _manaAbility.CastRange);
                    if (illusion != null)
                    {
                        Log.Debug($"use mana leech on illusion");
                        _manaAbility.UseAbility(illusion);
                        await Await.Delay(GetAbilityDelay(illusion, _manaAbility), tk);
                    }
                }
            }

            // check if we are near the enemy
            if (!await MoveOrBlinkToEnemy(target, tk, minimumRange: 200, maximumRange: maxRange))
            {
                Log.Debug($"return because of blink");
                return;
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