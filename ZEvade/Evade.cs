using System;
using System.Collections.Generic;
using Ensage;
using Ensage.Common;
using Evade.EvadeData;
using Evade.Obstacles;
// ReSharper disable InconsistentNaming

namespace Evade
{
    using System.Linq;

    using Ensage.Common.Extensions;
    using Ensage.Common.Extensions.SharpDX;
    using Ensage.Common.Objects.UtilityObjects;

    using SharpDX;

    using static Helpers;

    class Evade
    {
        private readonly NavMeshPathfinding pathfinding = new NavMeshPathfinding();
        private readonly List<SupportedModifier> supportedModifiers = new List<SupportedModifier>
        {
            new SupportedModifier("modifier_invoker_sun_strike", ClassID.CDOTA_Ability_Invoker_SunStrike,true),
            new SupportedModifier("modifier_lina_light_strike_array", ClassID.CDOTA_Ability_Lina_LightStrikeArray, true),
            new SupportedModifier("modifier_kunkka_torrent_thinker", ClassID.CDOTA_Ability_Kunkka_Torrent, true),
            new SupportedModifier("modifier_enigma_black_hole_thinker", ClassID.CDOTA_Ability_Enigma_BlackHole, true),
            new SupportedModifier("modifier_bloodseeker_bloodbath_thinker", ClassID.CDOTA_Ability_Bloodseeker_Bloodbath, true),
            //new SupportedModifier("modifier_faceless_void_chronosphere_speed", ClassID.CDOTA_Ability_FacelessVoid_Chronosphere, false),
        };
        private readonly List<IObstacle> obstacles = new List<IObstacle>();
        private Dictionary<Unit, ObstacleUnit> obstacleUnits = new Dictionary<Unit, ObstacleUnit>();
        private List<ObstacleParticle> obstacleParticles = new List<ObstacleParticle>();

        private readonly Hero myHero = ObjectManager.LocalHero;

        private bool isPositionKnown;
        private Vector3 movePosition;
        private bool pathingMoved;
        private bool notMovedYetToPath;
        private List<Vector3> forcePath, debugPath;

        private List<uint> obstaclesIDs = new List<uint>(), obstaclesPredictedIDs = new List<uint>();

        private readonly Orbwalker orbwalker;

        // menu controlled
        private bool autoEvade;
        private bool evadePressed;
        private bool orbwalkerPressed;
        private bool evadeMovePressed;

        // particle effects
        private ParticleEffect testParticle;
        private Unit miranaArrow;


        public Evade()
        {
            foreach (var unit in ObjectManager.GetEntities<Unit>().Where(x => x.IsAlive && !Equals(x, myHero) && x.NetworkPosition != Vector3.Zero))
            {
                var id = pathfinding.AddObstacle(unit.NetworkPosition, unit.HullRadius);
                obstacleUnits.Add(unit, new ObstacleUnit(id, unit));
            }

            orbwalker = new Orbwalker(myHero);

            Game.OnIngameUpdate += OnEvadePathUpdate;
            Game.OnIngameUpdate += OnParticleObstaclesUpdate;
            Unit.OnModifierAdded += OnObstacleModifierAdded;
            Unit.OnModifierRemoved += OnObstacleModifierRemoved;
            Entity.OnFloatPropertyChange += Entity_OnFloatPropertyChange;
            //Entity.OnBoolPropertyChange += Entity_OnBoolPropertyChange;
            Entity.OnInt32PropertyChange += Entity_OnInt32PropertyChange;
            //Entity.OnInt64PropertyChange += Entity_OnInt64PropertyChange;
            Entity.OnParticleEffectAdded += OnObstacleParticleAdded;
            Player.OnExecuteOrder += Player_OnExecuteOrder;
            ObjectManager.OnAddEntity += OnObstacleUnitAdded;
            ObjectManager.OnRemoveEntity += OnObstacleUnitRemoved;

            autoEvade = Program.Menu.IsAutoEvadeEnabled;
            Program.Menu.AutoEvadeChanged += (sender, args) =>
                { autoEvade = args.Value; };
            Program.Menu.EvadePressed += (sender, args) =>
                { evadePressed = args.Value; };
            Program.Menu.OrbwalkerPressed += (sender, args) =>
                { orbwalkerPressed = args.Value; };
            Program.Menu.EvadeMovePressed += (sender, args) =>
                { evadeMovePressed = args.Value; };

            // Debug
            if (Program.Menu.IsDebugDrawEnabled)
                Drawing.OnDraw += Drawing_OnDebugDraw;
            Program.Menu.DebugDrawChanged += (sender, args) =>
                {
                    if (args.Value) Drawing.OnDraw += Drawing_OnDebugDraw;
                    else Drawing.OnDraw -= Drawing_OnDebugDraw;
                };

            if (Program.Menu.IsDebugDrawMapEnabled)
                Drawing.OnDraw += OnDebugDrawOnMap;
            Program.Menu.DebugDrawMapChanged += (sender, args) =>
            {
                if (args.Value) Drawing.OnDraw += OnDebugDrawOnMap;
                else Drawing.OnDraw -= OnDebugDrawOnMap;
            };

            if (Program.Menu.IsBotAutoAttackEnabled)
                Game.OnIngameUpdate += Game_OnBotAttackTick;
            Program.Menu.BotAutoAttackChanged += (sender, args) =>
                {
                    if (args.Value) Game.OnIngameUpdate += Game_OnBotAttackTick;
                    else Game.OnIngameUpdate -= Game_OnBotAttackTick;
                };
        }


