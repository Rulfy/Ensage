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
using SharpDX;
using Zaio.Helpers;
using Zaio.Interfaces;
using AbilityId = Ensage.AbilityId;


namespace Zaio.Heroes
{
    [Hero(ClassId.CDOTA_Unit_Hero_MonkeyKing)]
    internal class MonkeyKing : ComboHero
    {
        private static readonly ILog Log = AssemblyLogs.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private static readonly string[] SupportedAbilities =
        {
            "monkey_king_boundless_strike",
            "monkey_king_primal_spring",
            "monkey_king_wukongs_command"
        };

        private static readonly string[] KillstealAbilities =
        {
            "monkey_king_boundless_strike",
            "monkey_king_primal_spring"
        };

        private Vector3 _lastEPosition = Vector3.Zero;

        private MenuItem _minimumEnemyUltCount;
        private Ability _springAbility;
        private Ability _springEarlyAbility;
        private Ability _stunAbility;
        private Ability _ultAbility;

        private bool IsBuffed => MyHero.HasModifier("modifier_monkey_king_quadruple_tap_bonuses");
        private int EnemyCountForUlt => _minimumEnemyUltCount.GetValue<Slider>().Value;

        public override void OnLoad()
        {
            base.OnLoad();

            Player.OnExecuteOrder += Player_OnExecuteOrder;

            var heroMenu = new Menu("MonkeyKing", "zaioMonkeyKing", false, "npc_dota_hero_monkey_king", true);

            heroMenu.AddItem(new MenuItem("zaioMonkeyKingAbilitiesText", "Supported Abilities"));
            var supportedStuff = new MenuItem("zaioMonkeyKingAbilities", string.Empty);
            supportedStuff.SetValue(new AbilityToggler(SupportedAbilities.ToDictionary(x => x, y => true)));
            heroMenu.AddItem(supportedStuff);

            heroMenu.AddItem(new MenuItem("zaioMonkeyKingKillstealAbilitiesText", "Supported Killsteal Abilities"));
            var supportedKillsteal = new MenuItem("zaioMonkeyKingKillstealAbilities", string.Empty);
            supportedKillsteal.SetValue(new AbilityToggler(KillstealAbilities.ToDictionary(x => x, y => true)));
            heroMenu.AddItem(supportedKillsteal);

            OnLoadMenuItems(supportedStuff, supportedKillsteal);

            _minimumEnemyUltCount =
                new MenuItem("zaioMonkeyKingMinEnemyCount", "Minimum Enemies for Ult").SetValue(new Slider(1, 0, 4));
            _minimumEnemyUltCount.Tooltip = "Minimum enemies besides your target to use ult.";
            heroMenu.AddItem(_minimumEnemyUltCount);

            _stunAbility = MyHero.GetAbilityById(AbilityId.monkey_king_boundless_strike);
            _springAbility = MyHero.GetAbilityById(AbilityId.monkey_king_primal_spring);
            _springEarlyAbility = MyHero.GetAbilityById(AbilityId.monkey_king_primal_spring_early);
            _ultAbility = MyHero.GetAbilityById(AbilityId.monkey_king_wukongs_command);

            ZaioMenu.LoadHeroSettings(heroMenu);
        }

        public override void OnClose()
        {
            Player.OnExecuteOrder -= Player_OnExecuteOrder;
            base.OnClose();
        }

