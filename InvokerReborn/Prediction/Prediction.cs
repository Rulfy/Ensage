using Ensage;
using SharpDX;

namespace InvokerReborn.Prediction
{
    public enum PredictionType
    {
        Static,
        GridNav,
        Blasted,
    }
    public static class Prediction
    {
        public static Vector3 PredictPosition(Unit target, int time, PredictionType type = PredictionType.GridNav)
        {
            var pos = target.NetworkPosition;


            return pos;
        }
    }
}