        private void OnParticleObstaclesUpdate(EventArgs args)
        {
            if (Game.IsPaused)
                return;

            var updatedList = new List<ObstacleParticle>();
            foreach (ObstacleParticle obstacleParticle in obstacleParticles)
            {
                //Console.WriteLine("{0} - {1} - {2} - {3} - {4} - {5}", obstacleParticle.ParticleEffect.GetControlPoint(0)
                //    , obstacleParticle.ParticleEffect.GetControlPoint(1)
                //    , obstacleParticle.ParticleEffect.GetControlPoint(2)
                //    , obstacleParticle.ParticleEffect.GetControlPoint(3)
                //    , obstacleParticle.ParticleEffect.GetControlPoint(4)
                //    , obstacleParticle.ParticleEffect.GetControlPoint(5));
                if (obstacleParticle.IsValid)
                {
                    if (obstacleParticle.IsLine)
                    {
                        pathfinding.UpdateObstacle(
                            obstacleParticle.ID,
                            obstacleParticle.UseCurrentPosition ? obstacleParticle.CurrentPosition : obstacleParticle.Position,
                            obstacleParticle.EndPosition);
                    }
                    else
                    {
                        pathfinding.UpdateObstacle(obstacleParticle.ID, obstacleParticle.Position);
                    }

                    updatedList.Add(obstacleParticle);
                }
                else
                {
                    if (forcePath != null && (obstaclesIDs.Contains(obstacleParticle.ID) || obstaclesPredictedIDs.Contains(obstacleParticle.ID)) )
                    {
                        forcePath = null;
                        debugPath = null;
                        myHero.Move(movePosition);
                    }
                    pathfinding.RemoveObstacle(obstacleParticle.ID);
                }
            }
            obstacleParticles = updatedList;
        }

