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

namespace Zaio.Heroes
{
    [Hero(ClassID.CDOTA_Unit_Hero_MonkeyKing)]
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

            _minimumEnemyUltCount =
                new MenuItem("zaioMonkeyKingMinEnemyCount", "Minimum Enemies for Ult").SetValue(new Slider(1, 0, 4));
            _minimumEnemyUltCount.Tooltip = "Minimum enemies besides your target to use ult.";
            heroMenu.AddItem(_minimumEnemyUltCount);

            ZaioMenu.LoadHeroSettings(heroMenu);
        }

        public override void OnClose()
        {
            Player.OnExecuteOrder -= Player_OnExecuteOrder;
            base.OnClose();
        }

        private void Player_OnExecuteOrder(Player sender, ExecuteOrderEventArgs args)
        {
            if (args.Order == Order.AbilityLocation && args.Ability.ID == (uint) AbilityId.monkey_king_primal_spring)
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

            if (
                MyHero.HasModifiers(
                    new[] {"modifier_monkey_king_tree_dance_hidden", "modifier_monkey_king_tree_dance_activity"}, false))
            {
                // dont interrupt the comboing
                var e = MyHero.GetAbilityById(AbilityId.monkey_king_primal_spring);
                if (!e.IsChanneling)
                {
                    var damage = e.GetAbilityData("impact_damage");

                    var enemy =
                        ObjectManager.GetEntitiesParallel<Hero>()
                                     .FirstOrDefault(
                                         x =>
                                             x.IsAlive && x.Team != MyHero.Team && !x.IsIllusion && e.CanBeCasted(x) &&
                                             e.CanHit(x) && !x.IsMagicImmune() &&
                                             x.Health < damage * (1 - x.MagicResistance()) && !x.CantBeAttacked() &&
                                             !x.CantBeKilled());
                    if (enemy != null)
                    {
                        var prop = GetNeededEProp(e, enemy);
                        var predictedPos = Prediction.Prediction.PredictPosition(enemy,
                            (int) (125 + prop * e.ChannelTime() * 1000.0f));
                        e.UseAbility(predictedPos);
                        Log.Debug(
                            $"Using E to killsteal on {enemy.Name} with {prop} time {(int) (125 + Game.Ping + prop * e.ChannelTime() * 1000)}");
                        await Await.Delay((int) (125 + Game.Ping + prop * e.ChannelTime() * 1000));
                        var eEarly = MyHero.GetAbilityById(AbilityId.monkey_king_primal_spring_early);
                        if (eEarly.CanBeCasted())
                        {
                            eEarly.UseAbility();
                            Log.Debug($"jumping early to killsteal!");
                            await Await.Delay(125);
                        }
                        return true;
                    }
                }
            }

            var q = MyHero.GetAbilityById(AbilityId.monkey_king_boundless_strike);
            if (q.CanBeCasted())
            {
                var enemy =
                    ObjectManager.GetEntitiesParallel<Hero>()
                                 .FirstOrDefault(
                                     x =>
                                         x.IsAlive && x.Team != MyHero.Team && !x.IsIllusion && q.CanBeCasted(x) &&
                                         q.CanHit(x) &&
                                         x.Health < GetQDamage(q, x) && !x.CantBeAttacked() &&
                                         !x.CantBeKilled());
                if (enemy != null)
                {
                    var castPoint = q.FindCastPoint() * 1000;
                    var predictedPos = Prediction.Prediction.PredictPosition(enemy, (int) castPoint);
                    q.UseAbility(predictedPos);
                    await Await.Delay((int) (castPoint + Game.Ping));
                    return true;
                }
            }


            return false;
        }

        public override async Task ExecuteComboAsync(Unit target, CancellationToken tk = new CancellationToken())
        {
            if (
                MyHero.HasModifiers(
                    new[] {"modifier_monkey_king_tree_dance_hidden", "modifier_monkey_king_tree_dance_activity"}, false))
            {
                var e = MyHero.GetAbilityById(AbilityId.monkey_king_primal_spring);
                if (e.IsChanneling)
                {
                    if (_lastEPosition != Vector3.Zero)
                    {
                        var radius = e.GetAbilityData("impact_radius");
                        var prediction = Prediction.Prediction.PredictPosition(Target,
                            (int) (1.5f * Target.MovementSpeed));
                        var dist = prediction.Distance2D(_lastEPosition);
                        Log.Debug($"damage known pos: {GetEDamage(e, Target)} with dist {dist}");
                        if (dist >= radius)
                        {
                            var eEarly = MyHero.GetAbilityById(AbilityId.monkey_king_primal_spring_early);
                            if (eEarly.CanBeCasted())
                            {
                                _lastEPosition = Vector3.Zero;
                                eEarly.UseAbility();
                                Log.Debug($"jumping early because enemy is escaping our target location!");
                                await Await.Delay(125, tk);
                            }
                        }
                    }
                    else
                    {
                        Log.Debug($"damage: {GetEDamage(e, Target)}");
                    }
                }
                else if (e.CanBeCasted(Target) && e.CanHit(Target))
                {
                    var prop = GetNeededEProp(e, Target);
                    var castPoint = e.FindCastPoint() * 1000;
                    var predictedPos = Prediction.Prediction.PredictPosition(Target,
                        (int) (castPoint + prop * e.ChannelTime() * 1000.0f));
                    e.UseAbility(predictedPos);
                    _lastEPosition = predictedPos;
                    Log.Debug($"Using E with prop {prop} to {predictedPos}");
                    await Await.Delay((int) (castPoint + Game.Ping), tk);
                }

                return;
            }

            await HasNoLinkens(Target, tk);
            await UseItems(tk);

            // make him disabled
            if (await DisableEnemy(tk) == DisabledState.UsedAbilityToDisable)
            {
                Log.Debug($"disabled!");
                // return;
            }

            var q = MyHero.GetAbilityById(AbilityId.monkey_king_boundless_strike);
            if (q.CanBeCasted(Target) && q.CanHit(Target))
            {
                if (IsBuffed || GetQDamage(q, Target) > Target.Health)
                {
                    var castPoint = q.FindCastPoint() * 1000;
                    var predictedPos = Prediction.Prediction.PredictPosition(Target, (int) castPoint);
                    q.UseAbility(predictedPos);
                    await Await.Delay((int) (castPoint + Game.Ping), tk);
                }
            }

            var ult = MyHero.GetAbilityById(AbilityId.monkey_king_wukongs_command);
            if (ult.CanBeCasted() && ult.CanHit(Target))
            {
                var radius = ult.GetAbilityData("leadership_radius");
                var enemiesNearCount =
                    ObjectManager.GetEntitiesParallel<Hero>()
                                 .Count(
                                     x =>
                                         x.IsValid && x != Target && x.IsAlive && x.Team != MyHero.Team &&
                                         !x.IsIllusion && x.Distance2D(Target) <= radius);
                if (enemiesNearCount >= EnemyCountForUlt)
                {
                    Log.Debug($"using ult since more enemies here");
                    ult.UseAbility(Target.NetworkPosition);
                    await Await.Delay((int) (ult.FindCastPoint() * 1000 + Game.Ping), tk);
                }
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
            }
            else
            {
                MyHero.Attack(Target);
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
            return damage * crit * (1 - target.DamageResist);
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