using System;

namespace VisibleByEnemy
{
    using System.Collections.Generic;
    using Ensage;
    using Ensage.Common.Menu;

    internal class Program
    {
        #region Static Fields

        private static Dictionary<Unit, ParticleEffect> _effects = new Dictionary<Unit, ParticleEffect>();

        private static readonly Menu Menu = new Menu("VisibleByEnemy", "visibleByEnemy", true);

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
            Entity.OnInt32PropertyChange += Entity_OnInt32PropertyChange;
        }

        #endregion

        #region Methods
        private static bool IsWard(Entity sender)
        {
            return (sender.ClassID == ClassID.CDOTA_NPC_Observer_Ward ||
                    sender.ClassID == ClassID.CDOTA_NPC_Observer_Ward_TrueSight);
        }

        private static bool IsMine(Entity sender)
        {
            return sender.ClassID == ClassID.CDOTA_NPC_TechiesMines;
        }

        private static bool IsUnit(Unit sender)
        {
            return !(sender is Hero) && !(sender is Building) && 
                ((sender.ClassID != ClassID.CDOTA_BaseNPC_Creep_Lane && sender.ClassID != ClassID.CDOTA_BaseNPC_Creep_Siege) || sender.IsControllable)
                            && sender.ClassID != ClassID.CDOTA_NPC_TechiesMines
                            && sender.ClassID != ClassID.CDOTA_NPC_Observer_Ward
                            && sender.ClassID != ClassID.CDOTA_NPC_Observer_Ward_TrueSight;
        }

        private static void Entity_OnInt32PropertyChange(Entity sender, Int32PropertyChangeEventArgs args)
        {
            var unit = sender as Unit;
            if (unit == null)
                return;

            if (args.PropertyName != "m_iTaggedAsVisibleByTeam")
                return;

            var player = ObjectManager.LocalPlayer;
            if (player == null || player.Team == Team.Observer || sender.Team != player.Team)
                return;

            var visible = args.NewValue == 0x1E;
            // heroes
            if (sender is Hero && Menu.Item("heroes").GetValue<bool>())
            {
                HandleEffect(unit, visible);
            }

            // wards
            else if (IsWard(sender) && Menu.Item("wards").GetValue<bool>())
            {
                HandleEffect(unit, visible);
            }

            // mines
            else if (IsMine(sender) && Menu.Item("mines").GetValue<bool>())
            {
                HandleEffect(unit, visible);
            }

            // units
            else if (Menu.Item("units").GetValue<bool>() && IsUnit(unit))
            {
                HandleEffect(unit, visible);
            }

            // buildings
            else if (sender is Building && Menu.Item("buildings").GetValue<bool>())
            {
                HandleEffect(unit, visible);
            }
        }

        private static void HandleEffect(Unit unit, bool visible)
        {
            if (!unit.IsValid)
            {
                return;
            }
            if (visible && unit.IsAlive)
            {
                ParticleEffect effect;
                if (!_effects.TryGetValue(unit, out effect))
                {
                    effect = unit.AddParticleEffect("particles/items_fx/aura_shivas.vpcf");
                    _effects.Add(unit, effect);
                }
            }
            else
            {
                ParticleEffect effect;
                if (_effects.TryGetValue(unit, out effect))
                {
                    effect.Dispose();
                    _effects.Remove(unit);
                }
            }
        }

        // ReSharper disable once InconsistentNaming
        private static void Item_ValueChanged(object sender, OnValueChangeEventArgs e)
        {
            var item = sender as MenuItem;
            if (item == null)
                return;

            bool hero = false, wards = false,  mines = false,units = false,buildings=false;
            switch (item.Name)
            {
                case "heroes":
                    hero = true;
                    break;
                case "wards":
                    wards = true;
                    break;
                case "mines":
                    mines = true;
                    break;
                case "units":
                    units = true;
                    break;
                case "buildings":
                    buildings = true;
                    break;
            }
            // update dictionary
            var newDict = new Dictionary<Unit,ParticleEffect>();
            foreach (var effect in _effects)
            {
                if( hero && effect.Key is Hero )
                    effect.Value.Dispose();
                else if( wards && IsWard(effect.Key) )
                    effect.Value.Dispose();
                else if (mines && IsMine(effect.Key))
                    effect.Value.Dispose();
                else if (units && IsUnit(effect.Key))
                    effect.Value.Dispose();
                else if (buildings && effect.Key is Building)
                    effect.Value.Dispose();
                else
                    newDict.Add(effect.Key,effect.Value);
            }
            _effects = newDict;
        }

        #endregion
    }
}