        private void OnObstacleParticleAdded(Entity sender, ParticleEffectAddedEventArgs args)
        {
            //if (sender.Team == myHero.Team) return;

            // Pudge
            if (args.Name == "particles/units/heroes/hero_pudge/pudge_meathook.vpcf")
                obstacleParticles.Add(
                    new Obstacles.Particles.ObstacleParticleHook(pathfinding, sender, args.ParticleEffect));
            // Invoker
            else if (args.Name == "particles/units/heroes/hero_invoker/invoker_emp.vpcf")
                obstacleParticles.Add(
                    new Obstacles.Particles.ObstacleParticleEMP(pathfinding, sender, args.ParticleEffect));
            // Death Prophet
            else if (args.Name == "particles/units/heroes/hero_death_prophet/death_prophet_carrion_swarm.vpcf")
                obstacleParticles.Add(
                    new Obstacles.Particles.ObstacleParticleCarrionSwarm(pathfinding, sender, args.ParticleEffect));
            // TimberSaw
            else if (args.Name == "particles/units/heroes/hero_shredder/shredder_timberchain.vpcf")
                obstacleParticles.Add(
                    new Obstacles.Particles.ObstacleParticleTimberChain(pathfinding, sender, args.ParticleEffect));
            else if (args.Name == "particles/units/heroes/hero_shredder/shredder_chakram_stay.vpcf")
                obstacleParticles.Add(
                    new Obstacles.Particles.ObstacleParticleChakram(pathfinding, sender, args.ParticleEffect));
            // Bloodcyka
            else if (args.Name == "particles/units/heroes/hero_bloodseeker/bloodseeker_bloodritual_ring.vpcf")
                obstacleParticles.Add(
                    new Obstacles.Particles.ObstacleParticleBloodRitual(pathfinding, sender, args.ParticleEffect));
            // Elder Titan
            else if (args.Name == "particles/units/heroes/hero_elder_titan/elder_titan_echo_stomp_cast_combined.vpcf"
                || args.Name == "particles/units/heroes/hero_elder_titan/elder_titan_echo_stomp_cast_spirit.vpcf"
                || args.Name == "particles/units/heroes/hero_elder_titan/elder_titan_echo_stomp_cast_titan.vpcf")
                obstacleParticles.Add(
                    new Obstacles.Particles.ObstacleParticleEchoStomp(pathfinding, sender, args.ParticleEffect));
            else if (args.Name == "particles/units/heroes/hero_elder_titan/elder_titan_earth_splitter.vpcf")
                obstacleParticles.Add(
                    new Obstacles.Particles.ObstacleParticleEarthSplitter(pathfinding, sender, args.ParticleEffect));
            // Gyro
            else if (args.Name == "particles/units/heroes/hero_gyrocopter/gyro_calldown_first.vpcf")
                obstacleParticles.Add(
                    new Obstacles.Particles.ObstacleParticleCallDown(pathfinding, sender, args.ParticleEffect, true));
            else if (args.Name == "particles/units/heroes/hero_gyrocopter/gyro_calldown_second.vpcf")
                obstacleParticles.Add(
                    new Obstacles.Particles.ObstacleParticleCallDown(pathfinding, sender, args.ParticleEffect, false));
            // Clockwerk 
            else if (args.Name == "particles/units/heroes/hero_rattletrap/rattletrap_hookshot.vpcf")
                obstacleParticles.Add(
                    new Obstacles.Particles.ObstacleParticleHookShot(pathfinding, sender, args.ParticleEffect));

            /*
            // Windrunner -> dodge by npc
            //else if (args.Name == "particles/units/heroes/hero_windrunner/windrunner_spell_powershot_channel.vpcf")
            //    obstacleParticles.Add(new Obstacles.Particles.ObstacleParticlePowershot(pathfinding,sender,args.ParticleEffect));
            // Lion -> attached to stick and no correct rotation
            //else if (args.Name == "particles/units/heroes/hero_lion/lion_spell_impale_staff.vpcf")
            //    obstacleParticles.Add(
            //        new Obstacles.Particles.ObstacleParticleEarthSpike(pathfinding, sender, args.ParticleEffect));
            //// Pheonix
            //else if (args.Name == "particles/units/heroes/hero_phoenix/phoenix_fire_spirit_launch.vpcf")
            //    obstacleParticles.Add(
            //        new Obstacles.Particles.ObstacleParticleFireSpirit(pathfinding, sender, args.ParticleEffect));
            //// Ancient
            //else if(args.Name == "particles/units/heroes/hero_ancient_apparition/ancient_apparition_ice_blast_final.vpcf")
            //    obstacleParticles.Add(new Obstacles.Particles.ObstacleParticleIceBlast(pathfinding,sender,args.ParticleEffect));


             * Invoker
             * particles/units/heroes/hero_invoker/invoker_chaos_meteor_fly.vpcf
             * 
    
             * Clock
             * particles/units/heroes/hero_rattletrap/rattletrap_cog_deploy.vpcf vs particles/units/heroes/hero_rattletrap/rattletrap_cog_ambient.vpc
             * 
             * Ancient
             * particles/units/heroes/hero_ancient_apparition/ancient_apparition_ice_blast_marker.vpcf
                particles/units/heroes/hero_ancient_apparition/ancient_apparition_ice_blast_final.vpcf
                particles/units/heroes/hero_ancient_apparition/ancient_apparition_ice_blast_debuff.vpcf
                particles/status_fx/status_effect_iceblast.vpcf
                particles/units/heroes/hero_ancient_apparition/ancient_apparition_ice_blast_explode.vpcf

       
            Veno
            particles/units/heroes/hero_venomancer/venomancer_venomous_gale_mouth.vpcf

            particles/units/heroes/hero_venomancer/venomancer_poison_nova.vpcf
            particles/units/heroes/hero_venomancer/venomancer_poison_nova_cast.vpcf

*/
            //if (args.Name == "particles/units/heroes/hero_pudge/pudge_meathook.vpcf")
            //{
            //    hook = args.ParticleEffect;
            //}
            else if (args.Name == "particles/units/heroes/hero_rattletrap/rattletrap_hookshot.vpcf aa")
            {
                testParticle = args.ParticleEffect;
            }
            //else
            //{
            //    Debugging.WriteLine(args.Name);
            //}
        }

