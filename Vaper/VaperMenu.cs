// <copyright file="VaperMenu.cs" company="Ensage">
//    Copyright (c) 2017 Ensage.
// </copyright>

namespace Vaper
{
    using System;

    using Ensage;
    using Ensage.Common.Menu;
    using Ensage.SDK.Menu;

    public class VaperMenu : IDisposable
    {
        private bool disposed;

        public VaperMenu(HeroId id)
        {
            this.Factory = MenuFactory.Create("Vaper");
            this.General = new GeneralMenu(this.Factory);
            this.Hero = new HeroMenu(this.Factory, id.ToString());
        }

        public MenuFactory Factory { get; }

        public GeneralMenu General { get; set; }

        public HeroMenu Hero { get; set; }

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

        public class GeneralMenu : IDisposable
        {
            private bool disposed;

            public GeneralMenu(MenuFactory factory)
            {
                this.Factory = factory.Menu("General");

                this.Killsteal = this.Factory.Item("Killsteal", true);
                this.Killsteal.Item.Tooltip = "Enables killstealing.";

                this.DrawTargetLine = this.Factory.Item("Draw Target Line", true);
                this.DrawTargetLine.Item.Tooltip = "Draws a line to your target";

                this.LockTarget = this.Factory.Item("Lock Target", true);
                this.LockTarget.Item.Tooltip = "Keep your target until it's dead or you release the key.";

                this.KiteMode = this.Factory.Item("Kite mode", true);
                this.KiteMode.Item.Tooltip = "Only attack when in attack range.";

                this.ComboKey = this.Factory.Item("Combo Key", new KeyBind(32));
            }

            public MenuItem<KeyBind> ComboKey { get; }

            public MenuItem<bool> DrawTargetLine { get; }

            public MenuFactory Factory { get; }

            public MenuItem<bool> Killsteal { get; }

            public MenuItem<bool> KiteMode { get; }

            public MenuItem<bool> LockTarget { get; }

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

        public class HeroMenu : IDisposable
        {
            private bool disposed;

            public HeroMenu(MenuFactory factory, string heroName)
            {
                this.Factory = factory.MenuWithTexture(Game.Localize(heroName), heroName);
            }

            public MenuFactory Factory { get; }

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
}