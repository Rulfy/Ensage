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

namespace Zaio.Heroes
{
    [Hero(ClassID.CDOTA_Unit_Hero_Leshrac)]
    internal class Leshrak : ComboHero
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

        public override void OnLoad()
        {
            base.OnLoad();

            var heroMenu = new Menu("Leshrak", "zaioLeshrak", false, "npc_dota_hero_leshrak", true);

            heroMenu.AddItem(new MenuItem("zaioLeshrakAbilitiesText", "Supported Abilities"));
            var supportedStuff = new MenuItem("zaioLeshrakAbilities", string.Empty);
            supportedStuff.SetValue(new AbilityToggler(SupportedAbilities.ToDictionary(x => x, y => true)));
            heroMenu.AddItem(supportedStuff);

            ZaioMenu.LoadHeroSettings(heroMenu);
        }

        public override async Task ExecuteComboAsync(Unit target, CancellationToken tk = new CancellationToken())
        {
            HasNoLinkens(Target);

            var stun = MyHero.Spellbook.SpellQ;
            var eulsModifier = Target.FindModifier("modifier_eul_cyclone");
            if ((stun.CanBeCasted(Target) || eulsModifier != null && stun.CanBeCasted()) && stun.CanHit(Target))
            {
                var stunCastpoint = stun.FindCastPoint();
                var delay = stun.GetAbilityData("delay");

                if (eulsModifier != null)
                {
                    Log.Debug($"has euls {eulsModifier.RemainingTime}");
                    if (eulsModifier.RemainingTime < stunCastpoint + delay)
                    {
                        Log.Debug($"using stun on cycloned target");
                        stun.UseAbility(Target.NetworkPosition);
                        await Await.Delay((int) (stunCastpoint * 1200.0 + Game.Ping), tk);
                    }
                }
                else
                {
                    var disabled = 0.0f;
                    if (Target.IsRooted(out disabled) || Target.IsStunned(out disabled))
                    {
                        var time = disabled - stunCastpoint - delay;
                        if (time >= 0)
                        {
                            Log.Debug($"using stun on disabled target {time}");
                            stun.UseAbility(Target.NetworkPosition);
                            await Await.Delay((int) (stunCastpoint * 1200.0 + Game.Ping), tk);
                        }
                        else
                        {
                            var predictedPos = Prediction.Prediction.PredictPosition(Target, (int) time * -1000);

                            Log.Debug($"using stun on disabled target {time} with predicted pos {predictedPos}");
                            stun.UseAbility(predictedPos);
                            await Await.Delay((int) (stunCastpoint * 1200.0 + Game.Ping), tk);
                        }
                    }
                    else
                    {
                        var euls = MyHero.GetItemById(ItemId.item_cyclone);
                        if (euls != null && euls.CanBeCasted(Target))
                        {
                            if (euls.CanHit(Target))
                            {
                                Log.Debug($"using euls to disable enemy before stun");
                                euls.UseAbility(Target);
                                await Await.Delay(125, tk);
                                return;
                            }
                            // check if we are near the enemy
                            if (!await MoveOrBlinkToEnemy(tk, 250, euls.GetCastRange()))
                            {
                                Log.Debug($"return because of blink and euls ready");
                                return;
                            }
                            Log.Debug($"ELSE");
                        }


                        var predictedPos = Prediction.Prediction.PredictPosition(Target,
                            (int) ((stunCastpoint + delay) * 1000), true);
                        if (predictedPos != Vector3.Zero)
                        {
                            Log.Debug($"using stun on target with predicted pos {predictedPos}");
                            stun.UseAbility(predictedPos);
                            await Await.Delay((int) (stunCastpoint * 1200.0 + Game.Ping), tk);
                        }
                        else
                        {
                            Log.Debug($"Not using stun due to enemy turning!");
                        }
                    }
                }
            }

            await UseItems(tk);

            // make him disabled
            if (DisableEnemy(tk) == DisabledState.UsedAbilityToDisable)
            {
                Log.Debug($"disabled!");
                // return;
            }

            var ult = MyHero.Spellbook.SpellR;
            if (!ult.IsToggled && ult.CanBeCasted(Target) && ult.CanHit(Target))
            {
                Log.Debug($"using ult");
                ult.ToggleAbility();
                await Await.Delay((int) (ult.FindCastPoint() * 1000.0 + Game.Ping), tk);
            }

            var edict = MyHero.Spellbook.SpellW;
            if (edict.CanBeCasted(Target) && edict.CanHit(Target))
            {
                Log.Debug($"using edict");
                edict.UseAbility();
                await Await.Delay((int) (edict.FindCastPoint() * 1000.0 + Game.Ping), tk);
            }

            var lightning = MyHero.Spellbook.SpellE;
            if (lightning.CanBeCasted(Target) && lightning.CanHit(Target))
            {
                Log.Debug($"using lightning");
                lightning.UseAbility(Target);
                await Await.Delay((int) (lightning.FindCastPoint() * 1000.0 + Game.Ping), tk);
            }

            // check if we are near the enemy
            if (!await MoveOrBlinkToEnemy(tk))
            {
                Log.Debug($"return because of blink");
                return;
            }

            if (ZaioMenu.ShouldUseOrbwalker)
            {
                Orbwalk();
                Log.Debug($"orbwalking");
            }
            else
            {
                MyHero.Attack(Target);
                await Await.Delay(125, tk);
            }
        }
    }
}