        private void Player_OnExecuteOrder(Player sender, ExecuteOrderEventArgs args)
        {
            if (args.OrderId == OrderId.AbilityLocation && args.Ability.Id == AbilityId.monkey_king_primal_spring)
            {
                if (Target != null)
                {
                    var radius = args.Ability.GetAbilityData("impact_radius");
                    if (Target.Distance2D(args.Target) <= radius)
                    {
                        _lastEPosition = args.TargetPosition;
                    }
                }
            }
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

            if (Target != null)
            {
                return false;
            }

            if ( _springAbility.IsKillstealAbilityEnabled() && 
                MyHero.HasModifiers(
                    new[] {"modifier_monkey_king_tree_dance_hidden", "modifier_monkey_king_tree_dance_activity"}, false))
            {
                // dont interrupt the comboing
                if (!_springAbility.IsChanneling)
                {
                    var damage = _springAbility.GetAbilityData("impact_damage");

                    var enemy =
                        ObjectManager.GetEntitiesParallel<Hero>()
                                     .FirstOrDefault(
                                         x =>
                                             x.IsAlive && x.Team != MyHero.Team && !x.IsIllusion &&
                                             _springAbility.CanBeCasted(x) &&
                                             _springAbility.CanHit(x) && !x.IsMagicImmune() &&
                                             x.Health < damage * (1 - x.MagicResistance()) && !x.CantBeAttacked() &&
                                             !x.CantBeKilled());
                    if (enemy != null)
                    {
                        var prop = GetNeededEProp(_springAbility, enemy);
                        var predictedPos = Prediction.Prediction.PredictPosition(enemy,
                            (int) (125 + prop * _springAbility.ChannelTime() * 1000.0f));
                        _springAbility.UseAbility(predictedPos);
                        Log.Debug(
                            $"Using E to killsteal on {enemy.Name} with {prop} time {(int) (125 + Game.Ping + prop * _springAbility.ChannelTime() * 1000)}");
                        await Await.Delay((int) (125 + Game.Ping + prop * _springAbility.ChannelTime() * 1000));
                        if (_springEarlyAbility.CanBeCasted())
                        {
                            _springEarlyAbility.UseAbility();
                            Log.Debug($"jumping early to killsteal!");
                            await Await.Delay(125);
                        }
                        return true;
                    }
                }
            }

            if (_stunAbility.IsKillstealAbilityEnabled() && _stunAbility.CanBeCasted())
            {
                var enemy =
                    ObjectManager.GetEntitiesParallel<Hero>()
                                 .FirstOrDefault(
                                     x =>
                                         x.IsAlive && x.Team != MyHero.Team && !x.IsIllusion &&
                                         _stunAbility.CanBeCasted(x) &&
                                         _stunAbility.CanHit(x) &&
                                         x.Health < GetQDamage(_stunAbility, x) && !x.CantBeAttacked() &&
                                         !x.CantBeKilled());
                if (enemy != null)
                {
                    var castPoint = _stunAbility.FindCastPoint() * 1000;
                    var predictedPos = Prediction.Prediction.PredictPosition(enemy, (int) castPoint);
                    _stunAbility.UseAbility(predictedPos);
                    await Await.Delay(GetAbilityDelay(predictedPos, _stunAbility));
                    return true;
                }
            }


            return false;
        }

