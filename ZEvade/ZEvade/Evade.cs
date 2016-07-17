using System;
using System.Collections.Generic;
using Ensage;
using Ensage.Common;
using Evade.EvadeData;
using Evade.Obstacles;

namespace Evade
{
    using System.Linq;

    using Ensage.Common.Extensions;

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

        private readonly Hero myHero = ObjectManager.LocalHero;

        private bool isPositionKnown;
        private Vector3 movePosition;
        private bool pathingMoved;
        private bool pathingAborted;
        private List<Vector3> forcePath;

        public Evade()
        {
            Game.OnIngameUpdate += Game_OnIngameUpdate;
            Unit.OnModifierAdded += Unit_OnModifierAdded;
            Unit.OnModifierRemoved += Unit_OnModifierRemoved;
            //Entity.OnFloatPropertyChange += Entity_OnFloatPropertyChange;
            Entity.OnBoolPropertyChange += Entity_OnBoolPropertyChange;
            //Entity.OnInt32PropertyChange += Entity_OnInt32PropertyChange;
            //Entity.OnInt64PropertyChange += Entity_OnInt64PropertyChange;
            Player.OnExecuteOrder += Player_OnExecuteOrder;

            // Debug
            var debugMenu = Program.Menu.Children.First(x => x.Name == "debugMenu");
            // Drawing
            var debugDraw = debugMenu.Items.First(x => x.Name == "debugDraw");
            if(debugDraw.GetValue<bool>())
                Drawing.OnDraw += Drawing_OnDebugDraw;

            debugDraw.ValueChanged += (sender,args) =>
                    {
                        if (args.GetNewValue<bool>())
                            Drawing.OnDraw += Drawing_OnDebugDraw;
                        else
                            Drawing.OnDraw -= Drawing_OnDebugDraw;
                    };
            // AutoAttacking
            var debugAttack = debugMenu.Items.First(x => x.Name == "debugBotAttack");
            if (debugAttack.GetValue<bool>())
                Game.OnIngameUpdate += Game_OnBotAttackTick;
            debugAttack.ValueChanged += (sender, args) =>
            {
                if (args.GetNewValue<bool>())
                    Game.OnIngameUpdate += Game_OnBotAttackTick;
                else
                    Game.OnIngameUpdate -= Game_OnBotAttackTick;
            };
        }

        