        private void OnObstacleUnitRemoved(EntityEventArgs args)
        {
            var unit = args.Entity as Unit;
            if (unit != null)
            {
                obstacleUnits.Remove(unit);
            }
        }

        private void OnObstacleUnitAdded(EntityEventArgs args)
        {
            var unit = args.Entity as Unit;
            if (unit != null)
            {
                miranaArrow = unit;
                var id = pathfinding.AddObstacle(unit.NetworkPosition, unit.HullRadius);
                obstacleUnits.Add(unit, new ObstacleUnit(id, unit));
            }
        }

        private void Entity_OnBoolPropertyChange(Entity sender, BoolPropertyChangeEventArgs args)
        {
            if (!sender.Equals(myHero)) return;
            if (args.PropertyName == "m_bIsMoving")
            {
                if (!args.NewValue)
                {
                    //isPositionKnown = false;
                    Debugging.WriteLine("Stopping and not known");
                }
                // get some position where we can evade to
                else if (!isPositionKnown)
                {
                    movePosition = GetValidMovePosition();
                }
            }
        }

        private void Player_OnExecuteOrder(Player sender, ExecuteOrderEventArgs args)
        {
            if (!args.Entities.Contains(myHero))
                return;
            switch (args.Order)
            {
                case Order.AbilityLocation:
                case Order.MoveLocation:
                case Order.AttackLocation:
                case Order.DropItem:
                    movePosition = args.TargetPosition;
                    isPositionKnown = true;
                    notMovedYetToPath = true;
                    break;
                case Order.AbilityTarget:
                case Order.MoveTarget:
                case Order.AbilityTargetRune:
                case Order.TransferItem:
                    movePosition = args.Target.Position;
                    isPositionKnown = true;
                    notMovedYetToPath = true;
                    break;

                case Order.AbilityTargetTree:
                case Order.Stop:
                case Order.Hold:
                    notMovedYetToPath = true;
                    isPositionKnown = false;
                    break;


            }
        }

        private void Entity_OnInt64PropertyChange(Entity sender, Int64PropertyChangeEventArgs args)
        {
            if (!(sender is Hero)) return;
            Debugging.WriteLine("64 - {0}: {1}", sender.Name, args.PropertyName);
        }

        private void Entity_OnInt32PropertyChange(Entity sender, Int32PropertyChangeEventArgs args)
        {
            var unit = sender as Unit;
            ObstacleUnit obstacleUnit;
            if (unit != null && obstacleUnits.TryGetValue(unit, out obstacleUnit))
            {
                if (args.PropertyName == "m_iTaggedAsVisibleByTeam")
                {
                    // "remove" obstacle if unit not visible
                    pathfinding.UpdateObstacle(
                        obstacleUnit.ID,
                        args.NewValue == 0x1E
                            ? unit.NetworkPosition
                            : new Vector3(float.MinValue, float.MinValue, float.MinValue));
                }
                else if (args.PropertyName == "m_iHealth" && args.NewValue <= 0)
                {
                    pathfinding.RemoveObstacle(obstacleUnit.ID);
                    obstacleUnits.Remove(unit);
                }
            }
        }

        private void Entity_OnFloatPropertyChange(Entity sender, FloatPropertyChangeEventArgs args)
        {
            var unit = sender as Unit;
            ObstacleUnit obstacleUnit;
            if (unit != null && obstacleUnits.TryGetValue(unit, out obstacleUnit))
            {
                var newPos = obstacleUnit.Unit.NetworkPosition;
                //switch (args.PropertyName)
                //{
                //    case "m_vecX":
                //        newPos.X = args.NewValue;
                //        break;
                //    case "m_vecY":
                //        newPos.Y = args.NewValue;
                //        break;
                //    case "m_vecZ":
                //        newPos.Z = args.NewValue;
                //        break;
                //    default:
                //        return;
                //}
                //Debugging.WriteLine("<{3}> {0}: {1} - {2}",unit.Name,unit.NetworkPosition,newPos,args.PropertyName);
                pathfinding.UpdateObstacle(obstacleUnit.ID, newPos);
            }
            //if (Equals(myHero, unit))
            //{
            //    if ((movePosition - myHero.Position).LengthSquared() < 100*100)
            //    {
            //        isPositionKnown = false;
            //    }
            //}
        }

