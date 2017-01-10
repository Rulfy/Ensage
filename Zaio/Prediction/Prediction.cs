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
        private static Dictionary<Unit, float> _lastRotations = new Dictionary<Unit, float>();

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
            return target.RotationDifference != 0;
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

        public static Vector3 PredictPosition(Unit target, int time, bool noTurning = false, PredictionType type = PredictionType.GridNav)
        {
            var maxDistance = time / 1000.0f * target.MovementSpeed + target.HullRadius;

            if (noTurning && IsRotating(target)) // max distance checking? -> todo: testing
                return Vector3.Zero;

            if (!target.IsMoving)
                return target.NetworkPosition;

            var inFront = Ensage.Common.Prediction.InFront(target, maxDistance);
            inFront.Z = target.NetworkPosition.Z;

            if (time <= 400)
            {
                return inFront;
            }

            bool completed;
            var path = Pathfinding.CalculateStaticLongPath(
                target.NetworkPosition,
                inFront * 1.5f,
                target.MovementSpeed * time * 4,
                true,
                out completed).ToList();
            if (!completed)
            {
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
            return lastNode + dir;
        }

        private static void Game_OnIngameUpdate(EventArgs args)
        {
            if (Utils.SleepCheck("invReborn_Prediction_NavMesh"))
            {
                Utils.Sleep(500, "invReborn_Prediction_NavMesh");
                Pathfinding.UpdateNavMesh();
            }
            if (Utils.SleepCheck("invReborn_Prediction_Position"))
            {
                Utils.Sleep(250, "invReborn_Prediction_Position");
                var units =
                    ObjectManager.GetEntitiesFast<Unit>()
                                 .Where(
                                     x =>
                                         x.IsValid && x.IsAlive && x.Team != ObjectManager.LocalPlayer.Team &&
                                         x.IsVisible);
                foreach (var unit in units)
                {
                    _lastPositions[unit] = unit.NetworkPosition;
                    _lastRotations[unit] = unit.NetworkRotationRad;
                }
                _lastPositions = _lastPositions.Where(x => x.Key.IsValid && x.Key.IsAlive)
                                               .ToDictionary(x => x.Key, y => y.Value);
                _lastRotations = _lastRotations.Where(x => x.Key.IsValid && x.Key.IsAlive)
                                               .ToDictionary(x => x.Key, y => y.Value);
            }
        }
    }
}