        public override async Task ExecuteComboAsync(Unit target, CancellationToken tk = new CancellationToken())
        {
            if ( _springAbility.IsAbilityEnabled() &&
                MyHero.HasModifiers(
                    new[] {"modifier_monkey_king_tree_dance_hidden", "modifier_monkey_king_tree_dance_activity"}, false))
            {
                if (MyHero.IsSilenced())
                {
                    return;
                }

                if (_springAbility.IsChanneling)
                {
                    if (_lastEPosition != Vector3.Zero)
                    {
                        var radius = _springAbility.GetAbilityData("impact_radius");
                        var prediction = Prediction.Prediction.PredictPosition(target,
                            (int) (1.5f * target.MovementSpeed));
                        var dist = prediction.Distance2D(_lastEPosition);
                        Log.Debug($"damage known pos: {GetEDamage(_springAbility, target)} with dist {dist}");
                        if (dist >= radius)
                        {
                            if (_springEarlyAbility.CanBeCasted())
                            {
                                _lastEPosition = Vector3.Zero;
                                _springEarlyAbility.UseAbility();
                                Log.Debug($"jumping early because enemy is escaping our target location!");
                                await Await.Delay(125, tk);
                            }
                        }
                    }
                    else
                    {
                        Log.Debug($"damage: {GetEDamage(_springAbility, target)}");
                    }
                }
                else if (_springAbility.CanBeCasted(target) && _springAbility.CanHit(target))
                {
                    var prop = GetNeededEProp(_springAbility, target);
                    var castPoint = _springAbility.FindCastPoint() * 1000;
                    var predictedPos = Prediction.Prediction.PredictPosition(target,
                        (int) (castPoint + prop * _springAbility.ChannelTime() * 1000.0f));
                    _springAbility.UseAbility(predictedPos);
                    _lastEPosition = predictedPos;
                    Log.Debug($"Using E with prop {prop} to {predictedPos}");
                    await Await.Delay(GetAbilityDelay(predictedPos, _springAbility), tk);
                }

                return;
            }

            await HasNoLinkens(target, tk);
            await UseItems(target, tk);

            // make him disabled
            if (await DisableEnemy(target, tk) == DisabledState.UsedAbilityToDisable)
            {
                Log.Debug($"disabled!");
                // return;
            }

            if (!MyHero.IsSilenced())
            {
                if (_stunAbility.IsAbilityEnabled() && _stunAbility.CanBeCasted(target) && _stunAbility.CanHit(target))
                {
                    if (IsBuffed || GetQDamage(_stunAbility, target) > target.Health)
                    {
                        var castPoint = _stunAbility.FindCastPoint() * 1000;
                        var predictedPos = Prediction.Prediction.PredictPosition(target, (int) castPoint);
                        _stunAbility.UseAbility(predictedPos);
                        await Await.Delay(GetAbilityDelay(predictedPos, _stunAbility), tk);
                    }
                }

                if (_ultAbility.IsAbilityEnabled() && _ultAbility.CanBeCasted() && _ultAbility.CanHit(target))
                {
                    var radius = _ultAbility.GetAbilityData("leadership_radius");
                    var enemiesNearCount =
                        ObjectManager.GetEntitiesParallel<Hero>()
                                     .Count(
                                         x =>
                                             x.IsValid && x != target && x.IsAlive && x.Team != MyHero.Team &&
                                             !x.IsIllusion && x.Distance2D(target) <= radius);
                    if (enemiesNearCount >= EnemyCountForUlt)
                    {
                        Log.Debug($"using ult since more enemies here");
                        _ultAbility.UseAbility(target.NetworkPosition);
                        await Await.Delay(GetAbilityDelay(target.NetworkPosition, _ultAbility), tk);
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
            else
            {
                MyHero.Attack(target);
                await Await.Delay(125, tk);
            }
        }

        private float GetQDamage(Ability q, Unit target)
        {
            var damage = (float) (MyHero.MinimumDamage + MyHero.BonusDamage);
            if (IsBuffed)
            {
                damage += MyHero.GetAbilityById(AbilityId.monkey_king_jingu_mastery).GetAbilityData("bonus_damage");
            }

            float crit;
            if (MyHero.GetAbilityById(AbilityId.special_bonus_unique_monkey_king).Level > 0)
            {
                crit = (q.GetAbilityData("strike_crit_mult") + 100.0f) / 100.0f;
            }
            else
            {
                crit = q.GetAbilityData("strike_crit_mult") / 100.0f;
            }
            return damage * crit * (1 - target.PhysicalResistance());
        }

        private float GetEDamage(Ability e, Unit target)
        {
            if (!e.IsChanneling)
            {
                return 0.0f;
            }

            var damage = e.GetAbilityData("impact_damage");
            var prop = e.ChannelTime / e.ChannelTime();
            prop = Math.Min(prop, 1.0f);
            prop = Math.Max(0.0f, prop);
            return damage * prop;
        }

        private float GetNeededEProp(Ability e, Unit target)
        {
            var damage = e.GetAbilityData("impact_damage");
            var time = e.ChannelTime();
            var health = (target.Health + target.HealthRegeneration * time) * (1.0f + target.MagicResistance()) * 1.1f;
            // todo: calc from monkey ms/travel speed?
            Log.Debug($"health {health} | {target.HealthRegeneration * time}");
            return Math.Min(1.0f, health / damage);
        }
    }
}