        private void OnObstacleModifierRemoved(Unit sender, ModifierChangedEventArgs args)
        {
            var entry = obstacles.Find(x => x.GetHandle() == sender.Handle && x is ObstacleModifier) as ObstacleModifier;
            // test if it's a known obstacle which is getting removed
            if (entry == null) return;

            if (obstaclesIDs.Contains(entry.ID) || obstaclesPredictedIDs.Contains(entry.ID))
            {
                forcePath = null;
                debugPath = null;
                myHero.Move(movePosition);
            }
            pathfinding.RemoveObstacle(entry.ID);
            obstacles.Remove(entry);
        }

        private void OnObstacleModifierAdded(Unit sender, ModifierChangedEventArgs args)
        {
            // Debugging.WriteLine("Added {0}: {1} {2} {3}", sender.Handle, args.Modifier.Name, args.Modifier.RemainingTime, args.Modifier.Duration);

            if (!(sender is Hero) && sender.Owner == null) return;

            // test if it's an modifier we want to avoid
            var supportedMod = supportedModifiers.Find(x => x.ModifierName == args.Modifier.Name);
            if (supportedMod == null) return;

            var caster = sender is Hero ? sender : sender.Owner;

            if (supportedMod.EnemyTeamOnly && (!supportedMod.EnemyTeamOnly || caster.Team == myHero.Team)) return;


            var ability = caster.Spellbook().Spells.FirstOrDefault(x => x.ClassID == supportedMod.AbilityClassID);
            if (ability == null) return;

            // TODO test if ability actually does damage? magic immune? pure?

            var id = pathfinding.AddObstacle(sender.NetworkPosition, ability.GetRadius(ability.Name));
            obstacles.Add(new ObstacleModifier(id, sender, args.Modifier));
        }




