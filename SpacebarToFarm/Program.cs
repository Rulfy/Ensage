using System;
using System.Collections.Generic;
using System.Linq;
using Ensage;
using Ensage.Common;
using SpacebarToFarm.Interfaces;
using SpacebarToFarm.Interfaces.Units;

namespace SpacebarToFarm
{
    class Program
    {
        #region Properties
        private static readonly Dictionary<Unit, FarmUnit> FarmUnits = new Dictionary<Unit, FarmUnit>();
        private static readonly List<FarmUnit> AutoFarmUnits = new List<FarmUnit>();
        private static List<Unit> _oldSelection = new List<Unit>();

        private static bool _farmPressed;
        #endregion

        static void Main()
        {
            FarmMenu.Initialize();

            Events.OnClose += Events_OnClose;

            FarmMenu.FarmPressed += FarmMenu_FarmPressed;
            FarmMenu.AutoFarmChanged += FarmMenu_AutoFarmChanged;
            FarmMenu.ActiveEffectChanged += FarmMenu_ActiveEffectChanged;
            FarmMenu.RangeEffectChanged += FarmMenu_RangeEffectChanged;
            FarmMenu.RangeChanged += FarmMenu_RangeChanged; ;
            Game.OnIngameUpdate += Game_OnIngameUpdate;
        }

        private static void FarmMenu_RangeChanged(object sender, EventArgs e)
        {
            FarmMenu_RangeEffectChanged(sender, new BoolEventArgs(false));
            FarmMenu_RangeEffectChanged(sender, new BoolEventArgs(FarmMenu.ShouldDrawLasthitRange));
            FarmMenu_ActiveEffectChanged(sender, new BoolEventArgs(false));
            FarmMenu_ActiveEffectChanged(sender, new BoolEventArgs(FarmMenu.ShouldUseActiveEffect));
        }

        private static void FarmMenu_RangeEffectChanged(object sender, BoolEventArgs e)
        {
            if (e.Value)
            {
                var list = FarmUnits.Where(x => _oldSelection.Contains(x.Key)).Select(x => x.Value).ToList();
                foreach (var farmUnit in list)
                {
                    farmUnit.AddRangeEffect();
                }
                foreach (var autoFarmUnit in AutoFarmUnits)
                {
                    autoFarmUnit.AddRangeEffect();
                }
            }
            else
            {
                foreach (var source in FarmUnits.Values.Where(x => x.IsRangeEffectActive))
                {
                    source.RemoveRangeEffect();
                }
            }
        }

        private static void FarmMenu_ActiveEffectChanged(object sender, BoolEventArgs e)
        {
            if (e.Value)
            {
                var list = FarmUnits.Where(x => _oldSelection.Contains(x.Key)).Select(x => x.Value).ToList();
                foreach (var farmUnit in list)
                {
                    farmUnit.AddFarmActiveEffect();
                }
                foreach (var autoFarmUnit in AutoFarmUnits)
                {
                    autoFarmUnit.AddFarmActiveEffect();
                }
            }
            else
            {
                foreach (var source in FarmUnits.Values.Where(x => x.IsFarmActiveEffectActive))
                {
                    source.RemoveFarmActiveEffect();
                }
            }
        }

        #region Events
        private static void Events_OnClose(object sender, EventArgs e)
        {
            AutoFarmUnits.Clear();
            FarmUnits.Clear();
        }
        private static void FarmMenu_AutoFarmChanged(object sender, BoolEventArgs e)
        {
            if (!e.Value)
                return;

            if (!Game.IsInGame || Game.IsPaused)
                return;

            var player = ObjectManager.LocalPlayer;
            if (player == null)
                return;

            var selection = player.Selection.Where(x => x is Unit).Cast<Unit>().ToList();
            if (!selection.Any())
                return;

            bool newOne = false;
            var tmpList = new List<FarmUnit>();
            foreach (var unit in selection)
            {
                if (!unit.IsAlive || !unit.IsControllable)
                    continue;

                FarmUnit farmer;
                if (!FarmUnits.TryGetValue(unit, out farmer))
                {
                    farmer = CreateFarmer(unit);
                    FarmUnits.Add(unit, farmer);
                    newOne = true;
                }
                if (!newOne && !AutoFarmUnits.Contains(farmer))
                {
                    newOne = true;
                }
                tmpList.Add(farmer);
            }
            // if one new unit is in selection then let all autofarm, else toggle them off
            if (newOne)
            {
                foreach (var farmUnit in tmpList)
                {
                    if (!AutoFarmUnits.Contains(farmUnit))
                    {
                        AutoFarmUnits.Add(farmUnit);
                        farmUnit.AddEffects();

                        FarmMenu.AddAutoFarmEntry(farmUnit.ControlledUnit);
                    }
                }
            }
            else
            {
                foreach (var farmUnit in tmpList)
                {
                    AutoFarmUnits.Remove(farmUnit);
                    farmUnit.RemoveEffects();
                    FarmMenu.RemoveAutoFarmEntry(farmUnit.ControlledUnit);
                }
            }
        }
        private static void FarmMenu_FarmPressed(object sender, BoolEventArgs e)
        {
            if (!e.Value)
            {
                foreach (var unit in _oldSelection)
                {
                    FarmUnit farmer;
                    if (FarmUnits.TryGetValue(unit, out farmer))
                    {
                        farmer.RemoveEffects();
                    }
                }
                _oldSelection.Clear();
            }
            _farmPressed = e.Value;
        }

        private static void Game_OnIngameUpdate(EventArgs args)
        {
            if (!Game.IsInGame || Game.IsPaused)
                return;

            // auto farm units
            for( int i = AutoFarmUnits.Count - 1; i >= 0; --i )
            {
                var entry = AutoFarmUnits[i];
                if (!entry.IsValid)
                {
                    entry.RemoveEffects();
                    AutoFarmUnits.RemoveAt(i);
                    continue;
                }
                entry.LastHit();
            }

            // currently pressed
            if (!_farmPressed)
                return;
                

            var player = ObjectManager.LocalPlayer;
            if (player == null)
                return;

            List<Unit> selection = player.Selection.Where(x => x is Unit).Cast<Unit>().ToList();
            if (!selection.Any())
                return;

            if (!_oldSelection.SequenceEqual(selection))
            {
                foreach (var unit in _oldSelection)
                {
                    FarmUnit farmer;
                    if (FarmUnits.TryGetValue(unit, out farmer))
                    {
                        farmer.RemoveEffects();
                    }
                }
                foreach (var unit in selection)
                {
                    FarmUnit farmer;
                    if (FarmUnits.TryGetValue(unit, out farmer))
                    {
                        farmer.AddEffects();
                    }
                }
                _oldSelection = selection;
            }

            foreach (var unit in selection)
            {
                if( !unit.IsAlive || !unit.IsControllable )
                    continue;
               
                FarmUnit farmer;
                if (!FarmUnits.TryGetValue(unit, out farmer))
                {
                    farmer = CreateFarmer(unit);
                    FarmUnits.Add(unit, farmer);

                    farmer.AddEffects();
                }
                AutoFarmUnits.Remove(farmer);
                farmer.LastHit();
            }
        }

        #endregion

        private static FarmUnit CreateFarmer(Unit unit)
        {
            switch (unit.ClassID)
            {
                case ClassID.CDOTA_BaseNPC_Invoker_Forged_Spirit:
                    return new FarmForgeSpirit(unit);

                // TODO: special hero implementations

                default:
                    if(unit.IsMelee)
                        return new FarmUnitMelee(unit);
                    else
                        return new FarmUnitRanged(unit);
            }
        }
    }
}
