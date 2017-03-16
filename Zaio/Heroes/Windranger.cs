using System;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Ensage;
using Ensage.Common;
using Ensage.Common.Enums;
using Ensage.Common.Extensions;
using Ensage.Common.Extensions.SharpDX;
using Ensage.Common.Menu;
using Ensage.Common.Threading;
using log4net;
using PlaySharp.Toolkit.Logging;
using SharpDX;
using Zaio.Helpers;
using Zaio.Interfaces;
using Zaio.Prediction;
using AbilityId = Ensage.Common.Enums.AbilityId;

namespace Zaio.Heroes
{
    [Hero(ClassID.CDOTA_Unit_Hero_Windrunner)]
    internal class Windranger : ComboHero
    {
        private static readonly ILog Log = AssemblyLogs.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private static readonly string[] SupportedAbilities =
        {
            "windrunner_shackleshot",
            "windrunner_powershot",
            "windrunner_focusfire",
            "item_branches"
        };

        private static readonly string[] KillstealAbilities =
        {
            "windrunner_powershot"
        };

        private Ability _shackleAbility;
        private Ability _powerShotAbility;
        private Ability _ultAbility;

        private Unit _ultTarget;
        private float _attackSpeed;

        private MenuItem _branchShackle;
        private MenuItem _orbwalkWhileUlt;
        private bool ShouldUseBranchShackle => _branchShackle.GetValue<bool>();
        private bool ShouldUseOrbwalkWhileUlt => _orbwalkWhileUlt.GetValue<bool>();

        public override void OnLoad()
        {
            base.OnLoad();

            var heroMenu = new Menu("Windrunner", "zaioWindrunner", false, "npc_dota_hero_windrunner", true);

            heroMenu.AddItem(new MenuItem("zaioWindrunnerAbilitiesText", "Supported Abilities"));
            var supportedStuff = new MenuItem("zaioWindrunnerAbilities", string.Empty);
            supportedStuff.SetValue(new AbilityToggler(SupportedAbilities.ToDictionary(x => x, y => true)));
            heroMenu.AddItem(supportedStuff);

            heroMenu.AddItem(new MenuItem("zaioWindrunnerKillstealAbilitiesText", "Supported Killsteal Abilities"));
            var supportedKillsteal = new MenuItem("zaioWindrunnerKillstealAbilities", string.Empty);
            supportedKillsteal.SetValue(new AbilityToggler(KillstealAbilities.ToDictionary(x => x, y => true)));
            heroMenu.AddItem(supportedKillsteal);

            OnLoadMenuItems(supportedStuff, supportedKillsteal);

            _branchShackle = new MenuItem("zaioBranchShackle", "Use Shackle -> Blink -> Branch").SetValue(true);
            _branchShackle.Tooltip = "Will use the shackle -> blink -> branch trick if suitable.";
            heroMenu.AddItem(_branchShackle);

            _orbwalkWhileUlt = new MenuItem("zaioOrbwalkWhileUlt", "Enable Ult-Orbwalking").SetValue(false);
            _orbwalkWhileUlt.Tooltip = "Enables orbwalking while being under the effect of focus fire.";
            heroMenu.AddItem(_orbwalkWhileUlt);

            ZaioMenu.LoadHeroSettings(heroMenu);

            _shackleAbility = MyHero.GetAbilityById(AbilityId.windrunner_shackleshot);
            _powerShotAbility = MyHero.GetAbilityById(AbilityId.windrunner_powershot);
            _ultAbility = MyHero.GetAbilityById(AbilityId.windrunner_focusfire);

            NewTargetAcquired += OnNewTargetAcquired;
            Player.OnExecuteOrder += Player_OnExecuteOrder;
        }

        private void Player_OnExecuteOrder(Player sender, ExecuteOrderEventArgs args)
        {
            if (args.Order == Order.AbilityTarget && args.Ability.GetAbilityId() == AbilityId.windrunner_focusfire && args.Entities.Contains(MyHero))
            {
                _ultTarget = args.Target as Unit;
                if(!MyHero.HasModifier("modifier_windrunner_focusfire"))
                    _attackSpeed = UnitDatabase.GetAttackSpeed(MyHero);
            }
        }

