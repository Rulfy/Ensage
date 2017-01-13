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
            HasNoLinkens(Target);

            var stun = MyHero.Spellbook.SpellW;
            var eulsModifier = Target.FindModifier("modifier_eul_cyclone");
            if ((stun.CanBeCasted(Target) || eulsModifier != null && stun.CanBeCasted()) && stun.CanHit(target))
            {
                var stunCastpoint = stun.FindCastPoint();
                var delay = stun.GetAbilityData("path_delay");

                if (eulsModifier != null)
                {
                    Log.Debug($"has euls {eulsModifier.RemainingTime}");
                    if (eulsModifier.RemainingTime < stunCastpoint + delay)
                    {
                        Log.Debug($"using stun on cycloned target");
                        stun.UseAbility(Target.NetworkPosition);
                        await Await.Delay((int) (stunCastpoint * 1000.0 + Game.Ping), tk);
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
                            await Await.Delay((int) (stunCastpoint * 1000.0 + Game.Ping), tk);
                        }
                        else
                        {
                            var predictedPos = Prediction.Prediction.PredictPosition(Target, (int) time * -1000);

                            Log.Debug($"using stun on disabled target {time} with predicted pos {predictedPos}");
                            stun.UseAbility(predictedPos);
                            await Await.Delay((int) (stunCastpoint * 1000.0 + Game.Ping), tk);
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
                                Log.Debug($"return because of blink");
                                return;
                            }
                        }


                        var predictedPos = Prediction.Prediction.PredictPosition(Target,
                            (int) ((stunCastpoint + delay) * 1000));

                        Log.Debug($"using stun on target with predicted pos {predictedPos}");
                        stun.UseAbility(predictedPos);
                        await Await.Delay((int) (stunCastpoint * 1000.0 + Game.Ping), tk);
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
            if (ult.CanBeCasted(Target) && ult.CanHit(Target))
            {
                if (Target.IsStunned() || Target.IsRooted())
                {
                    Log.Debug($"using ult because target is stunned");
                    ult.UseAbility(Target.NetworkPosition);
                    await Await.Delay((int) (ult.FindCastPoint() * 1500.0 + Game.Ping), tk);
                }
                else
                {
                    var predictedPos = Prediction.Prediction.PredictPosition(Target,
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
                                             x.IsValid && x != Target && x.IsAlive && !x.IsMagicImmune() &&
                                             x.Team != MyHero.Team && rec.IsInside(x.NetworkPosition));
                    if (hasMoreEnemies)
                    {
                        Log.Debug($"using ult because more enemies");
                        ult.UseAbility(predictedPos);
                        await Await.Delay((int) (ult.FindCastPoint() * 1500.0 + Game.Ping), tk);
                    }
                }
            }

            var dual = MyHero.Spellbook.SpellQ;
            if (dual.CanBeCasted(Target) && dual.CanHit(Target))
            {
                Log.Debug($"using Q");
                dual.UseAbility(Target.NetworkPosition);
                await Await.Delay((int) (dual.FindCastPoint() * 1000.0 + Game.Ping), tk);
            }

            var orb = MyHero.Spellbook.SpellE;
            if (orb.CanBeCasted(Target) && orb.CanHit(Target))
            {
                Log.Debug($"using orb");
                orb.UseAbility(Target);
                await Await.Delay((int) (orb.FindCastPoint() * 1000.0 + Game.Ping), tk);
            }

            // check if we are near the enemy
            if (!await MoveOrBlinkToEnemy(tk))
            {
                Log.Debug($"return because of blink");
                return;
            }

            if (ZaioMenu.ShouldUseOrbwalker)
            {
                Orbwalk(450);
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