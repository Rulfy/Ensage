/*
Radiant Bot: 
X:-3772,969 Y:-6178,125 Z:384
X:4680,375 Y:-6174,563 Z:384
X:5991,813 Y:-5084,563 Z:384
X:6254,5 Y:-1836,813 Z:384
X:6377,156 Y:2874,313 Z:384 Dire Bot


Radiant Mid:
X:-4411,344 Y:-3951,813 Z:384
X:-446,7813 Y:-220,2188 Z:128,2188
X:4127,969 Y:3679,5 Z:384 dire mid

Radiant Top:
X:-6642,813 Y:-3158,719 Z:384
X:-6194,781 Y:5079,656 Z:384
X:-4905,031 Y:5775,781 Z:384
X:3365,406 Y:5799,75 Z:384
*/

namespace Zaio.Heroes
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;
    using System.Threading;
    using System.Threading.Tasks;
    using System.Windows.Input;

    using Ensage;
    using Ensage.Common;
    using Ensage.Common.Enums;
    using Ensage.Common.Extensions;
    using Ensage.Common.Extensions.SharpDX;
    using Ensage.Common.FuncWheel;
    using Ensage.Common.Menu;
    using Ensage.Common.Objects;
    using Ensage.Common.Threading;

    using log4net;

    using PlaySharp.Toolkit.Logging;

    using SharpDX;

    using Zaio.Helpers;
    using Zaio.Interfaces;

    using AbilityId = Ensage.Common.Enums.AbilityId;

    [Hero(ClassID.CBaseButton)] // CDOTA_Unit_Hero_Meepo
    internal class Meepo : ComboHero
    {
        private static readonly string[] KillstealAbilities = { "meepo_poof" };

        private static readonly ILog Log = AssemblyLogs.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private static readonly string[] SupportedAbilities = { "meepo_earthbind", "meepo_poof" };

        private readonly List<Vector3> _radiantToDireBot = new List<Vector3>
                                                               {
                                                                   new Vector3(-3704, -6135, 384),
                                                                   new Vector3(5622, -5819, 384),
                                                                   new Vector3(6251, -5188, 384),
                                                                   new Vector3(6351, 3616, 384)
                                                               };

        private readonly List<Vector3> _radiantToDireMid = new List<Vector3>
                                                               {
                                                                   new Vector3(-4805, -4268, 384),

                                                                   // new Vector3(-446, -220, 128),
                                                                   new Vector3(4056, 3585, 384)
                                                               };

        private readonly List<Vector3> _radiantToDireTop = new List<Vector3>
                                                               {
                                                                   new Vector3(-6612, -3890, 384),
                                                                   new Vector3(-6246, 4884, 384),
                                                                   new Vector3(-5271, 5754, 384),
                                                                   new Vector3(3254, 5762, 384)
                                                               };

        private List<JungleCamp> _availableCamps = new List<JungleCamp>();

        private List<Vector3> _enemyCreepRouteBottom;

        private List<Vector3> _enemyCreepRouteMid;

        private List<Vector3> _enemyCreepRouteTop;

        private WheelEntry _farmJungleEntry;

        private List<JungleFarmer> _farmJungleIllusions = new List<JungleFarmer>();

        private FunctionWheel _functionWheel;

        private MenuItem _functionWheelItem;

        private List<LanePusher> _lanePusherUnits = new List<LanePusher>();

        private int _lastAvailableCampRefreshMinute;

        private WheelEntry _mixedModeEntry;

        private WheelEntry _pushLaneEntry;

        private Ability _qAbility;

        private WheelEntry _stackJungleEntry;

        private Ability _wAbility;

        private IEnumerable<Hero> MyMeepos
        {
            get
            {
                var result =
                    ObjectManager.GetEntitiesParallel<Hero>()
                                 .Where(x => x.IsValid && x.IsAlive && x is Meepo && x.IsControllable);

                // Log.Debug($"Returning {result.Count()} illusions");
                return result;
            }
        }


        public override async Task ExecuteComboAsync(Unit target, CancellationToken tk = new CancellationToken())
        {
            await this.HasNoLinkens(target, tk);
            await this.UseItems(target, tk);

            // make him disabled
            await this.DisableEnemy(target, tk);

          
            // check if we are near the enemy
            if (!await this.MoveOrBlinkToEnemy(target, tk))
            {
                Log.Debug($"return because of blink");
                return;
            }

            if (ZaioMenu.ShouldUseOrbwalker)
            {
                this.Orbwalk();
            }
            else
            {
                this.MyHero.Attack(target);
                await Await.Delay(125, tk);
            }
        }

        public override void OnClose()
        {
            Game.OnIngameUpdate -= this.Game_OnIngameUpdate;
            base.OnClose();
        }

        public override void OnLoad()
        {
            base.OnLoad();
            var heroMenu = new Menu("Meepo", "zaioMeepo", false, "npc_dota_hero_meepo", true);

            heroMenu.AddItem(new MenuItem("zaioMeepoAbilitiesText", "Supported Abilities"));
            var supportedStuff = new MenuItem("zaioMeepoAbilities", string.Empty);
            supportedStuff.SetValue(new AbilityToggler(SupportedAbilities.ToDictionary(x => x, y => true)));
            heroMenu.AddItem(supportedStuff);

            heroMenu.AddItem(new MenuItem("zaioMeepoKillstealAbilitiesText", "Supported Killsteal Abilities"));
            var supportedKillsteal = new MenuItem("zaioMeepoKillstealAbilities", string.Empty);
            supportedKillsteal.SetValue(new AbilityToggler(KillstealAbilities.ToDictionary(x => x, y => true)));
            heroMenu.AddItem(supportedKillsteal);

            this.OnLoadMenuItems(supportedStuff, supportedKillsteal);

            this._functionWheelItem =
                new MenuItem("zaioMeepoFunctionWheel", "Function Wheel").SetValue(new KeyBind(0, KeyBindType.Press));
            this._functionWheelItem.Tooltip = "Hotkey for showing the function wheel.";
            this._functionWheelItem.ValueChanged += this._functionWheelItem_ValueChanged;
            heroMenu.AddItem(this._functionWheelItem);

            ZaioMenu.LoadHeroSettings(heroMenu);

            this._qAbility = this.MyHero.GetAbilityById(AbilityId.meepo_earthbind);
            this._wAbility = this.MyHero.GetAbilityById(AbilityId.meepo_poof);

            this._stackJungleEntry = new WheelEntry("Stack Jungle", this.StackJungle, false);
            this._farmJungleEntry = new WheelEntry("Farm Jungle", this.FarmJungleFunc);
            this._pushLaneEntry = new WheelEntry("Push Lanes", this.PushLanesFunc);
            this._mixedModeEntry = new WheelEntry("Mixed Mode", this.MixedMode);

            var key = KeyInterop.KeyFromVirtualKey((int)this._functionWheelItem.GetValue<KeyBind>().Key);
            this._functionWheel = new FunctionWheel(key)
                                      {
                                          this._mixedModeEntry,
                                          this._pushLaneEntry,
                                          this._farmJungleEntry,
                                          this._stackJungleEntry
                                      };

            this._availableCamps = new List<JungleCamp>(JungleCamps.GetCamps);
            Log.Debug($"Init jungle camps! {this._availableCamps.Count}");

            if (this.MyHero.Team == Team.Dire)
            {
                Log.Debug($"using radiant to dire creep lanes");
                this._enemyCreepRouteTop = this._radiantToDireTop;
                this._enemyCreepRouteMid = this._radiantToDireMid;
                this._enemyCreepRouteBottom = this._radiantToDireBot;
            }
            else
            {
                Log.Debug($"using reversed radiant to dire creep lanes");
                this._enemyCreepRouteTop = new List<Vector3>(this._radiantToDireTop);
                this._enemyCreepRouteMid = new List<Vector3>(this._radiantToDireMid);
                this._enemyCreepRouteBottom = new List<Vector3>(this._radiantToDireBot);

                this._enemyCreepRouteTop.Reverse();
                this._enemyCreepRouteMid.Reverse();
                this._enemyCreepRouteBottom.Reverse();
            }

            Game.OnIngameUpdate += this.Game_OnIngameUpdate;
        }

        protected override async Task<bool> Killsteal()
        {
            if (await base.Killsteal())
            {
                return true;
            }

            if (this.MyHero.IsSilenced())
            {
                return false;
            }

            if (this._wAbility.IsKillstealAbilityEnabled() && this._wAbility.CanBeCasted())
            {
                var damage = (float)_wAbility.GetDamage(_wAbility.Level - 1);
                damage *= this.GetSpellAmp();

                var radius = _wAbility.GetAbilityData("radius");
                var meepos = this.MyMeepos;
                var enemy =
                    ObjectManager.GetEntitiesParallel<Hero>()
                                 .FirstOrDefault(
                                     x =>
                                         x.IsAlive && x.Team != this.MyHero.Team && !x.IsIllusion
                                         && _wAbility.CanBeCasted(x)
                                         && meepos.Any(y => y.Distance2D(x) < radius) && !x.IsMagicImmune()
                                         && x.Health < damage * (1 - x.MagicResistance()) && !x.CantBeAttacked()
                                         && !x.CantBeKilled());
                if (enemy != null)
                {
                    Log.Debug(
                        $"use riptide because enough damage {enemy.Health} <= {damage * (1 - enemy.MagicResistance())} ");
                    _wAbility.UseAbility();
                    await Await.Delay(this.GetAbilityDelay(_wAbility));
                    return true;
                }
            }

            return false;
        }

        private void _functionWheelItem_ValueChanged(object sender, OnValueChangeEventArgs e)
        {
            if (e.GetNewValue<KeyBind>().Key != e.GetOldValue<KeyBind>().Key)
            {
                this._functionWheel.KeyCode = this._functionWheelItem.GetValue<KeyBind>().Key;
            }
        }

    
        private async Task FarmJungle(List<Unit> units)
        {
            if (!units.Any())
            {
                Log.Debug($"no units to farm the jungle!");
                return;
            }

            var movespeed = (float)this.MyHero.MovementSpeed;
            var jungleCamps = this._availableCamps.Where(x => !x.Ancients).ToList();

            Log.Debug($"Available camps: {jungleCamps.Count}");
            foreach (var unit in units)
            {
                // all camps are either cleared or already occupied by an illusion, so just send moar
                if (!jungleCamps.Any())
                {
                    jungleCamps = this._availableCamps.Where(x => !x.Ancients).ToList();
                }

                var jungleCamp = jungleCamps.OrderBy(x => x.WaitPosition.Distance2D(unit)).First();
                Log.Debug($"Moving unit to farm {jungleCamp.Name}");
                this._farmJungleIllusions.Add(new JungleFarmer(unit, jungleCamp));
                unit.Move(jungleCamp.CampPosition);
                unit.Attack(jungleCamp.CampPosition, true);
                await Await.Delay(50);
                jungleCamps.Remove(jungleCamp);
            }
        }

        private async Task FarmJungleFunc()
        {
            await this.FarmJungle(this.MyPlayer.Selection.OfType<Unit>().ToList());
        }

        private float FindIntersection(Unit unit, Vector3 start, Vector3 end, float speed = 325)
        {
            // sx^2 + sy^2 + (2 * sx * vx + 2 * sy * vy) * t + (vx^2 + vy^2 - v2^2) * t^2 = 0
            var pos = unit.NetworkPosition - start;
            var distance = pos.Length();

            var v = (end - start).Normalized() * speed;
            var mySpeed = (float)(unit.MovementSpeed * unit.MovementSpeed);

            var a = mySpeed - speed * speed;
            var b = 2 * Vector3.Dot(pos, v);
            var c = -distance * distance;

            var discr = b * b - 4 * a * c;
            if (discr < 0)
            {
                throw new Exception($"discr < 0 ({discr})");
            }

            if (discr == 0)
            {
                var t = -b / (2 * a);
                Log.Debug($"discr == 0 -> {t}");
                return t;
            }

            discr = (float)Math.Sqrt(discr);
            var t1 = (-b + discr) / (2 * a);
            var t2 = (-b - discr) / (2 * a);

            if (t1 < 0 && t2 < 0)
            {
                throw new Exception($"t1 and t2 < 0 ({t1} | {t2})");
            }

            float time;
            if (t1 > 0 && t2 > 0)
            {
                time = Math.Min(t1, t2);
            }
            else
            {
                time = Math.Max(t1, t2);
            }

            Log.Debug($"discr >0 -> {time} with ({t1} | {t2})");
            return time;
        }

        private async void Game_OnIngameUpdate(EventArgs args)
        {
            if (Utils.SleepCheck("zaioMeepoFunctionWheelSleep"))
            {
                Utils.Sleep(500, "zaioMeepoFunctionWheelSleep");

                // test function wheel items
                var gameTime = (int)Game.GameTime;
                var minute = gameTime / 60;
                var seconds = gameTime % 60;
                if (minute % 2 == 1)
                {
                    // refresh available jungle camps
                    if (minute != this._lastAvailableCampRefreshMinute)
                    {
                        this._availableCamps = new List<JungleCamp>(JungleCamps.GetCamps);
                        this._lastAvailableCampRefreshMinute = minute;
                        Log.Debug($"Refreshing jungle camps! {this._availableCamps.Count}");
                    }

                    // Log.Debug($"{minute % 2} == 1 => FALSE");
                    this._stackJungleEntry.IsEnabled = false;
                    this._stackJungleEntry.DisplayName = $"Stack Jungle ({91 - seconds:00}s)";
                }
                else
                {
                    // Log.Debug($"{60 - seconds} > {estimatedTime} => TRUE");
                    this._stackJungleEntry.IsEnabled = true;
                    this._stackJungleEntry.DisplayName = "Stack Jungle";
                }

                this._farmJungleEntry.IsEnabled = this._availableCamps.Any();
                this._mixedModeEntry.IsEnabled = this._farmJungleEntry.IsEnabled || this._pushLaneEntry.IsEnabled;
            }

            if (Utils.SleepCheck("zaioMeepoPushLanes"))
            {
                Utils.Sleep(250, "zaioMeepoPushLanes");
                this._lanePusherUnits = this._lanePusherUnits.Where(x => x.Unit.IsAlive).ToList();
                if (!this._lanePusherUnits.Any())
                {
                    return;
                }

                foreach (var lanePusher in this._lanePusherUnits)
                {
                    lanePusher.UpdateParticleEffect();

                    var illusion = lanePusher.Unit;
                    if (!(illusion.Distance2D(lanePusher.TargetPosition) < 250))
                    {
                        await lanePusher.RefreshCommand();
                        continue;
                    }

                    var unitNearby =
                        ObjectManager.GetEntitiesParallel<Unit>()
                                     .Any(
                                         x =>
                                             x.IsValid && x.IsAlive && x.Team != illusion.Team && x.IsSpawned
                                             && x.IsRealUnit() && x.Distance2D(illusion) < 300);
                    if (!unitNearby)
                    {
                        var nextUnit =
                            ObjectManager.GetEntitiesParallel<Unit>()
                                         .FirstOrDefault(
                                             x =>
                                                 x.IsValid && x.IsAlive && x.Team != illusion.Team && x.IsSpawned
                                                 && x.IsRealUnit() && x.Distance2D(illusion) < 300);
                        if (nextUnit != null)
                        {
                            lanePusher.CurrentTargetPosition = new UnitOrPosition(nextUnit);
                            illusion.Attack(nextUnit.NetworkPosition);
                            Log.Debug($"illusion pusher attacking next unit");
                            await Await.Delay(50);
                        }
                        else
                        {
                            var nextPos = lanePusher.NextLanePosition;
                            lanePusher.CurrentTargetPosition = new UnitOrPosition(nextPos);
                            illusion.Attack(nextPos);
                            await Await.Delay(50);
                        }
                    }
                    else
                    {
                        var towerAttacker =
                            ObjectManager.GetEntitiesParallel<Tower>()
                                         .FirstOrDefault(
                                             x =>
                                                 x.IsValid && x.IsAlive && x.Team != illusion.Team
                                                 && x.AttackTarget == illusion);
                        if (towerAttacker != null)
                        {
                            // move away from tower
                            var dir = (illusion.NetworkPosition - towerAttacker.NetworkPosition).Normalized();
                            var targetPos = towerAttacker.NetworkPosition + dir * (towerAttacker.AttackRange * 1.4f);
                            illusion.Move(targetPos);
                            illusion.Attack(targetPos, true);
                        }
                        else
                        {
                            var heroesNear =
                                ObjectManager.GetEntitiesParallel<Hero>()
                                             .Any(
                                                 x =>
                                                     x.IsValid && x.IsAlive && !x.IsIllusion
                                                     && x.Team != this.MyHero.Team);
                            if (heroesNear)
                            {
                                // TODO: move to creep spawn
                            }
                        }
                    }
                }
            }

            if (Utils.SleepCheck("zaioMeepoFarmJungle"))
            {
                Utils.Sleep(250, "zaioMeepoFarmJungle");
                this._farmJungleIllusions = this._farmJungleIllusions.Where(x => x.Unit.IsAlive).ToList();
                if (!this._farmJungleIllusions.Any())
                {
                    return;
                }

                foreach (var farmJungleIllusion in this._farmJungleIllusions)
                {
                    farmJungleIllusion.UpdateParticleEffect();

                    // test if illusion is already at the camp but no creeps are left
                    var illusion = farmJungleIllusion.Unit;
                    if (!(illusion.Distance2D(farmJungleIllusion.JungleCamp.CampPosition) < 150))
                    {
                        continue;
                    }

                    var unitNearby =
                        ObjectManager.GetEntitiesParallel<Unit>()
                                     .Any(
                                         x =>
                                             x.IsValid && x.IsAlive && x.Team != illusion.Team && x.IsSpawned
                                             && x.IsRealUnit() && x.Distance2D(illusion) < 500);
                    if (!unitNearby)
                    {
                        // remove from available camps list
                        this._availableCamps.Remove(farmJungleIllusion.JungleCamp);
                        var jungleCamps = this._availableCamps.Where(x => !x.Ancients).ToList();

                        // have to find another jungle spot (if possible without a illusion nearby)
                        var timeRemaining = illusion.GetIllusionRemainingTime();
                        var moveSpeed = (float)illusion.MovementSpeed;

                        var campsReachable =
                            jungleCamps.Where(x => x.CampPosition.Distance2D(illusion) / moveSpeed <= timeRemaining)
                                       .OrderBy(x => x.CampPosition.Distance2D(illusion));
                        if (campsReachable.Any())
                        {
                            var freeCamp =
                                campsReachable.FirstOrDefault(
                                    x => this._farmJungleIllusions.All(y => y.JungleCamp.Id != x.Id));
                            if (freeCamp != null)
                            {
                                // use the free camp
                                farmJungleIllusion.JungleCamp = freeCamp;
                                Log.Debug($"Moving illusion to free camp {freeCamp.Name}");
                            }
                            else
                            {
                                // use first closest camp
                                var closestCamp = campsReachable.First();
                                farmJungleIllusion.JungleCamp = closestCamp;
                                Log.Debug($"Moving illusion to new closest camp {closestCamp.Name}");
                            }

                            illusion.Move(farmJungleIllusion.JungleCamp.CampPosition);
                            illusion.Attack(farmJungleIllusion.JungleCamp.CampPosition, true);
                            await Await.Delay(50);
                        }
                    }
                }
            }
        }

        private UnitOrPosition GetCreepWavePosition(
            Hero illusion,
            List<Vector3> creepWalkingPositions,
            List<Creep> myCreeps,
            List<Creep> enemyCreeps)
        {
            // newest wave
            var seconds = (int)Math.Ceiling(Game.GameTime) % 60;
            var lastWave = seconds % 30;
            var nextWave = (60 - seconds) % 30;

            /* until 7:29
                            406 dire top, radiant bot
                            243 dire bot and radiant top
                            then at 7:30 only 325
                        */
            var creepSpeed = 315; // usually 325, but our creep paths are sloppy and mostly too short 
            try
            {
                var dist = illusion.Distance2D(creepWalkingPositions[0]);
                var distTime = dist / illusion.MovementSpeed;

                var predictionTime = lastWave;
                if (nextWave < distTime && nextWave < lastWave)
                {
                    Log.Debug($"using next wave in {nextWave}");
                    predictionTime = -nextWave;
                }
                else
                {
                    Log.Debug($"using last wave wave from {lastWave}");
                }

                var dir = (creepWalkingPositions[1] - creepWalkingPositions[0]).Normalized();
                var start = creepWalkingPositions[0] + dir * creepSpeed * predictionTime;
                var intersectionTime = this.FindIntersection(illusion, start, creepWalkingPositions[1], creepSpeed);
                var intersection = start + dir * creepSpeed * intersectionTime;
                Log.Debug(
                    $"intersection = {creepWalkingPositions[0]} => {start} + {dir} * {intersectionTime} * {creepSpeed} = {intersection}");

                var nearestAllyCreep =
                    myCreeps.FirstOrDefault(
                        x =>
                            x.GetShortestDistance(creepWalkingPositions[0], creepWalkingPositions[1])
                            <= intersection.Distance2D(creepWalkingPositions[0]));
                if (nearestAllyCreep != null)
                {
                    predictionTime = -nextWave;
                    Log.Debug($"allied creep is closer, so cutting next wave {predictionTime}");
                    start = creepWalkingPositions[0] + dir * creepSpeed * predictionTime;
                    intersectionTime = this.FindIntersection(illusion, start, creepWalkingPositions[1], creepSpeed);
                    intersection = start + dir * creepSpeed * intersectionTime;
                    Log.Debug(
                        $"new intersection = {creepWalkingPositions[0]} => {start} + {dir} * {intersectionTime} * {creepSpeed} = {intersection}");
                }

                var reversed = new List<Vector3>(creepWalkingPositions);
                reversed.Reverse();

                var nearestCreep =
                    enemyCreeps.FirstOrDefault(
                        x => x.GetShortestDistance(reversed) <= intersection.Distance2D(creepWalkingPositions[0]));

                // var nearestCreep =
                // enemyCreeps.FirstOrDefault(
                // x => 
                // x.GetShortestDistance(creepWalkingPositions[0], creepWalkingPositions[1]) <= intersection.Distance2D(creepWalkingPositions[0]));
                if (nearestCreep != null)
                {
                    Log.Debug($"nearest creep closer than calc position {intersection}");
                    return new UnitOrPosition(nearestCreep);
                }

                return new UnitOrPosition(intersection);
            }
            catch (Exception e)
            {
                // ignored
                Log.Debug($"{e}");
                throw;
            }
        }

        private List<UnitOrPositionWithRoute> GetCreepWavePositions(Hero illusion)
        {
            var enemyCreeps =
                ObjectManager.GetEntitiesParallel<Creep>()
                             .Where(x => x.IsValid && x.IsAlive && x.Team != this.MyHero.Team && x.IsSpawned)
                             .ToList();
            var myCreeps =
                ObjectManager.GetEntitiesParallel<Creep>()
                             .Where(x => x.IsValid && x.IsAlive && x.Team != this.MyHero.Team && x.IsSpawned)
                             .ToList();

            var result = new List<UnitOrPositionWithRoute>();
            try
            {
                var pos = this.GetCreepWavePosition(illusion, this._enemyCreepRouteTop, myCreeps, enemyCreeps);
                result.Add(new UnitOrPositionWithRoute(pos, this._enemyCreepRouteTop));
            }
            catch (Exception)
            {
                // ignored
            }

            try
            {
                var pos = this.GetCreepWavePosition(illusion, this._enemyCreepRouteMid, myCreeps, enemyCreeps);
                result.Add(new UnitOrPositionWithRoute(pos, this._enemyCreepRouteMid));
            }
            catch (Exception)
            {
                // ignored
            }

            try
            {
                var pos = this.GetCreepWavePosition(illusion, this._enemyCreepRouteBottom, myCreeps, enemyCreeps);
                result.Add(new UnitOrPositionWithRoute(pos, this._enemyCreepRouteBottom));
            }
            catch (Exception)
            {
                // ignored
            }

            return result;
        }

      
        private async Task MixedMode()
        {
            var units = this.MyPlayer.Selection.OfType<Unit>().ToList();
            Log.Debug($"mixed mode got {units.Count} units");
            if (units.Count <= 2 || !this._availableCamps.Any())
            {
                await this.PushLanes(units);
            }
            else
            {
                var tasks = new List<Task>
                                {
                                    Task.Factory.StartNew(
                                        async () =>
                                            {
                                                Log.Debug($"doing push lanes task");
                                                await this.PushLanes(units.GetRange(0, 2));
                                            },

                                        // this will use current synchronization context
                                        CancellationToken.None,
                                        TaskCreationOptions.None,
                                        TaskScheduler.FromCurrentSynchronizationContext()),
                                    Task.Factory.StartNew(
                                        async () =>
                                            {
                                                Log.Debug($"doing farm jungle");
                                                await this.FarmJungle(units.GetRange(2, units.Count - 2));
                                            },

                                        // this will use current synchronization context
                                        CancellationToken.None,
                                        TaskCreationOptions.None,
                                        TaskScheduler.FromCurrentSynchronizationContext())
                                };

                Log.Debug($"waiting for mixed mode tasks");
                await Task.WhenAll(tasks);
                Log.Debug($"all mixed mode tasks done!");
            }
        }

        private async Task PushLanes(List<Unit> units)
        {
            // we assume the illusions are at our position Kappa
            var wavePositions = this.GetCreepWavePositions(this.MyHero);

            var positions = new List<UnitOrPositionWithRoute>(wavePositions);
            foreach (var unit in units)
            {
                if (positions.Count == 0)
                {
                    positions = new List<UnitOrPositionWithRoute>(wavePositions);
                }

                foreach (var position in positions.OrderBy(x => x.UPos.Position.Distance2D(unit)))
                {
                    unit.Move(position.UPos.Position);
                    unit.Attack(position.UPos.Position, true);
                    this._lanePusherUnits.Add(new LanePusher(unit, position.UPos, position.Route));
                    positions.Remove(position);
                    await Await.Delay(50);
                    break;
                }
            }
        }

        private async Task PushLanesFunc()
        {
            await this.PushLanes(this.MyPlayer.Selection.OfType<Unit>().ToList());
        }

        private async Task StackJungle()
        {
            var gameTime = (int)Math.Floor(Game.GameTime);

            var minute = gameTime / 60;
            if (minute % 2 == 1)
            {
                return;
            }

            var seconds = gameTime % 60;

            // get illusions
            var units = this.MyPlayer.Selection.OfType<Unit>().ToList();
            if (!units.Any())
            {
                Log.Debug($"didnt find any selected units");
                return;
            }

            var movespeed = units.First().MovementSpeed;
            var tasks = new List<Task>();
            var myCamps =
                this._availableCamps.Where(x => x.Team == this.MyHero.Team).ToList();
            foreach (var unit in units.OrderBy(x => x.GetIllusionRemainingTime()))
            {
                foreach (var jungleCamp in myCamps.OrderBy(x => x.WaitPosition.Distance2D(unit)))
                {
                    var distance = jungleCamp.WaitPosition.Distance2D(unit);
                    var timeToReachWaitPos = distance / movespeed;

                    var secondsTillStack = 0;
                    if (seconds <= jungleCamp.StackTime)
                    {
                        secondsTillStack = (int)(jungleCamp.StackTime - seconds);
                    }
                    else
                    {
                        secondsTillStack = (int)(60 - seconds + jungleCamp.StackTime);
                    }

                    if (minute % 2 == 1)
                    {
                        secondsTillStack += 60;
                    }

                    // too far away to stack
                    if (secondsTillStack < timeToReachWaitPos)
                    {
                        Log.Debug($"too far {secondsTillStack} <= {timeToReachWaitPos}");
                        continue;
                    }

                    var stackTimeNeeded = secondsTillStack + (int)(61 - jungleCamp.StackTime);

                    Log.Debug($"add task");
                    var task = Task.Factory.StartNew(
                        async () =>
                            {
                                unit.Move(jungleCamp.WaitPosition);
                                unit.Hold(true);
                                var timeToCamp =
                                    jungleCamp.CampPosition.Distance2D(jungleCamp.WaitPosition)
                                    / movespeed;
                                Log.Debug(
                                    $"Stacking: {jungleCamp.Name} ({jungleCamp.Team}) -> start in {secondsTillStack} - {timeToCamp}s + {Game.GameTime % 60} = {(secondsTillStack - timeToCamp + gameTime) % 60}");
                                await Await.Delay(
                                    (int)
                                    (Math.Floor(secondsTillStack - timeToCamp) * 1000.0f - Game.Ping));
                                unit.Move(jungleCamp.CampPosition);
                                unit.Move(jungleCamp.StackPosition, true);
                                await Await.Delay(50);
                            },

                        // this will use current synchronization context
                        CancellationToken.None,
                        TaskCreationOptions.None,
                        TaskScheduler.FromCurrentSynchronizationContext());

                    tasks.Add(task);

                    myCamps.Remove(jungleCamp);
                    break;
                }
            }

            // wait until all tasks are completed
            Log.Debug($"wait till all are completed");
            await Task.WhenAll(tasks);
            Log.Debug($"stacking done!");
        }
    }
}
