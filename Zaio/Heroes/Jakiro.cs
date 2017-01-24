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
using Zaio.Prediction;

namespace Zaio.Heroes
{
    [Hero(ClassID.CDOTA_Unit_Hero_Jakiro)]
    internal class Jakiro : ComboHero
    {
        private static readonly ILog Log = AssemblyLogs.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private static readonly string[] SupportedAbilities =
        {
            "jakiro_dual_breath",
            "jakiro_ice_path",
            "jakiro_liquid_fire",
            "jakiro_macropyre",
            "item_cyclone"
        };

        public override void OnLoad()
        {
            base.OnLoad();

            var heroMenu = new Menu("Jakiro", "zaioJakiro", false, "npc_dota_hero_jakiro", true);

            heroMenu.AddItem(new MenuItem("zaioJakiroAbilitiesText", "Supported Abilities"));
            var supportedStuff = new MenuItem("zaioJakiroAbilities", string.Empty);
            supportedStuff.SetValue(new AbilityToggler(SupportedAbilities.ToDictionary(x => x, y => true)));
            heroMenu.AddItem(supportedStuff);

            ZaioMenu.LoadHeroSettings(heroMenu);
        }

        public override async Task ExecuteComboAsync(Unit target, CancellationToken tk = new CancellationToken())
        {
            await HasNoLinkens(target, tk);

            var stun = MyHero.Spellbook.SpellW;
            var eulsModifier = target.FindModifier("modifier_eul_cyclone");
            if ((stun.CanBeCasted(target) || eulsModifier != null && stun.CanBeCasted()) && stun.CanHit(target))
            {
                var stunCastpoint = stun.FindCastPoint();
                var delay = stun.GetAbilityData("path_delay");

                if (eulsModifier != null)
                {
                    Log.Debug($"has euls {eulsModifier.RemainingTime}");
                    if (eulsModifier.RemainingTime < stunCastpoint + delay)
                    {
                        Log.Debug($"using stun on cycloned target");
                        stun.UseAbility(target.NetworkPosition);
                        await Await.Delay(GetAbilityDelay(target, stun), tk);
                    }
                }
                else
                {
                    var disabled = 0.0f;
                    if (target.IsRooted(out disabled) || target.IsStunned(out disabled))
                    {
                        var time = disabled - stunCastpoint - delay;
                        if (time >= 0)
                        {
                            Log.Debug($"using stun on disabled target {time}");
                            stun.UseAbility(target.NetworkPosition);
                            await Await.Delay(GetAbilityDelay(target, stun), tk);
                        }
                        else
                        {
                            var predictedPos = Prediction.Prediction.PredictPosition(target, (int) time * -1000);

                            Log.Debug($"using stun on disabled target {time} with predicted pos {predictedPos}");
                            stun.UseAbility(predictedPos);
                            await Await.Delay(GetAbilityDelay(target, stun), tk);
                        }
                    }
                    else
                    {
                        var euls = MyHero.GetItemById(ItemId.item_cyclone);
                        if (euls != null && euls.CanBeCasted(target))
                        {
                            if (euls.CanHit(target))
                            {
                                Log.Debug($"using euls to disable enemy before stun");
                                euls.UseAbility(target);
                                await Await.Delay(125, tk);
                                return;
                            }
                            // check if we are near the enemy
                            if (!await MoveOrBlinkToEnemy(tk, 250, euls.GetCastRange()))
                            {
                                Log.Debug($"return because of blink");
                                return;
                            }
                        }


                        var predictedPos = Prediction.Prediction.PredictPosition(target,
                            (int) ((stunCastpoint + delay) * 1000));

                        Log.Debug($"using stun on target with predicted pos {predictedPos}");
                        stun.UseAbility(predictedPos);
                        await Await.Delay(GetAbilityDelay(target, stun), tk);
                    }
                }
            }

            await UseItems(tk);

            // make him disabled
            if (await DisableEnemy(tk) == DisabledState.UsedAbilityToDisable)
            {
                Log.Debug($"disabled!");
                // return;
            }

            var ult = MyHero.Spellbook.SpellR;
            if (ult.CanBeCasted(target) && ult.CanHit(target))
            {
                if (target.IsStunned() || target.IsRooted())
                {
                    Log.Debug($"using ult because target is stunned");
                    ult.UseAbility(target.NetworkPosition);
                    await Await.Delay(GetAbilityDelay(target, ult) + 250, tk);
                }
                else
                {
                    var predictedPos = Prediction.Prediction.PredictPosition(target,
                        (int) (ult.FindCastPoint() * 1000.0));
                    var radius = ult.GetAbilityData("path_radius");

                    var dir = predictedPos - MyHero.NetworkPosition;
                    dir.Normalize();
                    dir *=
                        ult.GetAbilityData(MyHero.HasItem(ClassID.CDOTA_Item_UltimateScepter)
                            ? "cast_range_scepter"
                            : "cast_range");

                    var rec = new Geometry.Polygon.Rectangle(MyHero.NetworkPosition, MyHero.NetworkPosition + dir,
                        radius);
                    var hasMoreEnemies =
                        ObjectManager.GetEntitiesParallel<Hero>()
                                     .Any(
                                         x =>
                                             x.IsValid && x != target && x.IsAlive && !x.IsMagicImmune() &&
                                             x.Team != MyHero.Team && rec.IsInside(x.NetworkPosition));
                    if (hasMoreEnemies)
                    {
                        Log.Debug($"using ult because more enemies");
                        ult.UseAbility(predictedPos);
                        await Await.Delay(GetAbilityDelay(target, ult) + 250, tk);
                    }
                }
            }

            var dual = MyHero.Spellbook.SpellQ;
            if (dual.CanBeCasted(target) && dual.CanHit(target))
            {
                Log.Debug($"using Q");
                dual.UseAbility(target.NetworkPosition);
                await Await.Delay(GetAbilityDelay(target, dual), tk);
            }

            var orb = MyHero.Spellbook.SpellE;
            if (orb.CanBeCasted(target) && orb.CanHit(target))
            {
                Log.Debug($"using orb");
                orb.UseAbility(target);
                await Await.Delay(GetAbilityDelay(target, orb), tk);
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
                MyHero.Attack(target);
                await Await.Delay(125, tk);
            }
        }
    }
}