using System;
using System.Collections.Generic;
using System.Linq;
using Ensage;
using Ensage.Common;

namespace SpacebarToFarm
{
    public class InfoCentral
    {
        public static List<Creep> EnemyCreeps{ get; private set; }
        public static List<Creep> AlliedCreeps { get; private set; }

        public static readonly Dictionary<Creep, List<HealthEntry>> HealthInformation = new Dictionary<Creep, List<HealthEntry>>();

        static InfoCentral()
        {
            EnemyCreeps = new List<Creep>(256);
            AlliedCreeps = new List<Creep>(256);
            Events.OnClose += Events_OnClose;
            Game.OnIngameUpdate += Game_OnIngameUpdate;
            Entity.OnInt32PropertyChange += Entity_OnInt32PropertyChange;
        }

        private static void Events_OnClose(object sender, EventArgs e)
        {
            HealthInformation.Clear();
            EnemyCreeps.Clear();
            AlliedCreeps.Clear();
        }

        private static void Entity_OnInt32PropertyChange(Entity sender, Int32PropertyChangeEventArgs args)
        {
            var creep = sender as Creep;
            if (creep == null)
                return;

            if (args.PropertyName != "m_iHealth")
                return;

            List<HealthEntry> healthInfo;
            if (!HealthInformation.TryGetValue(creep, out healthInfo))
            {
                if (args.NewValue <= 0 || !creep.IsAlive || !creep.IsSpawned)
                    return;

                healthInfo = new List<HealthEntry>();
                HealthInformation.Add(creep, healthInfo);
            }
            if (args.NewValue <= 0 || !creep.IsAlive || !creep.IsSpawned)
            {
                HealthInformation.Remove(creep);
                return;
            }
            healthInfo.Add(new HealthEntry(args.NewValue));
            if (healthInfo.Count > 100)
                healthInfo.RemoveRange(0, healthInfo.Count - 100);
        }

        private static void Game_OnIngameUpdate(EventArgs args)
        {
            var player = ObjectManager.LocalPlayer;
            if (player == null)
                return;

            if (Utils.SleepCheck("lastHitCreepRefresh"))
            {
                EnemyCreeps = ObjectManager.GetEntities<Creep>()
                   .Where(x => x.IsAlive && x.IsSpawned && x.Team != player.Team )
                   .ToList();
                //AlliedCreeps = ObjectManager.GetEntities<Creep>()
                //  .Where(x => x.IsAlive && x.IsSpawned && x.Team == player.Team)
                //  .ToList();

                Utils.Sleep(250, "lastHitCreepRefresh");
            }
        }
    }
}
