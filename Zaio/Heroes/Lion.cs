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
                                         x.IsAlive && x.Team != MyHero.Team && ult.CanBeCasted(x) && ult.CanHit(x) &&
                                         x.Health < damage * (1 - x.MagicDamageResist));
                if (enemy != null && HasNoLinkens(enemy))
                {
                    Log.Debug(
                        $"use killsteal ult because enough damage {enemy.Health} <= {damage * (1.0f - enemy.MagicDamageResist)} ");
                    ult.UseAbility(enemy);
                    await Await.Delay((int) (ult.FindCastPoint() * 1000.0 + Game.Ping));
                    return true;
                }
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
                                         x.IsAlive && x.Team != MyHero.Team && stun.CanBeCasted(x) && stun.CanHit(x) &&
                                         x.Health < damage * (1 - x.MagicDamageResist));
                if (enemy != null)
                {
                    var speed = stun.GetAbilityData("speed");
                    var time = enemy.Distance2D(MyHero) / speed * 1000.0f;

                    var predictedPos = Prediction.Prediction.PredictPosition(enemy, (int) time);
                    Log.Debug(
                        $"use killsteal stun because enough damage {enemy.Health} <= {damage * (1.0f - enemy.MagicDamageResist)} ");
                    stun.UseAbility(predictedPos);
                    await Await.Delay((int) (stun.FindCastPoint() * 1000.0 + Game.Ping));
                    return true;
                }
            }

            return false;
        }

        public override async Task ExecuteComboAsync(Unit target, CancellationToken tk = new CancellationToken())
        {
            await UseItems(tk);

            var ult = MyHero.Spellbook.SpellR;
            if (ult.CanBeCasted(Target) && ult.CanHit(Target) && !Target.IsLinkensProtected())
            {
                var damage =
                    ult.GetAbilityData(MyHero.HasItem(ClassID.CDOTA_Item_UltimateScepter) ? "damage_scepter" : "damage");
                if (Target.Health <= damage * (1.0f - Target.MagicDamageResist))
                {
                    Log.Debug(
                        $"use ult because enough damage {Target.Health} <= {damage * (1.0f - Target.MagicDamageResist)} ");
                    ult.UseAbility(Target);
                    await Await.Delay((int) (ult.FindCastPoint() * 1000.0 + Game.Ping), tk);
                }
            }

            // make him disabled
            if (DisableEnemy(tk) == DisabledState.UsedAbilityToDisable)
            {
                Log.Debug($"disabled!");
                // return;
            }
            float maxRange = 500;
            float duration;
            if (!(Target.IsHexed(out duration) || Target.IsStunned(out duration)) || duration < 1.2)
            {
                var hex = MyHero.Spellbook.SpellW;
                maxRange = Math.Max(maxRange, hex.CastRange);
                if (hex.CanBeCasted(Target))
                {
                    if (hex.CanHit(Target))
                    {
                        Log.Debug($"use hex {duration}");
                        hex.UseAbility(Target);
                        await Await.Delay((int) (ult.FindCastPoint() * 1000.0 + Game.Ping), tk);
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
                if (stun.CanBeCasted(Target))
                {
                    if (hex.CanHit(Target))
                    {
                        var speed = stun.GetAbilityData("speed");
                        var time = Target.Distance2D(MyHero) / speed * 1000.0f;

                        var predictedPos = Prediction.Prediction.PredictPosition(Target, (int) time);
                        if (MyHero.Distance2D(predictedPos) <= stun.GetCastRange())
                        {
                            Log.Debug($"use stun {duration} | {time}");
                            stun.UseAbility(predictedPos);
                            await Await.Delay((int) (stun.FindCastPoint() * 1000.0 + Game.Ping), tk);
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

            if (ult.CanBeCasted(Target) && ult.CanHit(Target) && HasNoLinkens(Target))
            {
                if (Target.IsHexed() || Target.IsStunned() || ((float)Target.Health / Target.MaximumHealth) * (1.0f + Target.MagicDamageResist) < 0.5f  )
                {
                    Log.Debug($"use ult");
                    ult.UseAbility(Target);
                    await Await.Delay((int) (ult.FindCastPoint() * 1000.0 + Game.Ping), tk);
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
                    await Await.Delay((int) (mana.FindCastPoint() * 1000.0 + Game.Ping), tk);
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