        public override void OnClose()
        {
            // ReSharper disable once DelegateSubtraction
            NewTargetAcquired -= OnNewTargetAcquired;
            Player.OnExecuteOrder -= Player_OnExecuteOrder;

            base.OnClose();
        }

        private void OnNewTargetAcquired(object sender, EntityEventArgs args)
        {
            var unit = args.Entity as Unit;

            if (MyHero.HasModifier("modifier_windrunner_focusfire"))
            {
                // Orbwalker.
                Orbwalker.CustomAttackSpeedValue = unit == _ultTarget ? 0 : _attackSpeed;
            }
            else
            {
                Orbwalker.CustomAttackSpeedValue = 0;
                _ultTarget = null;
            }
            Log.Debug($"new attack speed { Orbwalker.CustomAttackSpeedValue}");
           // Game.PrintMessage($"Orbwalker.CustomAttackSpeedValue: { Orbwalker.CustomAttackSpeedValue}");
        }

        protected override async Task<bool> Killsteal()
        {
            if (await base.Killsteal())
            {
                return true;
            }

            if (MyHero.IsSilenced() || MyHero.IsChanneling() || Target != null)
            {
                return false;
            }

            if (_powerShotAbility.IsKillstealAbilityEnabled() && _powerShotAbility.CanBeCasted())
            {
                var damage = (float) _powerShotAbility.GetDamage(_powerShotAbility.Level - 1);
                damage *= GetSpellAmp();

                var speed = _powerShotAbility.GetAbilityData("arrow_speed");
                var range = _powerShotAbility.GetAbilityData("arrow_range");
                var width = _powerShotAbility.GetAbilityData("arrow_width");
                var damageReduction = _powerShotAbility.GetAbilityData("damage_reduction"); // 0.2

                var enemies =
                    ObjectManager.GetEntitiesParallel<Hero>()
                                 .Where(
                                     x =>
                                         x.IsAlive && x.Team != MyHero.Team && !x.IsIllusion &&
                                         _powerShotAbility.CanBeCasted(x) &&
                                         _powerShotAbility.CanHit(x) && !x.IsMagicImmune() &&
                                         x.Health < damage * (1 - x.MagicResistance()) && !x.CantBeAttacked() &&
                                         !x.CantBeKilled());
                foreach (var enemy in enemies)
                {
                    var time = enemy.Distance2D(MyHero) / speed * 1000.0f;
                    var predictedPos = Prediction.Prediction.PredictPosition(enemy, (int)time, true);
                    if (predictedPos == Vector3.Zero || MyHero.Distance2D(predictedPos) >= range )
                    {
                        continue;
                    }
                    // check for reduction
                    var rec = new Geometry.Polygon.Rectangle(MyHero.NetworkPosition, predictedPos, width);

                    // test for enemies in range
                    var unitsHit = ObjectManager.GetEntitiesParallel<Unit>()
                                                      .Count(
                                                          x =>
                                                              x.IsValid && x != enemy && x.IsAlive && !(x is Building) && x != MyHero && x.Team != MyHero.Team && 
                                                              x.IsSpawned && x.IsRealUnit() &&
                                                              rec.IsInside(x.NetworkPosition));

                    var newDamage = damage * (unitsHit > 0 ? Math.Pow(1 - damageReduction, unitsHit) : 1.0f);
                    if(enemy.Health >= newDamage * (1 - enemy.MagicResistance()) )
                    {
                        Log.Debug($"not using powershot killsteal because too many units hit before ({unitsHit}) - {newDamage * (1 - enemy.MagicResistance())}");
                        continue;
                    }
                    var powerShotProp = GetPowerShotProp(enemy);
                    if (powerShotProp > 1.0f)
                    {

                        Log.Debug($"powershot prop too high {powerShotProp}");
                    }
                    else
                    {
                        time += (1000.0f * powerShotProp);
                        predictedPos = Prediction.Prediction.PredictPosition(enemy, (int)time, true);

                        Log.Debug(
                            $"use killsteal powershot because enough damage {enemy.Health} <= {damage * (1 - enemy.MagicResistance())} prop {powerShotProp}");
                        _powerShotAbility.UseAbility(predictedPos);
                        await Await.Delay(GetAbilityDelay(enemy, _powerShotAbility) + (int)time);
                        MyHero.Stop();
                        return true;
                    }
                }
            }

            return false;
        }

