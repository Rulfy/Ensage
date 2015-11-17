using System.Collections.Generic;
using System.Linq;
using Ensage;
using Ensage.Common.Menu;

namespace VisibleByEnemy
{
    class Program
    {
        private static readonly Menu Menu = new Menu("VisibleByEnemy", "visibleByEnemy", true);

        private static readonly Dictionary<Unit, ParticleEffect> Effects = new Dictionary<Unit, ParticleEffect>();
        public static void Main(string[] args)
        {
            MenuItem item;

            item = new MenuItem("heroes", "Check allied heroes").SetValue(true);
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

            Game.OnIngameUpdate += Game_OnUpdate;
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

        private static void Game_OnUpdate(System.EventArgs args)
        {
            var player = ObjectMgr.LocalPlayer;
            if (player == null || player.Team == Team.Observer)
                return;
            // check allied heroes
            var units = ObjectMgr.GetEntities<Unit>().Where(
                x =>
                // heroes
                (Menu.Item("heroes").GetValue<bool>() && x is Hero && x.Team == player.Team)
                // wards
                || (Menu.Item("wards").GetValue<bool>()
                    && (x.ClassID == ClassID.CDOTA_NPC_Observer_Ward
                        || x.ClassID == ClassID.CDOTA_NPC_Observer_Ward_TrueSight) && x.Team == player.Team)
                // techies mines
                || (Menu.Item("mines").GetValue<bool>() && x.ClassID == ClassID.CDOTA_NPC_TechiesMines
                    && x.Team == player.Team)
                // units
                || (Menu.Item("units").GetValue<bool>() && !(x is Hero) && !(x is Building) && x.ClassID != ClassID.CDOTA_BaseNPC_Creep_Lane
                    && x.ClassID != ClassID.CDOTA_NPC_TechiesMines && x.ClassID != ClassID.CDOTA_NPC_Observer_Ward
                    && x.ClassID != ClassID.CDOTA_NPC_Observer_Ward_TrueSight && x.Team == player.Team)
                // buildings
                || (Menu.Item("buildings").GetValue<bool>() && x is Building && x.Team == player.Team)).ToList();


            foreach (var unit in units)
            {
                HandleEffect(unit);
            }
        }

        static void HandleEffect(Unit unit)
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
    }
}
