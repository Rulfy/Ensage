// <copyright file="VisibleByEnemyConfig.cs" company="Ensage">
//    Copyright (c) 2018 Ensage.
// </copyright>

namespace VisibleByEnemy
{
    using System.ComponentModel;

    using Ensage.SDK.Menu;

    [Menu("VisibleByEnemy")]
    public class VisibleByEnemyConfig
    {
        [Item("Allied Heroes")]
        [DefaultValue(true)]
        public bool AlliedHeroes { get; set; }

        [Item("Buildings")]
        [DefaultValue(true)]
        public bool BuildingsItem { get; set; }

        [Item("Techies Mines")]
        [DefaultValue(true)]
        public bool MinesItem { get; set; }

        [Item("Units")]
        [DefaultValue(true)]
        public bool UnitsItem { get; set; }

        [Item("Wards")]
        [DefaultValue(true)]
        public bool WardsItem { get; set; }
    }
}