        private void Entity_OnBoolPropertyChange(Entity sender, BoolPropertyChangeEventArgs args)
        {
            if (!sender.Equals(myHero)) return;
            if (args.PropertyName == "m_bIsMoving")
            {
                if (!args.NewValue)
                {
                    //isPositionKnown = false;
                    Console.WriteLine("Stopping and not known");
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
            if(!args.Entities.Contains(myHero))
                return;
            switch (args.Order)
            {
                case Order.AbilityLocation:
                case Order.MoveLocation:
                case Order.AttackLocation:
                case Order.DropItem:
                    movePosition = args.TargetPosition;
                    isPositionKnown = true;
                    pathingAborted = true;
                    break;
                case Order.AbilityTarget:
                case Order.MoveTarget:
                case Order.AbilityTargetRune:
                case Order.TransferItem:
                    movePosition = args.Target.Position;
                    isPositionKnown = true;
                    pathingAborted = true;
                    break;

                case Order.AbilityTargetTree:
                case Order.Stop:
                case Order.Hold:
                    pathingAborted = true;
                    break;


            }
        }

        private void Entity_OnInt64PropertyChange(Entity sender, Int64PropertyChangeEventArgs args)
        {
            if (!(sender is Hero)) return;
            Console.WriteLine("64 - {0}: {1}", sender.Name, args.PropertyName);
        }

        private void Entity_OnInt32PropertyChange(Entity sender, Int32PropertyChangeEventArgs args)
        {
            if (!(sender is Hero) || !Equals(sender, myHero) || args.PropertyName != "m_NetworkActivity") return;
            Console.WriteLine("32 - {0}: {1} [{2} -> {3}]", sender.Name, args.PropertyName, (NetworkActivity)args.OldValue, (NetworkActivity)args.NewValue);
        }

        private void Entity_OnFloatPropertyChange(Entity sender, FloatPropertyChangeEventArgs args)
        {
            if (!(sender is Hero) || args.PropertyName == "m_flStartSequenceCycle") return;
            Console.WriteLine("Fl - {0}: {1}", sender.Name, args.PropertyName);
        }

        private void Unit_OnModifierRemoved(Unit sender, ModifierChangedEventArgs args)
        {
            var entry = obstacles.Find(x => x.GetHandle() == sender.Handle && x is ObstacleModifier) as ObstacleModifier;
            // test if it's a known obstacle which is getting removed
            if (entry == null) return;

            pathfinding.RemoveObstacle(entry.ID);
            obstacles.Remove(entry);
        }

        private void Unit_OnModifierAdded(Unit sender, ModifierChangedEventArgs args)
        {
            Console.WriteLine("Added {0}: {1} {2} {3}", sender.Handle, args.Modifier.Name, args.Modifier.RemainingTime, args.Modifier.Duration);

            if (!(sender is Hero) && sender.Owner == null) return;

            // test if it's an modifier we want to avoid
            var supportedMod = supportedModifiers.Find(x => x.ModifierName == args.Modifier.Name);
            if (supportedMod == null) return;

            var caster = sender is Hero ? sender : sender.Owner;

            if (supportedMod.EnemyTeamOnly && (!supportedMod.EnemyTeamOnly || caster.Team == myHero.Team)) return;


            var ability = caster.Spellbook().Spells.FirstOrDefault(x => x.ClassID == supportedMod.AbilityClassID);
            if (ability == null) return;

            // TODO test if ability actually does damage? magic immune? pure?

            var id = pathfinding.AddObstacle(sender.Position, ability.GetRadius(ability.Name));
            obstacles.Add(new ObstacleModifier(id, sender, args.Modifier));
        }

        

        
        private void Game_OnIngameUpdate(EventArgs args)
        {
            if (Utils.SleepCheck("evadeupdate"))
            {
                Utils.Sleep(125, "evadeupdate");

                pathfinding.UpdateNavMesh();

                var obstaclesIDs = pathfinding.GetIntersectingObstacleIDs(myHero.Position, myHero.HullRadius).ToList();
                var obstaclesPredictedIDs = pathfinding.GetIntersectingObstacleIDs(myHero.Predict(300), myHero.HullRadius).ToList();


                bool tried = false;
                bool completed = false;
                List<Vector3> path = null;
                // test if we're currently on an obstacle
                if (obstaclesIDs.Any())
                {
                    tried = true;
                    movePosition = GetValidMovePosition(movePosition);
                    float maxDistance = (movePosition - myHero.Position).Length() * 4;

                    float turnRate =
                        Game.FindKeyValues(myHero.Name + "/MovementTurnRate", KeyValueSource.Hero).FloatValue;
                    float timeLeft = TimeLeftFromObstacles( obstacles.Where( x => obstaclesIDs.Any(y => y == x.ID)).ToList() );

                    var path1 = pathfinding.CalculatePathFromObstacle(
                        myHero.Position,
                        movePosition,
                        myHero.RotationRad,
                        myHero.MovementSpeed,
                        turnRate,
                        timeLeft,
                        maxDistance,
                        true,
                        out completed).ToList();

                    path = pathfinding.CalculateLongPath(
                       myHero.Position,
                       movePosition,
                       maxDistance,
                       true,
                       out completed).ToList();
                }
                else if (obstaclesPredictedIDs.Any())
                {
                    tried = true;
                    movePosition = GetValidMovePosition(movePosition);
                    float maxDistance = (movePosition - myHero.Position).Length() * 4;

                    path = pathfinding.CalculateLongPath(
                        myHero.Position,
                        movePosition,
                        maxDistance,
                        true,
                        out completed).ToList();
                }
                else
                {
                    forcePath = null;
                }
                if (tried)
                {
                    // if we tried to find a path...
                    if (completed && path.Any())
                    {
                        forcePath = path;
                        pathingAborted = true;
                        // TODO: get intersecting obstacle ids need!

                        // test if we guessed a position to go
                        if (!isPositionKnown)
                            pathingMoved = true;

                        foreach (var vector3 in path)
                        {
                            Console.WriteLine(vector3);
                        }
                    }
                    else
                    {
                        // maybe we are at the spawn and enemy exactly tried to attack there
                        Console.WriteLine("can't run ! panic!! -> use spells");
                    }
                }
               
            }

            if (pathingAborted && forcePath != null && forcePath.Any() )
            {
                myHero.Move(forcePath[0]);
                for (int i = 1; i < forcePath.Count; ++i)
                {
                    myHero.Move(forcePath[i], true);
                }
                pathingAborted = false;
            }
            else if (pathingMoved)
            {
                pathingMoved = false;
                myHero.Stop();
                Console.WriteLine("STOP {0} - {1}", forcePath!=null, pathingAborted);
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
            Console.WriteLine("Couldn't find a valid target position which is safe :(");
            return GetValidMovePosition();
        }

        #region DebugSection

        private void Game_OnBotAttackTick(EventArgs args)
        {
            if (!Utils.SleepCheck("onBotAttack")) return;
            Utils.Sleep(125,"onBotAttack");

            const int RndDistance = 750;
            var rnd = new Random();
            foreach (var enemy in ObjectManager.GetEntities<Hero>().Where(x => x.Team != myHero.Team).ToList())
            {
                var validSpell =
                    enemy.Spellbook.Spells.FirstOrDefault(
                        x => supportedModifiers.Any(y => y.AbilityClassID == x.ClassID));
                if (validSpell != null && validSpell.CanBeCasted())
                {
                    var spellSleepName = $"onBotAttack_{enemy.Handle}_{validSpell.ClassID}";
                    if (!Utils.SleepCheck(spellSleepName)) continue;
                    Utils.Sleep(1000+rnd.Next(2000), spellSleepName);

                    var targetPos = myHero.Position;
                    targetPos.X += rnd.NextFloat(-RndDistance, +RndDistance);
                    targetPos.Y += rnd.NextFloat(-RndDistance, +RndDistance);
                    validSpell.UseAbility(targetPos);
                }

            }
        }

        private void Drawing_OnDebugDraw(EventArgs args)
        {
            var hero = ObjectManager.LocalHero;
            if (hero == null)
                return;

            var heroPos = hero.Position;
            // Drawing the navmesh grid
            const int CellCount = 40;
            for (int x = 0; x < CellCount; ++x)
            {
                for (int y = 0; y < CellCount; ++y)
                {
                    Vector2 p;
                    p.X = pathfinding.CellSize * (x - CellCount / 2) + heroPos.X;
                    p.Y = pathfinding.CellSize * (y - CellCount / 2) + heroPos.Y;

                    Color c;
                    if (x == CellCount / 2 && y == CellCount / 2)
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
                    Drawing.DrawRect(new Vector2(x * 10, 50 + (CellCount - y - 1) * 10), new Vector2(9, 9), c, false);
                }
            }
        }
    }
#endregion
}