        private float GetPowerShotProp(Unit target)
        {
            var damage = (float)_powerShotAbility.GetDamage(_powerShotAbility.Level - 1);
            damage *= GetSpellAmp();

            var speed = _powerShotAbility.GetAbilityData("arrow_speed");
            var time = (float) (1.0f + MyHero.Distance2D(target) / speed + _powerShotAbility.FindCastPoint());
            var health = (target.Health + target.HealthRegeneration * time) * (1.0f + target.MagicResistance()) * 1.1f;
            // todo: calc from monkey ms/travel speed?
            Log.Debug($"health {health} | {target.HealthRegeneration * time}");
            var result = (health / damage) * 10.0f;
            result = (int) result / 10.0f;
            return result; // Math.Min(1.0f, result);
        }

        public float RelativeAngleToPositions(Vector3 start, Entity middle, Entity end)
        {
            var v1 = middle.NetworkPosition - start;
            var v2 = end.NetworkPosition - middle.NetworkPosition;
            return v1.AngleBetween(v2);
        }

        public Unit FindShackleTarget(Unit target)
        {
            return FindShackleTarget(target, MyHero.NetworkPosition);
        }

        public Unit FindShackleTarget(Unit target, Vector3 myPos)
        {
            var shackleDistance = _shackleAbility.GetAbilityData("shackle_distance");
            var angle = _shackleAbility.GetAbilityData("shackle_angle");
            var speed = _shackleAbility.GetAbilityData("arrow_speed");

            var distance = myPos.Distance2D(target);
            var time = distance / speed * 1000.0f;
            var predictedPos = Prediction.Prediction.PredictPosition(target, (int) time);

            if (distance <= _shackleAbility.GetCastRange() && !target.IsLinkensProtected())
            {
                // check for direct hit with unit behind
                var unitBehind =
                    ObjectManager.GetEntitiesParallel<Unit>()
                                 .Any(x => x.IsValid && x != target && x.IsAlive && x.Team != MyHero.Team && !(x is Building) &&
                                           x.IsSpawned && x.IsRealUnit() && x.Distance2D(predictedPos) < shackleDistance  && x.Distance2D(predictedPos) > x.HullRadius &&
                                           RelativeAngleToPositions(myPos, target, x) <= angle);
                if (unitBehind)
                {
                    Log.Debug($"unit behind shackle");
                    return target;
                }


                // check for tree behind
                var treeBehind =
                    ObjectManager.GetEntitiesParallel<Tree>()
                                 .Any(x => x.IsValid && x.IsAlive && x.Team != MyHero.Team &&
                                           x.Distance2D(predictedPos) < shackleDistance &&
                                           RelativeAngleToPositions(myPos, target, x) <= angle);
                if (treeBehind)
                {
                    Log.Debug($"tree behind shackle");
                    return target;
                }
            }

            // test if unit infront
            var unitsInfront =
                ObjectManager.GetEntitiesParallel<Unit>()
                             .Where(x => x.IsValid && x.IsAlive && x.Team != MyHero.Team && !(x is Building) && !x.IsMagicImmune() && !x.IsLinkensProtected() && _shackleAbility.CanBeCasted(x) && _shackleAbility.CanHit(x)
                                        && x.Distance2D(target) <= shackleDistance && RelativeAngleToPositions(myPos, x, target) <= angle);
            Log.Debug($"in front {unitsInfront.Count()}");
            foreach (var unit in unitsInfront)
            {
                var smallestAngle = ObjectManager.GetEntitiesParallel<Unit>()
                             .Where(x => x.IsValid && x != unit && x.IsAlive && x.Team != MyHero.Team &&
                                         x.IsSpawned && x.IsRealUnit() && x.Distance2D(unit) < shackleDistance && x.Distance2D(unit) > x.HullRadius &&
                                         RelativeAngleToPositions(myPos, unit, x) <= angle)
                             .OrderBy(x => RelativeAngleToPositions(myPos, unit, x)).FirstOrDefault();
                if (smallestAngle == target)
                {
                    Log.Debug($"found unit infront {unit.Name}");
                    return unit;
                }
                else
                {
                    Log.Debug($"not smalled angle {smallestAngle?.Name}");
                }
            }

            return null;
        }

