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
using AbilityId = Ensage.AbilityId;


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
    [Hero(ClassId.CDOTA_Unit_Hero_Naga_Siren)]
    internal class Naga : ComboHero
    {
        private static readonly ILog Log = AssemblyLogs.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private static readonly string[] SupportedAbilities =
        {
            "naga_siren_mirror_image",
            "naga_siren_rip_tide",
            "item_manta"
        };

        private static readonly string[] KillstealAbilities =
        {
            "naga_siren_rip_tide"
        };

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

        private Ability _illuAbility;

        private Dictionary<Unit, ParticleEffect> _illusionEffectDictionary = new Dictionary<Unit, ParticleEffect>();
        private List<LanePusher> _lanePusherIllusions = new List<LanePusher>();
        private int _lastAvailableCampRefreshMinute;
        private WheelEntry _mixedModeEntry;
        private WheelEntry _pushLaneEntry;
        private Ability _ripTideAbility;
        private Ability _rootAbility;
        private MenuItem _stackAnctientsItem;
        private WheelEntry _stackJungleEntry;


        private bool ShouldStackAncients => _stackAnctientsItem.GetValue<bool>();

        private IEnumerable<Hero> MyIllusions
        {
            get
            {
                var result = ObjectManager.GetEntitiesParallel<Hero>()
                                          .Where(x => x.IsValid && x.IsAlive && x.IsIllusion && x.IsControllable);
               // Log.Debug($"Returning {result.Count()} illusions");
                return result;
            }
        }


        public override void OnLoad()
        {
            base.OnLoad();
            var heroMenu = new Menu("Naga", "zaioNaga", false, "npc_dota_hero_naga_siren", true);

            heroMenu.AddItem(new MenuItem("zaioNagaAbilitiesText", "Supported Abilities"));
            var supportedStuff = new MenuItem("zaioNagaAbilities", string.Empty);
            supportedStuff.SetValue(new AbilityToggler(SupportedAbilities.ToDictionary(x => x, y => true)));
            heroMenu.AddItem(supportedStuff);

            heroMenu.AddItem(new MenuItem("zaioNagaKillstealAbilitiesText", "Supported Killsteal Abilities"));
            var supportedKillsteal = new MenuItem("zaioNagaKillstealAbilities", string.Empty);
            supportedKillsteal.SetValue(new AbilityToggler(KillstealAbilities.ToDictionary(x => x, y => true)));
            heroMenu.AddItem(supportedKillsteal);

            OnLoadMenuItems(supportedStuff, supportedKillsteal);

             _functionWheelItem =
                new MenuItem("zaioNagaFunctionWheel", "Function Wheel").SetValue(new KeyBind(0, KeyBindType.Press));
            _functionWheelItem.Tooltip = "Hotkey for showing the function wheel.";
            _functionWheelItem.ValueChanged += _functionWheelItem_ValueChanged;
            heroMenu.AddItem(_functionWheelItem);

            _stackAnctientsItem = new MenuItem("zaioNagaStackAncients", "Stack Ancients?").SetValue(false);
            heroMenu.AddItem(_stackAnctientsItem);

            ZaioMenu.LoadHeroSettings(heroMenu);

            _illuAbility = MyHero.GetAbilityById(AbilityId.naga_siren_mirror_image);
            _rootAbility = MyHero.GetAbilityById(AbilityId.naga_siren_ensnare);
            _ripTideAbility = MyHero.GetAbilityById(AbilityId.naga_siren_rip_tide);

            _stackJungleEntry = new WheelEntry("Stack Jungle", StackJungle, false);
            _farmJungleEntry = new WheelEntry("Farm Jungle", FarmJungleFunc);
            _pushLaneEntry = new WheelEntry("Push Lanes", PushLanesFunc);
            _mixedModeEntry = new WheelEntry("Mixed Mode", MixedMode);

            var key = KeyInterop.KeyFromVirtualKey((int) _functionWheelItem.GetValue<KeyBind>().Key);
            _functionWheel = new FunctionWheel(key)
            {
                _mixedModeEntry,
                _pushLaneEntry,
                _farmJungleEntry,
                _stackJungleEntry
            };

            _availableCamps = new List<JungleCamp>(JungleCamps.GetCamps);
            Log.Debug($"Init jungle camps! {_availableCamps.Count}");

            if (MyHero.Team == Team.Dire)
            {
                Log.Debug($"using radiant to dire creep lanes");
                _enemyCreepRouteTop = _radiantToDireTop;
                _enemyCreepRouteMid = _radiantToDireMid;
                _enemyCreepRouteBottom = _radiantToDireBot;
            }
            else
            {
                Log.Debug($"using reversed radiant to dire creep lanes");
                _enemyCreepRouteTop = new List<Vector3>(_radiantToDireTop);
                _enemyCreepRouteMid = new List<Vector3>(_radiantToDireMid);
                _enemyCreepRouteBottom = new List<Vector3>(_radiantToDireBot);

                _enemyCreepRouteTop.Reverse();
                _enemyCreepRouteMid.Reverse();
                _enemyCreepRouteBottom.Reverse();
            }
            Game.OnIngameUpdate += Game_OnIngameUpdate;
        }

       

        public override void OnClose()
        {
            Game.OnIngameUpdate -= Game_OnIngameUpdate;
            base.OnClose();
        }

        private async void Game_OnIngameUpdate(EventArgs args)
        {
            if (Utils.SleepCheck("zaioNagaFunctionWheelSleep"))
            {
                Utils.Sleep(500, "zaioNagaFunctionWheelSleep");
                // test function wheel items
                var gameTime = (int) Game.GameTime;
                var minute = gameTime / 60;
                var seconds = gameTime % 60;
                if (minute % 2 == 1)
                {
                    // refresh available jungle camps
                    if (minute != _lastAvailableCampRefreshMinute)
                    {
                        _availableCamps = new List<JungleCamp>(JungleCamps.GetCamps);
                        _lastAvailableCampRefreshMinute = minute;
                        Log.Debug($"Refreshing jungle camps! {_availableCamps.Count}");
                    }

                    //Log.Debug($"{minute % 2} == 1 => FALSE");
                    _stackJungleEntry.IsEnabled = false;
                    _stackJungleEntry.DisplayName = $"Stack Jungle ({91 - seconds:00}s)";

                }
                else
                {
                    var estimatedTime = GetEstimatedIllusionTime();
                    if (estimatedTime <= 60 - seconds)
                    {
                        //Log.Debug($"{60 - seconds} <= {estimatedTime} => FALSE");
                        _stackJungleEntry.IsEnabled = false;
                        var diff = 31 - seconds;
                        _stackJungleEntry.DisplayName = diff >= 0 ? $"Stack Jungle ({diff:00}s)" : "Stack Jungle";
                    }
                    else
                    {
                        //Log.Debug($"{60 - seconds} > {estimatedTime} => TRUE");
                        _stackJungleEntry.IsEnabled = true;
                        _stackJungleEntry.DisplayName = "Stack Jungle";
                    }
                }
                _farmJungleEntry.IsEnabled = _availableCamps.Any();
                _pushLaneEntry.IsEnabled = GetEstimatedIllusionTime() > 0;
                _mixedModeEntry.IsEnabled =  _farmJungleEntry.IsEnabled || _pushLaneEntry.IsEnabled ;
            }

            if (Utils.SleepCheck("zaioNagaPushLanes"))
            {
                Utils.Sleep(250, "zaioNagaPushLanes");
                _lanePusherIllusions = _lanePusherIllusions.Where(x => x.Unit.IsAlive).ToList();
                if (!_lanePusherIllusions.Any())
                {
                    return;
                }

                foreach (var lanePusher in _lanePusherIllusions)
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
                                             x.IsValid && x.IsAlive && x.Team != illusion.Team && x.IsSpawned &&
                                             x.IsRealUnit() && x.Distance2D(illusion) < 300);
                    if (!unitNearby)
                    {
                        var nextUnit =
                        ObjectManager.GetEntitiesParallel<Unit>()
                                     .FirstOrDefault(x =>
                                            x.IsValid && x.IsAlive && x.Team != illusion.Team && x.IsSpawned &&
                                            x.IsRealUnit() && x.Distance2D(illusion) < 300);
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
                                         .FirstOrDefault(x => x.IsValid && x.IsAlive && x.Team != illusion.Team && x.AttackTarget == illusion);
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
                                             .Any(x => x.IsValid && x.IsAlive && !x.IsIllusion && x.Team != MyHero.Team);
                            if (heroesNear)
                            {
                                // TODO: move to creep spawn
                            }
                        }
                    }
                }
            }

            if (Utils.SleepCheck("zaioNagaFarmJungle"))
            {
                Utils.Sleep(250, "zaioNagaFarmJungle");
                _farmJungleIllusions = _farmJungleIllusions.Where(x => x.Unit.IsAlive).ToList();
                if (!_farmJungleIllusions.Any())
                {
                    return;
                }
                foreach (var farmJungleIllusion in _farmJungleIllusions)
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
                                             x.IsValid && x.IsAlive && x.Team != illusion.Team && x.IsSpawned &&
                                             x.IsRealUnit() && x.Distance2D(illusion) < 500);
                    if (!unitNearby)
                    {
                        // remove from available camps list
                        _availableCamps.Remove(farmJungleIllusion.JungleCamp);
                        var jungleCamps = _availableCamps.Where(x => !x.Ancients).ToList();

                        // have to find another jungle spot (if possible without a illusion nearby)
                        var timeRemaining = illusion.GetIllusionRemainingTime();
                        var moveSpeed = (float) illusion.MovementSpeed;

                        var campsReachable =
                            jungleCamps.Where(x => x.CampPosition.Distance2D(illusion) / moveSpeed <= timeRemaining)
                                       .OrderBy(x => x.CampPosition.Distance2D(illusion));
                        if (campsReachable.Any())
                        {
                            var freeCamp =
                                campsReachable.FirstOrDefault(
                                    x => _farmJungleIllusions.All(y => y.JungleCamp.Id != x.Id));
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

        private void _functionWheelItem_ValueChanged(object sender, OnValueChangeEventArgs e)
        {
            if (e.GetNewValue<KeyBind>().Key != e.GetOldValue<KeyBind>().Key)
            {
                _functionWheel.KeyCode = _functionWheelItem.GetValue<KeyBind>().Key;
            }
        }

        private float GetEstimatedIllusionTime(bool respectMenuSettings = false)
        {
            var time = 0.0f;

            var manta = MyHero.GetItemById(ItemId.item_manta);
            if (manta != null && (!respectMenuSettings || manta.IsAbilityEnabled()) && manta.CanBeCasted() &&
                (!_illuAbility.CanBeCasted() || MyHero.Mana >= _illuAbility.ManaCost + manta.ManaCost))
            {
                time = Math.Max(time, manta.GetAbilityData("tooltip_illusion_duration"));
            }

            if ((!respectMenuSettings || _illuAbility.IsAbilityEnabled()) && _illuAbility.CanBeCasted())
            {
                time = Math.Max(time, _illuAbility.GetAbilityData("illusion_duration"));
            }

            var illusions = MyIllusions.ToList();
            if (illusions.Any())
            {
                time = Math.Max(time, illusions.Max(x => x.GetIllusionRemainingTime()));
            }
           
            return time;
        }

        private async Task<IEnumerable<Hero>> CreateGetIllusions(int count = int.MaxValue, bool respectMenuSettings = false)
        {
            var useManta = new Func<Task<bool>>(async () =>
                                                {
                                                    var manta = MyHero.GetItemById(ItemId.item_manta);
                                                    if (manta != null && (!respectMenuSettings || manta.IsAbilityEnabled()) && manta.CanBeCasted())
                                                    {
                                                        Log.Debug($"using manta");
                                                        manta.UseAbility();
                                                        await Await.Delay(150 + (int) Game.Ping);
                                                        await MyHero.WaitLossUnitStateAsync(UnitState.Flying |
                                                                                            UnitState.Unselectable);
                                                        return true;
                                                    }
                                                    return false;
                                                });
            var useIllu = new Func<Task<bool>>(async () =>
                                               {
                                                   if ((!respectMenuSettings || _illuAbility.IsAbilityEnabled()) && _illuAbility.CanBeCasted())
                                                   {
                                                       // Stunned, Invulnerable, MagicImmune, Unselectable, Flying, FakeAlly
                                                       _illuAbility.UseAbility();
                                                       var delay = GetAbilityDelay(_illuAbility);
                                                       Log.Debug($"using mirror image with delay {delay}");
                                                       await Await.Delay(delay);
                                                       await MyHero.WaitLossUnitStateAsync(UnitState.Flying |
                                                                                           UnitState.Unselectable);
                                                       await Await.Delay(1000);
                                                       return true;
                                                   }
                                                   return false;
                                               });
            if (count <= 2)
            {
                if (await useManta())
                {
                    return MyIllusions;
                }
            }
            else if (count == 3)
            {
                if (await useIllu())
                {
                    return MyIllusions;
                }
            }

            await useIllu();
            await useManta();

            return MyIllusions;
        }


        private async Task MixedMode()
        {
            var illusions = (await CreateGetIllusions()).ToList();
            Log.Debug($"mixed mode got {illusions.Count} illusions");
            if (illusions.Count <= 2 || !_availableCamps.Any())
                await PushLanes(illusions);
            else
            {
                var tasks = new List<Task>
                {
                    Task.Factory.StartNew(
                        async () =>
                        {
                            Log.Debug($"doing push lanes task");
                            await PushLanes(illusions.GetRange(0, 2));
                        }, // this will use current synchronization context
                        CancellationToken.None,
                        TaskCreationOptions.None,
                        TaskScheduler.FromCurrentSynchronizationContext()),
                    Task.Factory.StartNew(
                        async () =>
                        {
                            Log.Debug($"doing farm jungle");
                            await FarmJungle(illusions.GetRange(2, illusions.Count - 2));
                        }, // this will use current synchronization context
                        CancellationToken.None,
                        TaskCreationOptions.None,
                        TaskScheduler.FromCurrentSynchronizationContext())
                };

                Log.Debug($"waiting for mixed mode tasks");
                await Task.WhenAll(tasks);
                Log.Debug($"all mixed mode tasks done!");
            }
        }

        private async Task StackJungle()
        {
            var gameTime = (int) Math.Floor(Game.GameTime);

            var minute = gameTime / 60;
            if (minute % 2 == 1)
            {
                return;
            }

            var estimatedTime = GetEstimatedIllusionTime();
            var seconds = gameTime % 60;
            if (estimatedTime <= 60 - seconds)
            {
                return;
            }

            // disable radiance for stacking
            var radiance = MyHero.GetItemById(ItemId.item_radiance);
            if (radiance != null && !radiance.IsToggled)
            {
                Log.Debug($"disable radiance");
                radiance.ToggleAbility();
                await Await.Delay(ItemDelay);
            }

            // get illusions
            var illusions = (await CreateGetIllusions()).ToList();

            // reenable radiance
            if (radiance != null && radiance.IsToggled)
            {
                Log.Debug($"enable radiance");
                radiance.ToggleAbility();
                await Await.Delay(ItemDelay);
            }

            if (!illusions.Any())
            {
                Log.Debug($"didnt find any illusions");
                return;
            }

            var movespeed = illusions.First().MovementSpeed;
            var tasks = new List<Task>();
            var myCamps =
                _availableCamps.Where(x => x.Team == MyHero.Team && (ShouldStackAncients || !x.Ancients)).ToList();
            foreach (var illusion in illusions.OrderBy(x => x.GetIllusionRemainingTime()))
            {
                var timeRemaining = illusion.GetIllusionRemainingTime();
                foreach (var jungleCamp in myCamps.OrderBy(x => x.WaitPosition.Distance2D(illusion)))
                {
                    var distance = jungleCamp.WaitPosition.Distance2D(illusion);
                    var timeToReachWaitPos = distance / movespeed;

                    var secondsTillStack = 0;
                    if (seconds <= jungleCamp.StackTime)
                    {
                        secondsTillStack = (int) (jungleCamp.StackTime - seconds);
                    }
                    else
                    {
                        secondsTillStack = (int) (60 - seconds + jungleCamp.StackTime);
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

                    var stackTimeNeeded = secondsTillStack + (int) (61 - jungleCamp.StackTime);
                    // not enough time for the illusion to stack the closest camp
                    if (timeRemaining < stackTimeNeeded)
                    {
                        Log.Debug($"not enough time to stack {timeRemaining} <= {stackTimeNeeded}");
                        break;
                    }

                    Log.Debug($"add task");
                    var task = Task.Factory.StartNew(
                        async () =>
                        {
                            illusion.Move(jungleCamp.WaitPosition);
                            illusion.Hold(true);
                            var timeToCamp =
                                jungleCamp.CampPosition.Distance2D(jungleCamp.WaitPosition) / movespeed;
                            Log.Debug(
                                $"Stacking: {jungleCamp.Name} ({jungleCamp.Team}) -> start in {secondsTillStack} - {timeToCamp}s + {Game.GameTime % 60} = {(secondsTillStack - timeToCamp + gameTime) % 60}");
                            await Await.Delay(
                                (int) (Math.Floor(secondsTillStack - timeToCamp) * 1000.0f - Game.Ping));
                            illusion.Move(jungleCamp.CampPosition);
                            illusion.Move(jungleCamp.StackPosition, true);
                            await Await.Delay(50);
                        }, // this will use current synchronization context
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

        private async Task PushLanesFunc()
        {
            var illusions = (await CreateGetIllusions(/*wavePositions.Count*/)).ToList();
            await PushLanes(illusions);
        }
        private async Task PushLanes(List<Hero> illusions )
        {
            // we assume the illusions are at our position Kappa
            var wavePositions = GetCreepWavePositions(MyHero);

            var positions = new List<UnitOrPositionWithRoute>(wavePositions);
            foreach (var illusion in illusions.OrderBy(x => x.GetIllusionRemainingTime()))
            {
                var timeRemaining = illusion.GetIllusionRemainingTime();

                if (positions.Count == 0)
                {
                    positions = new List<UnitOrPositionWithRoute>(wavePositions);
                }

                foreach (var position in positions.OrderBy(x => x.UPos.Position.Distance2D(illusion)))
                {
                    illusion.Move(position.UPos.Position);
                    illusion.Attack(position.UPos.Position, true);
                    _lanePusherIllusions.Add(new LanePusher(illusion, position.UPos, position.Route));
                    positions.Remove(position);
                    await Await.Delay(50);
                    break;
                }
            }
        }

        private async Task FarmJungleFunc()
        {
            var illusions = (await CreateGetIllusions()).ToList();
            await FarmJungle(illusions);
        }

        private async Task FarmJungle(List<Hero> illusions)
        {
            
            if (!illusions.Any())
            {
                Log.Debug($"no illus to farm the jungle!");
                return;
            }

            var movespeed = (float) MyHero.MovementSpeed;
            var jungleCamps = _availableCamps.Where(x => !x.Ancients).ToList();

            Log.Debug($"Available camps: {jungleCamps.Count}");
            foreach (var illusion in illusions.OrderBy(x => x.GetIllusionRemainingTime()))
            {
                var timeRemaining = illusion.GetIllusionRemainingTime();
                // all camps are either cleared or already occupied by an illusion, so just send moar
                if (!jungleCamps.Any())
                {
                    jungleCamps = _availableCamps.Where(x => !x.Ancients).ToList();
                }

                foreach (var jungleCamp in jungleCamps.OrderBy(x => x.WaitPosition.Distance2D(illusion)))
                {
                    var distance = jungleCamp.WaitPosition.Distance2D(illusion);
                    var timeToReachWaitPos = distance / movespeed;
                    // our illusions doesn't hold long enough to reach the camp
                    if (timeRemaining < timeToReachWaitPos)
                    {
                        Log.Debug($"{jungleCamp.Name}: {timeRemaining} < {timeToReachWaitPos}");
                        continue;
                    }

                    Log.Debug($"Moving illusion to farm {jungleCamp.Name}");
                    _farmJungleIllusions.Add(new JungleFarmer(illusion, jungleCamp));
                    illusion.Move(jungleCamp.CampPosition);
                    illusion.Attack(jungleCamp.CampPosition, true);
                    await Await.Delay(50);
                    jungleCamps.Remove(jungleCamp);
                    break;
                }
            }
        }

        private UnitOrPosition GetCreepWavePosition(Hero illusion, List<Vector3> creepWalkingPositions, List<Creep> myCreeps, List<Creep> enemyCreeps  )
        {
            // newest wave
            var seconds = (int) Math.Ceiling(Game.GameTime) % 60;
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
                var intersectionTime = FindIntersection(illusion, start, creepWalkingPositions[1], creepSpeed);
                var intersection = start + dir * creepSpeed * intersectionTime;
                Log.Debug(
                    $"intersection = {creepWalkingPositions[0]} => {start} + {dir} * {intersectionTime} * {creepSpeed} = {intersection}");

                var nearestAllyCreep =
                    myCreeps.FirstOrDefault(
                        x =>
                            x.GetShortestDistance(creepWalkingPositions[0], creepWalkingPositions[1]) <= intersection.Distance2D(creepWalkingPositions[0]));
                if (nearestAllyCreep != null)
                {
                    predictionTime = -nextWave;
                    Log.Debug($"allied creep is closer, so cutting next wave {predictionTime}");
                    start = creepWalkingPositions[0] + dir * creepSpeed * predictionTime;
                    intersectionTime = FindIntersection(illusion, start, creepWalkingPositions[1], creepSpeed);
                    intersection = start + dir * creepSpeed * intersectionTime;
                    Log.Debug(
                    $"new intersection = {creepWalkingPositions[0]} => {start} + {dir} * {intersectionTime} * {creepSpeed} = {intersection}");
                }

                var reversed = new List<Vector3>(creepWalkingPositions);
                reversed.Reverse();
                
                var nearestCreep =
                  enemyCreeps.FirstOrDefault(
                      x => x.GetShortestDistance(reversed)  <= intersection.Distance2D(creepWalkingPositions[0]));

                //var nearestCreep =
                //   enemyCreeps.FirstOrDefault(
                //       x => 
                //           x.GetShortestDistance(creepWalkingPositions[0], creepWalkingPositions[1]) <= intersection.Distance2D(creepWalkingPositions[0]));
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
                             .Where(x => x.IsValid && x.IsAlive && x.Team != MyHero.Team && x.IsSpawned).ToList();
            var myCreeps =
                ObjectManager.GetEntitiesParallel<Creep>()
                             .Where(x => x.IsValid && x.IsAlive && x.Team != MyHero.Team && x.IsSpawned).ToList();

            var result = new List<UnitOrPositionWithRoute>();
            try
            {
                var pos = GetCreepWavePosition(illusion, _enemyCreepRouteTop, myCreeps, enemyCreeps);
                result.Add(new UnitOrPositionWithRoute(pos, _enemyCreepRouteTop));
            }
            catch (Exception)
            {
                // ignored
            }
            try
            {
                var pos = GetCreepWavePosition(illusion, _enemyCreepRouteMid, myCreeps, enemyCreeps);
                result.Add(new UnitOrPositionWithRoute(pos, _enemyCreepRouteMid));
            }
            catch (Exception)
            {
                // ignored
            }
            try
            {
                var pos = GetCreepWavePosition(illusion, _enemyCreepRouteBottom, myCreeps, enemyCreeps);
                result.Add(new UnitOrPositionWithRoute(pos, _enemyCreepRouteBottom));
            }
            catch (Exception)
            {
                // ignored
            }
            return result;
        }

        private float FindIntersection(Unit unit, Vector3 start, Vector3 end, float speed = 325)
        {
            // sx^2 + sy^2 + (2 * sx * vx + 2 * sy * vy) * t + (vx^2 + vy^2 - v2^2) * t^2 = 0
            var pos = unit.NetworkPosition - start;
            var distance = pos.Length();

            var v = (end - start).Normalized() * speed;
            var mySpeed = (float) (unit.MovementSpeed * unit.MovementSpeed);

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

            discr = (float) Math.Sqrt(discr);
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

            if (_ripTideAbility.IsKillstealAbilityEnabled() && _ripTideAbility.CanBeCasted())
            {
                var damage = (float) _ripTideAbility.GetDamage(_ripTideAbility.Level - 1);
                damage *= GetSpellAmp();

                var radius = _ripTideAbility.GetAbilityData("radius");
                var illusions = MyIllusions;
                var enemy =
                    ObjectManager.GetEntitiesParallel<Hero>()
                                 .FirstOrDefault(
                                     x =>
                                         x.IsAlive && x.Team != MyHero.Team && !x.IsIllusion &&
                                         _ripTideAbility.CanBeCasted(x) && illusions.Any(y => y.Distance2D(x) < radius)
                                         && !x.IsMagicImmune() &&
                                         x.Health < damage * (1 - x.MagicResistance()) && !x.CantBeAttacked() &&
                                         !x.CantBeKilled());
                if (enemy != null)
                {
                    Log.Debug(
                        $"use riptide because enough damage {enemy.Health} <= {damage * (1 - enemy.MagicResistance())} ");
                    _ripTideAbility.UseAbility();
                    await Await.Delay(GetAbilityDelay(_ripTideAbility));
                    return true;
                }
            }

            return false;
        }


        public override async Task ExecuteComboAsync(Unit target, CancellationToken tk = new CancellationToken())
        {
            await HasNoLinkens(target, tk);
            await UseItems(target, tk);

            // make him disabled
            await DisableEnemy(target, tk);

            var illusions = (await CreateGetIllusions(int.MaxValue,true)).Where(x => x.Distance2D(MyHero) < 1000).ToList();

            if (_ripTideAbility.IsAbilityEnabled() && _ripTideAbility.CanBeCasted(target) &&
                (_ripTideAbility.CanHit(target) ||
                 illusions.Any(x => x.Distance2D(target) < _ripTideAbility.GetAbilityData("radius"))))
            {
                Log.Debug($"Using riptide on target");
                _ripTideAbility.UseAbility();
                await Await.Delay(GetAbilityDelay(_ripTideAbility), tk);
            }

            if (illusions.Any())
            {
                // move illusions to all enemies if possible, if none are available anymore, then to the target
                var enemies =
                    ObjectManager.GetEntitiesParallel<Hero>()
                                 .Where(x => x.IsValid && x.IsAlive && !x.IsIllusion && x.Team != MyHero.Team).ToList();
                foreach (var illusion in illusions)
                {
                    var enemy = enemies.OrderBy(x => x.Distance2D(illusion)).FirstOrDefault();
                    if (enemy == null)
                        illusion.Attack(target);
                    else
                    {
                        enemies.Remove(enemy);
                        illusion.Attack(enemy);
                    }
                    await Await.Delay(50, tk);
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
    }
}