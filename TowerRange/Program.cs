using System;
using System.Linq;
using Ensage;
using SharpDX;

namespace TowerRange
{
    class Program
    {
        private const bool OwnTowers = true;
        private const bool EnemyTowers = true;

        static void Main(string[] args)
        {
            Drawing.OnEndScene += Drawing_OnEndScene;
        }

        static void Drawing_OnEndScene(EventArgs args)
        {
            if (Drawing.Direct3DDevice == null || Drawing.Direct3DDevice.IsDisposed || !Game.IsInGame )
                return;

            if (!OwnTowers && !EnemyTowers)
                return;

            var player = EntityList.GetLocalPlayer();
            if (player == null )
                return;
            var towers = EntityList.GetEntities<Building>().Where(x => x.IsAlive && x.ClassId == ClassId.CDOTA_BaseNPC_Tower).ToList();
            if (!towers.Any())
                return;

            if (player.Team == Team.Observer)
            {
                foreach (var tower in towers)
                {
                    Circle.DrawCircle(tower.Position, 850, Color.Red, 1);
                }
            }
            else
            {
                if (EnemyTowers)
                {
                    foreach (var tower in towers.Where(x => x.Team != player.Team))
                    {
                        Circle.DrawCircle(tower.Position, 850, Color.Red, 1);
                    }
                }
                if (OwnTowers)
                {
                    foreach (var tower in towers.Where(x => x.Team == player.Team))
                    {
                        Circle.DrawCircle(tower.Position, 850, Color.Green, 1);
                    }
                }
            }
        }
    }
}