        private async Task BlinkShackleBranch(Unit target, Item blink, Item branch, CancellationToken tk = default(CancellationToken))
        {
            var dir = (target.NetworkPosition - MyHero.NetworkPosition).Normalized();

            var shackleDistance = _shackleAbility.GetAbilityData("shackle_distance");

            var blinkPos = target.NetworkPosition + dir * shackleDistance / 2;
           

            _shackleAbility.UseAbility(target);
            Log.Debug($"using branch-shackle on {target.Name}");
            await Await.Delay(GetAbilityDelay(target, _shackleAbility) + 50, tk);

            Log.Debug($"using branch-blink on {blinkPos}");
            blink.UseAbility(blinkPos);
            await Await.Delay((int)(MyHero.GetTurnTime(blinkPos) * 1000) + ItemDelay, tk);

            Log.Debug($"using branch on {blinkPos}");
            branch.UseAbility(blinkPos);
            await Await.Delay(ItemDelay, tk);
        }

        private async Task BlinkShackleFindPos(Unit target, Item blink,
            CancellationToken tk = default(CancellationToken))
        {
            var speed = _shackleAbility.GetAbilityData("arrow_speed");
            var radius = _shackleAbility.GetCastRange() / 2;

            var time = radius / speed * 1000.0f + _shackleAbility.FindCastPoint() * 1000.0f;
            var predictedPos = Prediction.Prediction.PredictPosition(target, (int)time);

            var shackleAngle = _shackleAbility.GetAbilityData("shackle_angle");
            var dir = (MyHero.NetworkPosition - predictedPos).Normalized();
           

            for (float angle = 0; angle < 360.0f; angle += shackleAngle)
            {
                var myPos = dir;
                myPos.X += radius * (float)Math.Cos(Utils.DegreeToRadian(angle));
                myPos.Y += radius * (float)Math.Sin(Utils.DegreeToRadian(angle));

                myPos += predictedPos;

                var shackleUnit = FindShackleTarget(target, myPos);
                if (shackleUnit != null)
                {
                    Log.Debug($"using blink-shackle on {myPos}");
                    blink.UseAbility(myPos);
                    await Await.Delay((int)(MyHero.GetTurnTime(myPos) * 1000) + ItemDelay, tk);

                    _shackleAbility.UseAbility(shackleUnit);
                    Log.Debug($"using shackle after blink on {shackleUnit.Name}");
                    await Await.Delay(GetAbilityDelay(shackleUnit, _shackleAbility) + 50, tk);
                    return;
                }
            }
            Log.Debug($"couldn't find a valid blink pos for shackle");
        }