        private void OnEvadePathUpdate(EventArgs args)
        {
            if (Utils.SleepCheck("unitcleanup"))
            {
                Utils.Sleep(1000 * 10, "unitcleanup");
                Dictionary<Unit, ObstacleUnit> updatedList = new Dictionary<Unit, ObstacleUnit>();
                foreach (var unit in obstacleUnits)
                {
                    if (unit.Key.IsValid && unit.Key.Health > 0) updatedList.Add(unit.Key, unit.Value);
                    else pathfinding.RemoveObstacle(unit.Value.ID);
                }
                obstacleUnits = updatedList;
            }

            if (Game.IsPaused)
                return;

            if ((autoEvade || evadePressed || orbwalkerPressed || evadeMovePressed) && Utils.SleepCheck("evadeupdate"))
            {
                Utils.Sleep(125, "evadeupdate");

                pathfinding.UpdateNavMesh();

                obstaclesIDs = pathfinding.GetIntersectingObstacleIDs(myHero.Position, myHero.HullRadius).ToList();
                obstaclesPredictedIDs = pathfinding.GetIntersectingObstacleIDs(myHero.BasePredict(350), myHero.HullRadius).ToList();

                if (orbwalkerPressed || evadeMovePressed)
                {
                    movePosition = Game.MousePosition;
                    isPositionKnown = true;
                }

                bool tried = false;
                bool completed = false;
                List<Vector3> path = null;
                // test if we're currently on an obstacle
                if (obstaclesIDs.Any())
                {
                    tried = true;
                    movePosition = GetValidMovePosition(movePosition);

                    float turnRate = myHero.GetTurnRate();

                    List<IObstacle> myList = new List<IObstacle>();
                    if( obstacles.Any() )
                        myList = myList.Concat(obstacles.Where(x => obstaclesIDs.Any(y => y == x.ID))).ToList();
                    if( obstacleParticles.Any() )
                        myList = myList.Concat(obstacleParticles.Where(x => obstaclesIDs.Any(y => y == x.ID))).ToList();

                    float timeLeft = TimeLeftFromObstacles(myList);

                    Debugging.WriteLine("Using obstacle algo with time: {0} and distance: {1} and posDiff: {2}", timeLeft, myHero.MovementSpeed * timeLeft / 1000.0f,(movePosition - myHero.Position).Length());

                    path = pathfinding.CalculatePathFromObstacle(
                        myHero.Position,
                        movePosition,
                        myHero.RotationRad,
                        myHero.MovementSpeed,
                        turnRate,
                        timeLeft,
                        true,
                        out completed).ToList();

                    if (Program.Menu.IsDebugDrawPathEnabled)
                        debugPath = pathfinding.CalculatePathFromObstacle(
                            myHero.Position,
                            movePosition,
                            myHero.RotationRad,
                            myHero.MovementSpeed,
                            turnRate,
                            timeLeft,
                            true,
                            out completed).ToList();

                    if (completed)
                    {
                        float maxDistance = (movePosition - myHero.Position).Length() * 4;
                        var toTarget = pathfinding.CalculateLongPath(
                        path.Last(),
                        movePosition,
                        maxDistance,
                        true,
                        out completed).ToList();

                        if (completed && toTarget.Any()) path = path.Concat(toTarget).ToList();
                    }
                }
                else if (obstaclesPredictedIDs.Any())
                {
                    tried = true;
                    movePosition = GetValidMovePosition(movePosition);
                    float maxDistance = (movePosition - myHero.Position).Length() * 4;
                    Debugging.WriteLine("Using normal algo with distance: {0}", maxDistance);
                    path = pathfinding.CalculateLongPath(
                        myHero.Position,
                        movePosition,
                        maxDistance,
                        true,
                        out completed).ToList();
                    if( Program.Menu.IsDebugDrawPathEnabled)
                        debugPath = pathfinding.CalculateLongPath(
                            myHero.Position,
                            movePosition,
                            maxDistance,
                            false,
                            out completed).ToList();
                }
                else
                {
                    forcePath = null;
                    debugPath = null;
                }
                if (tried)
                {
                    // if we tried to find a path...
                    if (completed && path.Any())
                    {
                        forcePath = path;
                        notMovedYetToPath = true;

                        // test if we guessed a position to go
                        if (!isPositionKnown)
                            pathingMoved = true;
                    }
                    else
                    {
                        // maybe we are at the spawn and enemy exactly tried to attack there

                        if (!obstaclesIDs.Any())
                        {
                            myHero.Stop();
                            Debugging.WriteLine("stopping to be safe");
                        }
                        else
                        {
                            Debugging.WriteLine("can't run ! panic!! -> use spells");
                        }
                    }
                }

            }

            if (notMovedYetToPath && forcePath != null && forcePath.Any())
            {
                myHero.Move(forcePath[0]);
                for (int i = 1; i < Math.Min(forcePath.Count, 32); ++i)
                {
                    myHero.Move(forcePath[i], true);
                }
                myHero.Move(movePosition,true);
                notMovedYetToPath = false;
            }
            else if (pathingMoved && forcePath == null)
            {
                debugPath = null;
                pathingMoved = false;
                myHero.Stop();
                Debugging.WriteLine("STOP {0} - {1}", forcePath != null, notMovedYetToPath);
            }
            if ((evadeMovePressed || orbwalkerPressed) && (forcePath == null || !pathingMoved))
            {
                var target = evadeMovePressed ? null : TargetSelector.ClosestToMouse(ObjectManager.LocalHero);
                if (target != null)
                    orbwalker.OrbwalkOn(target);
                else
                    myHero.Move(Game.MousePosition);

            }

        }

        Vector3 GetValidMovePosition()
        {

            var fountain =
                        ObjectManager.GetEntities<Building>()
                            .FirstOrDefault(x => x.ClassID == ClassID.CDOTA_Unit_Fountain && x.Team == myHero.Team);

            return fountain?.Position ?? Game.MousePosition;
        }
        Vector3 GetValidMovePosition(Vector3 currentTarget)
        {

            var dir = (currentTarget - myHero.Position);
            dir.Normalize();

            // try to walk further if position is not valid
            foreach (var possibleTarget in GetPositionsWithDistanceAndAngle(currentTarget, dir, pathfinding.CellSize, 4))
            {
                var flags = pathfinding.GetCellFlags(possibleTarget);
                if (IsWalkableCell(flags)) return possibleTarget;
            }
            // no valid position found for our old movement target, so try to find a new one
            foreach (var possibleTarget in GetPositionsWithDistanceAndAngle(GetValidMovePosition(), dir, pathfinding.CellSize, 4))
            {
                var flags = pathfinding.GetCellFlags(possibleTarget);
                if (IsWalkableCell(flags)) return possibleTarget;
            }

            // well no luck?
            Debugging.WriteLine("Couldn't find a valid target position which is safe :(");
            return GetValidMovePosition();
        }

        #region DebugSection

