using System.Collections.Generic;
using Ensage;
using Ensage.Common.Extensions.SharpDX;
using SharpDX;

namespace Evade
{
    using System;

    using Obstacles;

    public static class Helpers
    {
        public static bool IsWalkableCell(NavMeshCellFlags flags)
        {
            return !flags.HasFlag(NavMeshCellFlags.GridFlagObstacle) && flags.HasFlag(NavMeshCellFlags.Walkable)
                   && !flags.HasFlag(NavMeshCellFlags.Tree);
        }

        public static List<Vector3> GetPositionsWithDistanceAndAngle(
            Vector3 startPosition,
            Vector3 direction,
            float cellSize,
            int distance)
        {
            List<Vector3> result = new List<Vector3>();

            for (int ang = 0; ang < 360; ang += 90)
            {
                var currentDirection = direction.Rotated(MathUtil.DegreesToRadians(ang));
                for (int i = 0; i <= distance; ++i)
                {
                    var currentTarget = startPosition + (currentDirection * (i * cellSize));
                    result.Add(currentTarget);
                }
            }
            return result;
        }

        public static float TimeLeftFromObstacles(List<IObstacle> obstacleList)
        {
            var result = float.MaxValue;
            foreach (var obstacle in obstacleList)
            {
                var obstacleModifier = obstacle as ObstacleModifier;
                if (obstacleModifier != null)
                {
                    result = Math.Min(result, obstacleModifier.Modifier.RemainingTime * 1000);
                    continue;
                }
                var obstacleParticle = obstacle as ObstacleParticle;
                if (obstacleParticle != null)
                {
                    result = Math.Min(result, obstacleParticle.TimeLeft * 1000);
                    continue;
                }
            }
            return result;
        }
    }
}
