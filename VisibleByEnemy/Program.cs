namespace VisibleByEnemy
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    using Ensage;
    using Ensage.Common;
    using Ensage.Common.Menu;
    using Ensage.Common.Objects;

    internal class Program
    {
        #region Static Fields

        private static readonly Dictionary<Unit, ParticleEffect> Effects = new Dictionary<Unit, ParticleEffect>();

        private static readonly Menu Menu = new Menu("VisibleByEnemy", "visibleByEnemy", true);

        private static List<Building> buildings = new List<Building>();

        private static List<Unit> mines = new List<Unit>();

        private static List<Unit> units = new List<Unit>();

        private static List<Unit> wards = new List<Unit>();

        #endregion

        #region Public Methods and Operators

        private static void Main()
        {
            var item = new MenuItem("heroes", "Check allied heroes").SetValue(true);
            item.ValueChanged += Item_ValueChanged;
            Menu.AddItem(item);

            item = new MenuItem("wards", "Check wards").SetValue(true);
            item.ValueChanged += Item_ValueChanged;
            Menu.AddItem(item);

            item = new MenuItem("mines", "Check techies mines").SetValue(true);
            item.ValueChanged += Item_ValueChanged;
            Menu.AddItem(item);

            item = new MenuItem("units", "Check controlled units (not lane creeps)").SetValue(true);
            item.ValueChanged += Item_ValueChanged;
            Menu.AddItem(item);

            item = new MenuItem("buildings", "Check buildings").SetValue(true);
            item.ValueChanged += Item_ValueChanged;
            Menu.AddItem(item);

            Menu.AddToMainMenu();

            Game.OnIngameUpdate += Game_OnIngameUpdate;
        }

        #endregion

        #region Methods

        private static void HandleEffect(Unit unit)
        {
            if (unit.IsVisibleToEnemies && unit.IsAlive)
            {
                ParticleEffect effect;
                if (!Effects.TryGetValue(unit, out effect))
                {
                    effect = unit.AddParticleEffect("particles/items_fx/aura_shivas.vpcf");
                    Effects.Add(unit, effect);
                }
            }
            else
            {
                ParticleEffect effect;
                if (Effects.TryGetValue(unit, out effect))
                {
                    effect.Dispose();
                    Effects.Remove(unit);
                }
            }
        }

        private static void Game_OnIngameUpdate(EventArgs args)
        {
            var player = ObjectMgr.LocalPlayer;
            if (player == null || player.Team == Team.Observer)
            {
                return;
            }

            // heroes
            if (Menu.Item("heroes").GetValue<bool>())
            {
                HandleHeroes(player);
            }

            // wards
            if (Menu.Item("wards").GetValue<bool>())
            {
                HandleWards(player);
            }

            // mines
            if (Menu.Item("mines").GetValue<bool>())
            {
                HandleMines(player);
            }

            // units
            if (Menu.Item("units").GetValue<bool>())
            {
                HandleUnits(player);
            }

            // buildings
            if (Menu.Item("buildings").GetValue<bool>())
            {
                HandleBuildings(player);
            }
        }

        private static void HandleBuildings(Entity player)
        {
            if (Utils.SleepCheck("VisibleByEnemy.UpdateBuildings"))
            {
                buildings = ObjectMgr.GetEntities<Building>().Where(x => x.Team == player.Team).ToList();
                Utils.Sleep(1000, "VisibleByEnemy.UpdateBuildings");
            }

            foreach (var building in buildings)
            {
                HandleEffect(building);
            }
        }

        private static void HandleHeroes(Entity player)
        {
            foreach (var hero in Heroes.GetByTeam(player.Team))
            {
                HandleEffect(hero);
            }
        }

        private static void HandleMines(Entity player)
        {
            if (Utils.SleepCheck("VisibleByEnemy.UpdateMines"))
            {
                mines =
                    ObjectMgr.GetEntities<Unit>()
                        .Where(x => x.ClassID == ClassID.CDOTA_NPC_TechiesMines && x.Team == player.Team)
                        .ToList();
                Utils.Sleep(1000, "VisibleByEnemy.UpdateMines");
            }

            foreach (var mine in mines)
            {
                HandleEffect(mine);
            }
        }

        private static void HandleUnits(Entity player)
        {
            if (Utils.SleepCheck("VisibleByEnemy.UpdateUnits"))
            {
                units =
                    ObjectMgr.GetEntities<Unit>()
                        .Where(
                            x =>
                            !(x is Hero) && !(x is Building) && x.ClassID != ClassID.CDOTA_BaseNPC_Creep_Lane
                            && x.ClassID != ClassID.CDOTA_NPC_TechiesMines
                            && x.ClassID != ClassID.CDOTA_NPC_Observer_Ward
                            && x.ClassID != ClassID.CDOTA_NPC_Observer_Ward_TrueSight && x.Team == player.Team)
                        .ToList();
                Utils.Sleep(1000, "VisibleByEnemy.UpdateUnits");
            }

            foreach (var unit in units)
            {
                HandleEffect(unit);
            }
        }

        private static void HandleWards(Entity player)
        {
            if (Utils.SleepCheck("VisibleByEnemy.UpdateWards"))
            {
                wards =
                    ObjectMgr.GetEntities<Unit>()
                        .Where(
                            x =>
                            (x.ClassID == ClassID.CDOTA_NPC_Observer_Ward
                             || x.ClassID == ClassID.CDOTA_NPC_Observer_Ward_TrueSight) && x.Team == player.Team)
                        .ToList();
                Utils.Sleep(1000, "VisibleByEnemy.UpdateWards");
            }

            foreach (var ward in wards)
            {
                HandleEffect(ward);
            }
        }

        // ReSharper disable once InconsistentNaming
        private static void Item_ValueChanged(object sender, OnValueChangeEventArgs e)
        {
            foreach (var particleEffect in Effects.Values)
            {
                particleEffect.Dispose();
            }

            Effects.Clear();
        }

        #endregion
    }
}