        private void Game_OnBotAttackTick(EventArgs args)
        {
            if (!Utils.SleepCheck("onBotAttack")) return;
            Utils.Sleep(125, "onBotAttack");

            const int RndDistance = 750;
            var rnd = new Random();
            foreach (var enemy in ObjectManager.GetEntities<Hero>().Where(x => !Equals(x, myHero)).ToList())
            {
                var validSpell =
                    enemy.Spellbook.Spells.FirstOrDefault(
                        x => supportedModifiers.Any(y => y.AbilityClassID == x.ClassID));
                if (validSpell != null && validSpell.CanBeCasted())
                {
                    var spellSleepName = $"onBotAttack_{enemy.Handle}_{validSpell.ClassID}";
                    if (!Utils.SleepCheck(spellSleepName)) continue;
                    Utils.Sleep(1000 + rnd.Next(2000), spellSleepName);

                    var targetPos = myHero.Position;
                    targetPos.X += rnd.NextFloat(-RndDistance, +RndDistance);
                    targetPos.Y += rnd.NextFloat(-RndDistance, +RndDistance);
                    validSpell.UseAbility(targetPos);
                }
                var hookSpell =
                    enemy.Spellbook.Spells.FirstOrDefault(
                        x => x.ClassID == ClassID.CDOTA_Ability_Pudge_MeatHook);
                if (hookSpell != null && hookSpell.CanBeCasted())
                {
                    var spellSleepName = $"onBotAttack_{enemy.Handle}_{hookSpell.ClassID}";
                    if (!Utils.SleepCheck(spellSleepName)) continue;
                    Utils.Sleep(3000 + rnd.Next(3000), spellSleepName);

                    var targetPos = myHero.BasePredict(600);
                    hookSpell.UseAbility(targetPos);
                }

            }
        }

        private void Drawing_OnDebugDraw(EventArgs args)
        {
            var hero = ObjectManager.LocalHero;
            if (hero == null)
                return;

            //Console.WriteLine("> TurnRate: {0}",myHero.GetTurnRate());
            //foreach (var modifiers in myHero.Modifiers)
            //{
            //    Console.WriteLine("{0}: {1}",modifiers.Name, modifiers.StackCount);
            //}

            //foreach (ObstacleUnit unit in obstacleUnits.Values)
            //{
            //    if( myHero.Distance2D(unit.Unit) < 1000)
            //    Debugging.WriteLine(unit.Unit.Name);   
            //}
            //foreach (ObstacleModifier unit in obstacles)
            //{
            //    if (myHero.Distance2D(unit.Owner) < 1000)
            //        Debugging.WriteLine(unit.Owner.Name);
            //}

            var heroPos = hero.Position;
            // Drawing the navmesh grid
            const int CellCount = 40;
            for (int i = 0; i < CellCount; ++i)
            {
                for (int j = 0; j < CellCount; ++j)
                {
                    Vector2 p;
                    p.X = pathfinding.CellSize * (i - CellCount / 2) + heroPos.X;
                    p.Y = pathfinding.CellSize * (j - CellCount / 2) + heroPos.Y;

                    Color c;
                    if (i == CellCount / 2 && j == CellCount / 2)
                        c = Color.Blue;
                    else
                    {
                        bool isFlying = hero.MoveCapability == MoveCapability.Fly || hero.IsUnitState(UnitState.Flying);
                        var flag = pathfinding.GetCellFlags(p);
                        if (!isFlying && flag.HasFlag(NavMeshCellFlags.Walkable))
                        {
                            c = flag.HasFlag(NavMeshCellFlags.Tree) ? Color.Purple : Color.Green;
                            if (flag.HasFlag(NavMeshCellFlags.GridFlagObstacle))
                                c = Color.Pink;
                        }

                        else if (isFlying &&
                                 !flag.HasFlag(NavMeshCellFlags.MovementBlocker))
                            c = Color.Green;
                        else
                            c = Color.Red;
                    }
                    Drawing.DrawRect(new Vector2(i * 10, 50 + (CellCount - j - 1) * 10), new Vector2(9, 9), c, false);
                }
            }
            // Drawing the path
            if (Program.Menu.IsDebugDrawPathEnabled && debugPath != null )
            {
                int heroCellX, heroCellY;
                pathfinding.GetCellPosition(heroPos, out heroCellX, out heroCellY);
                foreach (var p in debugPath)
                {
                    int tx, ty;
                    pathfinding.GetCellPosition(p - heroPos, out tx, out ty);
                    tx += CellCount/2;
                    ty += CellCount / 2;
                    Drawing.DrawRect(new Vector2(tx*10, 50 + (CellCount - ty - 1)*10), new Vector2(9, 9), Color.Yellow,
                        false);
                }
            }
            // Draw predicted Hero Pos
            int x, y;
            pathfinding.GetCellPosition(myHero.BasePredict(350) - heroPos, out x, out y);
            x += CellCount / 2;
            y += CellCount / 2;
            Drawing.DrawRect(new Vector2(x * 10, 50 + (CellCount - y - 1) * 10), new Vector2(9, 9), Color.AliceBlue, false);

            Vector2 screenPos;
            if (Drawing.WorldToScreen(myHero.Position, out screenPos))
            {
                Drawing.DrawCircle(screenPos, (uint)myHero.HullRadius, 24, Color.Red);
            }
            if (Drawing.WorldToScreen(myHero.BasePredict(300), out screenPos))
            {
                Drawing.DrawCircle(screenPos, (uint)myHero.HullRadius, 24, Color.Red);
            }
        }