        public override async Task ExecuteComboAsync(Unit target, CancellationToken tk = new CancellationToken())
        {
            if (MyHero.IsChanneling())
                return;

            await HasNoLinkens(target, tk);
            await UseItems(target, tk);

            if (await DisableEnemy(target, tk) == DisabledState.UsedAbilityToDisable)
            {
                Log.Debug($"disabled!");
            }

            if (!MyHero.IsSilenced())
            {
                if (_powerShotAbility.IsAbilityEnabled() && _powerShotAbility.CanBeCasted(target) && _powerShotAbility.CanHit(target))
                {
                    var speed = _powerShotAbility.GetAbilityData("arrow_speed");
                    var time = target.Distance2D(MyHero) / speed * 1000.0f;

                    float disabledDuration;
                    if (target.IsDisabled(out disabledDuration) && disabledDuration >= time + 1.0f)
                    {
                        _powerShotAbility.UseAbility(target.NetworkPosition);
                        Log.Debug($"using powershot since target disabled for {disabledDuration}");
                        await Await.Delay(GetAbilityDelay(target, _powerShotAbility) + (int)(disabledDuration * 1000.0f), tk);
                        MyHero.Stop();
                    }
                    else
                    {
                        var range = _powerShotAbility.GetAbilityData("arrow_range");
                        var predictedPos = Prediction.Prediction.PredictPosition(target, (int)time + 1000, true);
                        var distance = MyHero.Distance2D(predictedPos);
                        if (predictedPos != Vector3.Zero && distance < range)
                        {
                            var damage = (float) _powerShotAbility.GetDamage(_powerShotAbility.Level - 1);
                            damage *= GetSpellAmp();
                            if (target.Health <= damage * (1.0f - target.MagicResistance()))
                            {
                                var powerShotProp = GetPowerShotProp(target);
                                time += (1000.0f * powerShotProp);
                                predictedPos = Prediction.Prediction.PredictPosition(target, (int) time);

                                _powerShotAbility.UseAbility(predictedPos);
                                Log.Debug($"using powershot since target can be killed");
                               
                                await Await.Delay(GetAbilityDelay(target, _powerShotAbility) + (int)time, tk);
                                MyHero.Stop();
                            }
                            else if (!_shackleAbility.CanBeCasted(target) && !MyHero.HasItem(ClassID.CDOTA_Item_BlinkDagger) && distance > MyHero.GetAttackRange() * 1.25f)
                            {
                                _powerShotAbility.UseAbility(predictedPos);
                                Log.Debug($"using powershot since no blink or shackle");
                                await Await.Delay(GetAbilityDelay(target, _powerShotAbility) + 1000, tk);
                            }
                        }
                    }
                }


                if (_shackleAbility.IsAbilityEnabled() && _shackleAbility.CanBeCasted(target))
                {
                    var shackleTarget = FindShackleTarget(target);
                    if (shackleTarget != null)
                    {
                        _shackleAbility.UseAbility(shackleTarget);
                        Log.Debug($"using shackle on {shackleTarget.Name}");
                        await Await.Delay(GetAbilityDelay(shackleTarget, _shackleAbility), tk);
                    }
                    else if( ZaioMenu.ShouldUseBlinkDagger)
                    {
                        // test for iron branch jump
                        var blink = MyHero.GetItemById(ItemId.item_blink);
                        var distance = MyHero.Distance2D(target);
                        if (blink != null  && blink.CanBeCasted())
                        {
                            var ironBranch = MyHero.GetItemById(ItemId.item_branches);
                            if (ShouldUseBranchShackle && ironBranch != null && distance >= 220 && distance <= blink.GetCastRange() - _shackleAbility.GetAbilityData("shackle_distance") / 2)
                            {
                                await BlinkShackleBranch(target, blink, ironBranch, tk);
                                Log.Debug($"used ironbranch trick");
                            }
                            else if(distance < blink.GetCastRange() + _shackleAbility.GetCastRange())
                            {
                                // find good blink pos
                                Log.Debug($"using blink shackle find pos");
                                await BlinkShackleFindPos(target, blink, tk);
                            }
                        }
                    }
                   
                }

                if (_ultAbility.IsAbilityEnabled() && (!_shackleAbility.CanBeCasted() || target.IsDisabled()) && _ultAbility.CanBeCasted(target) && _ultAbility.CanHit(target))
                {
                    if (!MyHero.HasModifier("modifier_windrunner_focusfire"))
                        _attackSpeed = UnitDatabase.GetAttackSpeed(MyHero);

                    Log.Debug($"use ult");
                    _ultAbility.UseAbility(target);
                    _ultTarget = target;
                    await Await.Delay(GetAbilityDelay(target, _ultAbility), tk);
                }
            }

            // check if we are near the enemy
            if (!_shackleAbility.CanBeCasted(target) || !_shackleAbility.IsAbilityEnabled())
            {
                if (!await MoveOrBlinkToEnemy(target, tk))
                {
                    Log.Debug($"return because of blink");
                    return;
                }
            }
            else if (!await MoveToEnemy(target, tk))
            {
                Log.Debug($"return because of move");
                return;
            }

            if (ZaioMenu.ShouldUseOrbwalker && (!MyHero.HasModifier("modifier_windrunner_focusfire" )|| ShouldUseOrbwalkWhileUlt))
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