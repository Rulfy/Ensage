// <copyright file="VisibleByEnemyConfig.cs" company="Ensage">
//    Copyright (c) 2017 Ensage.
// </copyright>

namespace VisibleByEnemy
{
    using System;

    using Ensage.SDK.Menu;

    internal class VisibleByEnemyConfig : IDisposable
    {
        public readonly MenuFactory Factory;

        public MenuItem<bool> AlliedHeroes;

        public MenuItem<bool> BuildingsItem;

        public MenuItem<bool> MinesItem;

        public MenuItem<bool> UnitsItem;

        public MenuItem<bool> WardsItem;

        private bool disposed;

        public VisibleByEnemyConfig()
        {
            this.Factory = MenuFactory.Create("VisibleByEnemy");
            this.AlliedHeroes = this.Factory.Item("Allied Heroes", true);
            this.WardsItem = this.Factory.Item("Wards", true);
            this.MinesItem = this.Factory.Item("Techies Mines", true);
            this.UnitsItem = this.Factory.Item("Units", true);
            this.BuildingsItem = this.Factory.Item("Buildings", true);
        }

        public void Dispose()
        {
            this.Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (this.disposed)
            {
                return;
            }

            if (disposing)
            {
                this.Factory.Dispose();
            }

            this.disposed = true;
        }
    }
}