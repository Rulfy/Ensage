using System;
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

            ZaioMenu.LoadHeroSettings(heroMenu);
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

            var ult = MyHero.Spellbook.SpellR;
            if (ult.CanBeCasted())
            {
                var damage =
                    ult.GetAbilityData(MyHero.HasItem(ClassID.CDOTA_Item_UltimateScepter) ? "damage_scepter" : "damage");
                damage *= GetSpellAmp();
                var enemy =
                    ObjectManager.GetEntitiesParallel<Hero>()
                                 .FirstOrDefault(
                                     x =>
                                         x.IsAlive && x.Team != MyHero.Team && !x.IsIllusion && ult.CanBeCasted(x) &&
                                         ult.CanHit(x) &&
                                         x.Health < damage * (1 - x.MagicResistance()) && !x.IsLinkensProtected() &&
                                         !x.CantBeAttacked() && !x.CantBeKilled());
                if (enemy != null)
                {
                    Log.Debug(
                        $"use killsteal ult because enough damage {enemy.Health} <= {damage * (1.0f - enemy.MagicResistance())} ");
                    ult.UseAbility(enemy);
                    await Await.Delay(GetAbilityDelay(enemy, ult));
                    return true;
                }
            }

            if (Target != null)
            {
                return false;
            }

            var stun = MyHero.Spellbook.SpellQ;
            if (stun.CanBeCasted())
            {
                var damage = (float) stun.GetDamage(stun.Level - 1);
                damage *= GetSpellAmp();

                var enemy =
                    ObjectManager.GetEntitiesParallel<Hero>()
                                 .FirstOrDefault(
                                     x =>
                                         x.IsAlive && x.Team != MyHero.Team && !x.IsIllusion && stun.CanBeCasted(x) &&
                                         stun.CanHit(x) &&
                                         x.Health < damage * (1 - x.MagicResistance()) && !x.IsLinkensProtected() &&
                                         !x.CantBeAttacked() && !x.CantBeKilled());
                if (enemy != null)
                {
                    var castPoint = stun.FindCastPoint();
                    var speed = stun.GetAbilityData("speed");
                    var time = (castPoint + enemy.Distance2D(MyHero) / speed) * 1000.0f;

                    var predictedPos = Prediction.Prediction.PredictPosition(enemy, (int) time);
                    Log.Debug(
                        $"use killsteal stun because enough damage {enemy.Health} <= {damage * (1.0f - enemy.MagicResistance())} ");
                    stun.UseAbility(predictedPos);
                    await Await.Delay(GetAbilityDelay(predictedPos, stun));
                    return true;
                }
            }

            return false;
        }

        public override async Task ExecuteComboAsync(Unit target, CancellationToken tk = new CancellationToken())
        {
            await UseItems(tk);

            var ult = MyHero.Spellbook.SpellR;
            if (ult.CanBeCasted(target) && ult.CanHit(target) && await HasNoLinkens(target, tk))
            {
                var damage =
                    ult.GetAbilityData(MyHero.HasItem(ClassID.CDOTA_Item_UltimateScepter) ? "damage_scepter" : "damage");
                if (target.Health <= damage * (1.0f - target.MagicResistance()))
                {
                    Log.Debug(
                        $"use ult because enough damage {target.Health} <= {damage * (1.0f - target.MagicResistance())} ");
                    ult.UseAbility(target);
                    await Await.Delay(GetAbilityDelay(target, ult), tk);
                }
            }

            // make him disabled
            if (await DisableEnemy(tk) == DisabledState.UsedAbilityToDisable)
            {
                Log.Debug($"disabled!");
                // return;
            }
            float maxRange = 500;
            float duration;
            if (!(target.IsHexed(out duration) || target.IsStunned(out duration)) || duration < 1.2)
            {
                var hex = MyHero.Spellbook.SpellW;
                maxRange = Math.Max(maxRange, hex.CastRange);
                if (hex.CanBeCasted(target))
                {
                    if (hex.CanHit(target))
                    {
                        Log.Debug($"use hex {duration}");
                        hex.UseAbility(target);
                        await Await.Delay(GetAbilityDelay(target, hex), tk);
                        return;
                    }
                    if (!await MoveOrBlinkToEnemy(tk, 250, hex.GetCastRange()))
                    {
                        Log.Debug($"return because of blink and hex ready");
                        return;
                    }
                }

                var stun = MyHero.Spellbook.SpellQ;
                maxRange = Math.Max(maxRange, stun.CastRange);
                if (stun.CanBeCasted(target))
                {
                    if (stun.CanHit(target))
                    {
                        var castPoint = stun.FindCastPoint();
                        var speed = stun.GetAbilityData("speed");
                        var time = (castPoint + target.Distance2D(MyHero) / speed) * 1000.0f;

                        var predictedPos = Prediction.Prediction.PredictPosition(target, (int) time);
                        if (MyHero.Distance2D(predictedPos) <= stun.GetCastRange())
                        {
                            Log.Debug($"use stun {duration} | {time}");
                            stun.UseAbility(predictedPos);
                            await Await.Delay(GetAbilityDelay(predictedPos, stun), tk);
                            return;
                        }
                    }
                    else
                    {
                        if (!await MoveOrBlinkToEnemy(tk, 250, stun.GetCastRange()))
                        {
                            Log.Debug($"return because of blink and stun ready");
                            return;
                        }
                    }
                }
            }

            if (ult.CanBeCasted(target) && ult.CanHit(target) && await HasNoLinkens(target, tk))
            {
                if (target.IsHexed() || target.IsStunned() ||
                    (float) target.Health / target.MaximumHealth * (1.0f + target.MagicResistance()) < 0.5f)
                {
                    Log.Debug($"use ult");
                    ult.UseAbility(target);
                    await Await.Delay(GetAbilityDelay(target, ult), tk);
                }
            }

            var mana = MyHero.Spellbook.SpellE;
            if (mana.CanBeCasted())
            {
                var illusion =
                    ObjectManager.GetEntitiesFast<Unit>()
                                 .FirstOrDefault(
                                     x =>
                                         x.IsAlive && x.IsIllusion && x.Team != MyHero.Team &&
                                         x.Distance2D(MyHero) <= mana.CastRange);
                if (illusion != null)
                {
                    Log.Debug($"use mana leech on illusion");
                    mana.UseAbility(illusion);
                    await Await.Delay(GetAbilityDelay(illusion, mana), tk);
                }
            }

            // check if we are near the enemy
            if (!await MoveOrBlinkToEnemy(tk, 200, maxRange))
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