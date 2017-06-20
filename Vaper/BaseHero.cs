// <copyright file="BaseHero.cs" company="Ensage">
//    Copyright (c) 2017 Ensage.
// </copyright>

namespace Vaper
{
    using System;
    using System.Collections.Specialized;
    using System.ComponentModel;
    using System.ComponentModel.Composition;
    using System.Threading;
    using System.Threading.Tasks;

    using Ensage;
    using Ensage.SDK.Handlers;
    using Ensage.SDK.Helpers;
    using Ensage.SDK.Service;

    public abstract class BaseHero : ControllableService, IHero
    {
        private VaperOrbwalkingMode orbwalkingMode;

        public VaperMenu Menu { get; private set; }

        [Import(typeof(IEnsageWorkUnit))]
        internal IEnsageWorkUnit Ensage { get; private set; }

        internal bool IsKillstealing { get; private set; }

        internal Hero Owner { get; private set; }

        protected TaskHandler KillstealHandler { get; set; }

        protected TaskHandler UpdateParticlesHandler { get; set; }

        protected async Task AwaitKillstealDelay(int castDelay, CancellationToken token = default(CancellationToken))
        {
            this.IsKillstealing = true;
            try
            {
                await Task.Delay(castDelay, token);
            }
            finally
            {
                this.IsKillstealing = false;
            }
        }

        protected abstract VaperOrbwalkingMode GetOrbwalkingMode();

        protected virtual void InventoryChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
        }

        protected void KillstealPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (this.Menu.General.Killsteal)
            {
                this.KillstealHandler.RunAsync();
            }
            else
            {
                this.KillstealHandler.Cancel();
            }
        }

        protected override void OnActivate()
        {
            this.Owner = (Hero)this.Ensage.Context.Owner;
            this.Menu = new VaperMenu(this.Owner.HeroId);

            this.orbwalkingMode = this.GetOrbwalkingMode();
            this.Ensage.Orbwalker.RegisterMode(this.orbwalkingMode);

            this.KillstealHandler = UpdateManager.Run(this.OnKillsteal, true, this.Menu.General.Killsteal);
            this.UpdateParticlesHandler = UpdateManager.Run(this.OnUpdateParticles, true, this.Menu.General.DrawTargetLine);

            this.Menu.General.DrawTargetLine.PropertyChanged += this.DrawTargetLinePropertyChanged;
            this.Menu.General.Killsteal.PropertyChanged += this.KillstealPropertyChanged;
            this.Ensage.Inventory.CollectionChanged += this.InventoryChanged;
        }

        protected override void OnDeactivate()
        {
            this.Ensage.Inventory.CollectionChanged -= this.InventoryChanged;
            this.Menu.General.DrawTargetLine.PropertyChanged -= this.DrawTargetLinePropertyChanged;
            this.Menu.General.Killsteal.PropertyChanged -= this.KillstealPropertyChanged;

            this.KillstealHandler.Cancel();

            this.Ensage.Orbwalker.UnregisterMode(this.orbwalkingMode);

            this.Menu.Dispose();
        }

        protected virtual async Task OnKillsteal(CancellationToken token)
        {
        }

        protected virtual async Task OnUpdateParticles(CancellationToken token)
        {
            if (this.orbwalkingMode == null)
            {
                return;
            }

            if (this.orbwalkingMode.CanExecute && this.orbwalkingMode.CurrentTarget != null)
            {
                this.Ensage.Particle.DrawTargetLine(this.Owner, "vaper_targetLine", this.orbwalkingMode.CurrentTarget.Position);
            }
            else
            {
                this.Ensage.Particle.Remove("vaper_targetLine");
            }
        }

        private void DrawTargetLinePropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (this.Menu.General.DrawTargetLine)
            {
                this.UpdateParticlesHandler.RunAsync();
            }
            else
            {
                this.Ensage.Particle.Remove("vaper_targetLine");
                this.UpdateParticlesHandler.Cancel();
            }
        }
    }
}