        private void OnDebugDrawOnMap(EventArgs args)
        {
            foreach (ObstacleParticle obstacleParticle in obstacleParticles)
            {
                if (!obstacleParticle.IsValid) continue;

                if (obstacleParticle.IsLine)
                {
                    Vector2 startPos, endPos;
                    //Debugging.WriteLine("Len {0}", (obstacleParticle.EndPosition - obstacleParticle.Position).Length());

                    var visible = Drawing.WorldToScreen(obstacleParticle.Position, out startPos);
                    visible |= Drawing.WorldToScreen(obstacleParticle.EndPosition, out endPos);
                    if (visible)
                    {
                        var ortho = (obstacleParticle.EndPosition - obstacleParticle.Position);
                        ortho = ortho.Rotated(MathUtil.DegreesToRadians(90));
                        ortho.Normalize();
                        ortho *= obstacleParticle.Radius;

                        var a = obstacleParticle.Position + ortho;
                        var b = obstacleParticle.Position - ortho;
                        var c = obstacleParticle.EndPosition - ortho;
                        var d = obstacleParticle.EndPosition + ortho;

                        Vector2 sa, sb, sc, sd;
                        Drawing.WorldToScreen(a, out sa);
                        Drawing.WorldToScreen(b, out sb);
                        Drawing.WorldToScreen(c, out sc);
                        Drawing.WorldToScreen(d, out sd);
                        Drawing.DrawLine(sa, sb, Color.Blue);
                        Drawing.DrawLine(sb, sc, Color.Blue);
                        Drawing.DrawLine(sc, sd, Color.Blue);
                        Drawing.DrawLine(sd, sa, Color.Blue);

                        if (obstacleParticle.Position != obstacleParticle.CurrentPosition)
                        {
                            var t1 = obstacleParticle.CurrentPosition + ortho;
                            var t2 = obstacleParticle.CurrentPosition - ortho;
                            Vector2 st1, st2;
                            Drawing.WorldToScreen(t1, out st1);
                            Drawing.WorldToScreen(t2, out st2);
                            Drawing.DrawLine(st1, st2, Color.Yellow);
                        }
                    }
                }
                else
                {
                    Vector2 screenPos;
                    if (Drawing.WorldToScreen(obstacleParticle.EndPosition, out screenPos))
                    {
                        Vector2 radiusPos;
                        var tmp = obstacleParticle.EndPosition;
                        tmp.X += obstacleParticle.Radius;
                        Drawing.WorldToScreen(tmp, out radiusPos);
                        Drawing.DrawCircle(screenPos, (uint)(radiusPos-screenPos).Length(), 24, Color.Red);
                    }
                }
            }
            if (testParticle != null && testParticle.IsValid)
            {
                Vector2 startPos, endPos;
                Drawing.WorldToScreen(testParticle.GetControlPoint(0), out startPos);
                Drawing.WorldToScreen(testParticle.GetControlPoint(1), out endPos);

                Console.WriteLine("TestParticle {0}:",testParticle.HighestControlPoint);
                for (uint i = 0; i < testParticle.HighestControlPoint; ++i)
                {
                    Console.WriteLine(testParticle.GetControlPoint(i));
                }
                Console.WriteLine();

                /*
                 * 0 == StartPosition
                 * 1 == EndPosition
                 * 3 == CurrenPosition
                 */
               // Drawing.DrawCircle(startPos, (uint)200, 24, Color.Red);
                Drawing.DrawLine(startPos, endPos, Color.Red);
            }
            //if (miranaArrow != null && miranaArrow.IsValid)
            //{
            //    Console.WriteLine(miranaArrow.MovementSpeed);
            //}
        }


        #endregion
    }

}
