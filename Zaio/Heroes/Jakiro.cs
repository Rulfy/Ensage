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

        private Ability _dualAbility;
        private Ability _orbAbility;
        private Ability _stunAbility;
        private Ability _ultAbility;

        public override void OnLoad()
        {
            base.OnLoad();

            var heroMenu = new Menu("Jakiro", "zaioJakiro", false, "npc_dota_hero_jakiro", true);

            heroMenu.AddItem(new MenuItem("zaioJakiroAbilitiesText", "Supported Abilities"));
            var supportedStuff = new MenuItem("zaioJakiroAbilities", string.Empty);
            supportedStuff.SetValue(new AbilityToggler(SupportedAbilities.ToDictionary(x => x, y => true)));
            heroMenu.AddItem(supportedStuff);

            OnLoadMenuItems(supportedStuff);

            ZaioMenu.LoadHeroSettings(heroMenu);

            _ultAbility = MyHero.GetAbilityById(AbilityId.jakiro_macropyre);
            _stunAbility = MyHero.GetAbilityById(AbilityId.jakiro_ice_path);
            _dualAbility = MyHero.GetAbilityById(AbilityId.jakiro_dual_breath);
            _orbAbility = MyHero.GetAbilityById(AbilityId.jakiro_liquid_fire);
        }

        public override async Task ExecuteComboAsync(Unit target, CancellationToken tk = new CancellationToken())
        {
            await HasNoLinkens(target, tk);

            if (!MyHero.IsSilenced())
            {
                var eulsModifier = target.FindModifier("modifier_eul_cyclone");
                if (_stunAbility.IsAbilityEnabled() &&(_stunAbility.CanBeCasted(target) || eulsModifier != null && _stunAbility.CanBeCasted()) &&
                    _stunAbility.CanHit(target))
                {
                    var stunCastpoint = _stunAbility.FindCastPoint();
                    var delay = _stunAbility.GetAbilityData("path_delay");

                    if (eulsModifier != null)
                    {
                        Log.Debug($"has euls {eulsModifier.RemainingTime}");
                        if (eulsModifier.RemainingTime < stunCastpoint + delay)
                        {
                            Log.Debug($"using stun on cycloned target");
                            _stunAbility.UseAbility(target.NetworkPosition);
                            await Await.Delay(GetAbilityDelay(target, _stunAbility), tk);
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
                                if (!await MoveOrBlinkToEnemy(target, tk, minimumRange: 250, maximumRange: euls.GetCastRange()))
                                {
                                    Log.Debug($"return because of blink");
                                    return;
                                }
                            }


                            var predictedPos = Prediction.Prediction.PredictPosition(target,
                                (int) ((stunCastpoint + delay) * 1000));

                            Log.Debug($"using stun on target with predicted pos {predictedPos}");
                            _stunAbility.UseAbility(predictedPos);
                            await Await.Delay(GetAbilityDelay(target, _stunAbility), tk);
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
                if (_ultAbility.IsAbilityEnabled() && _ultAbility.CanBeCasted(target) && _ultAbility.CanHit(target))
                {
                    if (target.IsStunned() || target.IsRooted())
                    {
                        Log.Debug($"using ult because target is stunned");
                        _ultAbility.UseAbility(target.NetworkPosition);
                        await Await.Delay(GetAbilityDelay(target, _ultAbility) + 250, tk);
                    }
                    else
                    {
                        var predictedPos = Prediction.Prediction.PredictPosition(target,
                            (int) (_ultAbility.FindCastPoint() * 1000.0));
                        var radius = _ultAbility.GetAbilityData("path_radius");

                        var dir = predictedPos - MyHero.NetworkPosition;
                        dir.Normalize();
                        dir *=
                            _ultAbility.GetAbilityData(MyHero.HasItem(ClassID.CDOTA_Item_UltimateScepter)
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
                            _ultAbility.UseAbility(predictedPos);
                            await Await.Delay(GetAbilityDelay(target, _ultAbility) + 250, tk);
                        }
                    }
                }

                if (_dualAbility.IsAbilityEnabled() && _dualAbility.CanBeCasted(target) && _dualAbility.CanHit(target))
                {
                    Log.Debug($"using Q");
                    _dualAbility.UseAbility(target.NetworkPosition);
                    await Await.Delay(GetAbilityDelay(target, _dualAbility), tk);
                }

                if (_orbAbility.IsAbilityEnabled() && _orbAbility.CanBeCasted(target) && _orbAbility.CanHit(target))
                {
                    Log.Debug($"using orb");
                    _orbAbility.UseAbility(target);
                    await Await.Delay(GetAbilityDelay(target, _orbAbility), tk);
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