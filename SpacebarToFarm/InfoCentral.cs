using SharpDX;

namespace SpacebarToFarm
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    using Ensage;
    using Ensage.Common;
    using Ensage.Common.Extensions;

    public static class InfoCentral
    {
        #region Static Fields

        public static readonly Dictionary<Creep, List<HealthEntry>> HealthInformation =
            new Dictionary<Creep, List<HealthEntry>>();

        public static Dictionary<Unit, float> AnimationInformation = new Dictionary<Unit, float>();

        #endregion

        #region Constructors and Destructors

        static InfoCentral()
        {
            EnemyCreeps = new List<Creep>(256);
            AlliedCreeps = new List<Creep>(256);

            Events.OnClose += Events_OnClose;
            Game.OnIngameUpdate += Game_OnIngameUpdate;
            //Entity.OnInt32PropertyChange += Entity_OnInt32PropertyChange;
            //ObjectManager.OnAddTrackingProjectile += ObjectManager_OnAddTrackingProjectile;
            Entity.OnAnimationChanged += Entity_OnAnimationChanged;

           // Drawing.OnDraw += Drawing_OnDraw;
        }

        private static void Drawing_OnDraw(EventArgs args)
        {
            var ControlledUnit = ObjectManager.LocalHero;
            var AttackRange = 550;
            foreach (var attacker in AnimationInformation)
            {
                Vector2 screenPos;
                if (Drawing.WorldToScreen(attacker.Key.NetworkPosition, out screenPos))
                {
                    var distance = attacker.Key.Distance2D(ControlledUnit) - attacker.Key.HullRadius - ControlledUnit.HullRadius;

                    var projectileTime = Math.Min(AttackRange, distance) / (float)ControlledUnit.ProjectileSpeed();
                    var timeLeft= (Math.Max(0, distance - AttackRange) / ControlledUnit.MovementSpeed)
                           + (float)ControlledUnit.AttackPoint() + (float)ControlledUnit.GetTurnTime(attacker.Key) + projectileTime
                           - Game.Ping / 1000.0f;

                    var nextAttack =
                       (float)
                       (attacker.Key.SecondsPerAttack * 1 - (Game.RawGameTime - attacker.Value)
                        - attacker.Key.AttackPoint());
                    var nextAttack1 =
                      (float)
                      (attacker.Key.SecondsPerAttack * 2 - (Game.RawGameTime - attacker.Value)
                       - attacker.Key.AttackPoint());
                    Drawing.DrawText($"{timeLeft} | {nextAttack} | {nextAttack1}",screenPos,Color.White,FontFlags.AntiAlias);
                    Drawing.DrawText($"{Math.Min(AttackRange, distance)} | {distance}", screenPos + new Vector2(0,60), Color.White, FontFlags.AntiAlias);
                }

            }
        }

        #endregion

        #region Public Properties

        public static List<Creep> AlliedCreeps { get; private set; }

        public static List<Creep> EnemyCreeps { get; private set; }

        #endregion

        #region Methods

        private static void Entity_OnAnimationChanged(Entity sender, EventArgs args)
        {
            var unit = sender as Unit;
            if (unit != null && unit.Animation.Name.Contains("attack"))
            {
                AnimationInformation[unit] = Game.RawGameTime;
            }
        }

        private static void Entity_OnInt32PropertyChange(Entity sender, Int32PropertyChangeEventArgs args)
        {
            var creep = sender as Creep;
            if (creep == null) return;

            if (args.PropertyName != "m_iHealth") return;

            List<HealthEntry> healthInfo;
            if (!HealthInformation.TryGetValue(creep, out healthInfo))
            {
                if (args.NewValue <= 0 || !creep.IsAlive || !creep.IsSpawned) return;

                healthInfo = new List<HealthEntry>();
                HealthInformation.Add(creep, healthInfo);
            }
            if (args.NewValue <= 0 || !creep.IsAlive || !creep.IsSpawned)
            {
                HealthInformation.Remove(creep);
                return;
            }
            healthInfo.Add(new HealthEntry(args.NewValue));
            if (healthInfo.Count > 100) healthInfo.RemoveRange(0, healthInfo.Count - 100);
        }

        private static void Events_OnClose(object sender, EventArgs e)
        {
            HealthInformation.Clear();
            EnemyCreeps.Clear();
            AlliedCreeps.Clear();
            AnimationInformation.Clear();
        }

        private static void Game_OnIngameUpdate(EventArgs args)
        {
            if (!Utils.SleepCheck("lastHitCreepRefresh"))
            {
                return;
            }

            var player = ObjectManager.LocalPlayer;
            if (player == null) return;

            EnemyCreeps =
                ObjectManager.GetEntitiesParallel<Creep>()
                    .Where(x => x.IsValid && x.IsAlive && x.IsSpawned && x.Team != player.Team)
                    .ToList();
            AlliedCreeps =
                ObjectManager.GetEntitiesParallel<Creep>()
                    .Where(x => x.IsValid && x.IsAlive && x.IsSpawned && x.Team == player.Team)
                    .ToList();

            AnimationInformation =
                AnimationInformation.Where(
                        x => x.Key.IsValid && x.Key.IsAlive && (Game.RawGameTime - x.Value) < x.Key.SecondsPerAttack * 4)
                    .ToDictionary(x => x.Key, x => x.Value);

            Utils.Sleep(250, "lastHitCreepRefresh");
        }

        private static void ObjectManager_OnAddTrackingProjectile(TrackingProjectileEventArgs args)
        {
            var source = args.Projectile.Source as Unit;
            var creep = args.Projectile.Target as Creep;
            if (creep == null || source == null) return;

            var damage = (source.MinimumDamage + source.MaximumDamage) / 2 + source.BonusDamage;
            var time = source.Distance2D(creep) / args.Projectile.Speed;
            if (damage <= 0 || time <= 0)
            {
                return;
            }

            List<HealthEntry> healthInfo;
            if (!HealthInformation.TryGetValue(creep, out healthInfo))
            {
                healthInfo = new List<HealthEntry>();
                HealthInformation.Add(creep, healthInfo);
            }

            var latestEntry =
                healthInfo.Where(x => x.Time > Game.RawGameTime).OrderByDescending(x => x.Time).FirstOrDefault();

            int latestHealth;
            if (latestEntry != null)
            {
                latestHealth = latestEntry.Health;
                if (latestHealth > creep.Health)
                {
                    latestHealth = (int)(creep.Health - (latestHealth - creep.Health));
                }
            }
            else latestHealth = (int)creep.Health;

            healthInfo.Add(new HealthEntry(latestHealth - damage, time));
            if (healthInfo.Count > 100) healthInfo.RemoveRange(0, healthInfo.Count - 100);
        }

        #endregion
    }
}