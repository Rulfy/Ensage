using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Ensage;
using Ensage.Common;
using Ensage.Common.Extensions.SharpDX;
using log4net;
using PlaySharp.Toolkit.Logging;
using SharpDX;

namespace Zaio.Prediction
{
    public enum PredictionType
    {
        Static,

        GridNav,

        Blasted
    }

    public static class Prediction
    {
        private static readonly ILog Log = AssemblyLogs.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private static readonly NavMeshPathfinding Pathfinding = new NavMeshPathfinding();
        private static Dictionary<Unit, Vector3> _lastPositions = new Dictionary<Unit, Vector3>();
        private static Dictionary<Unit, Queue<float>> _lastRotations = new Dictionary<Unit, Queue<float>>();

        static Prediction()
        {
            Game.OnIngameUpdate += Game_OnIngameUpdate;
            Events.OnClose += Events_OnClose;
        }

        private static void Events_OnClose(object sender, EventArgs e)
        {
            _lastPositions.Clear();
            _lastRotations.Clear();
        }

        public static bool IsRotating(Unit target)
        {
            var lastValue = _lastRotations.FirstOrDefault(x => x.Key == target);
            if (lastValue.Value == null || lastValue.Value.Count < 4)
            {
                return true;
            }

            foreach (var f in lastValue.Value)
            {
                //Log.Debug($"diff {Math.Abs(target.Rotation - f)}");
                if (Math.Abs(target.Rotation - f) > 15.0f)
                {
                    return true;
                }
            }
            return false;
        }

        public static bool IsMoving(Unit target)
        {
            Vector3 lastPos;
            if (_lastPositions.TryGetValue(target, out lastPos))
            {
                return target.NetworkPosition != lastPos;
            }
            return false;
        }

        public static IEnumerable<Vector3> PredictMyRoute(Unit target, int time, Vector3 targetPosition)
        {
            var maxDistance = time / 1000.0f * target.MovementSpeed + target.HullRadius;
            bool completed;
            var path = Pathfinding.CalculateStaticLongPath(
                target.NetworkPosition,
                targetPosition,
                target.MovementSpeed * time * 4,
                true,
                out completed).ToList();
            return !completed ? new List<Vector3> {targetPosition} : path;
        }

        public static Vector3 PredictPosition(Unit target, int time, bool noTurning = false,
            PredictionType type = PredictionType.GridNav)
        {
            var maxDistance = time / 1000.0f * target.MovementSpeed + target.HullRadius;

            if (noTurning && IsRotating(target)) // max distance checking? -> todo: testing
            {
                return Vector3.Zero;
            }

            if (!target.IsMoving)
            {
                return target.NetworkPosition;
            }

            var inFront = Ensage.Common.Prediction.InFront(target, maxDistance);
            inFront.Z = target.NetworkPosition.Z;

            if (time <= 400 || true)
            {
                Log.Debug($"using infront {inFront}");
                return inFront;
            }
            // TODO: better -> fix it
            bool completed;
            var path = Pathfinding.CalculateStaticLongPath(
                target.NetworkPosition,
                inFront * 1.5f,
                target.MovementSpeed * time * 4,
                true,
                out completed).ToList();
            if (!completed)
            {
                Log.Debug($"using gridnav not completed {inFront}");
                return inFront;
            }

            var distance = 0.0f;
            var lastNode = Vector3.Zero;
            for (var i = 0; i < path.Count; ++i)
            {
                var len = i == 0 ? (path[i] - target.NetworkPosition).Length() : (path[i] - path[i - 1]).Length();
                lastNode = path[i];
                if (maxDistance < len + distance)
                {
                    break;
                }

                distance += len;
            }

            var dir = lastNode.Normalized();
            dir *= maxDistance - distance;
            Log.Debug($"using gridnav  {lastNode + dir}");
            return lastNode + dir;
        }

        private static void Game_OnIngameUpdate(EventArgs args)
        {
            // Log.Debug($"waf: {ObjectManager.GetEntities<Hero>().FirstOrDefault(x => x != ObjectManager.LocalHero).RotationDifference}");

            if (Utils.SleepCheck("invReborn_Prediction_NavMesh"))
            {
                Utils.Sleep(500, "invReborn_Prediction_NavMesh");
                Pathfinding.UpdateNavMesh();
            }
            if (Utils.SleepCheck("invReborn_Prediction_Position"))
            {
                Utils.Sleep(125, "invReborn_Prediction_Position");
                var units =
                    ObjectManager.GetEntitiesParallel<Unit>()
                                 .Where(
                                     x =>
                                         x.IsValid && x.IsAlive && x.Team != ObjectManager.LocalPlayer.Team &&
                                         x.IsVisible);
                foreach (var unit in units)
                {
                    _lastPositions[unit] = unit.NetworkPosition;

                    Queue<float> lastRotations;
                    if (!_lastRotations.TryGetValue(unit, out lastRotations))
                    {
                        lastRotations = new Queue<float>(4);
                        _lastRotations[unit] = lastRotations;
                    }
                    else if (lastRotations.Count >= 4)
                    {
                        lastRotations.Dequeue();
                    }

                    lastRotations.Enqueue(unit.Rotation);
                }
                _lastPositions = _lastPositions.Where(x => x.Key.IsValid && x.Key.IsAlive)
                                               .ToDictionary(x => x.Key, y => y.Value);
                _lastRotations = _lastRotations.Where(x => x.Key.IsValid && x.Key.IsAlive)
                                               .ToDictionary(x => x.Key, y => y.Value);
            }
        }
    }
}