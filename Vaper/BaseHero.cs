// <copyright file="BaseHero.cs" company="Ensage">
//    Copyright (c) 2017 Ensage.
// </copyright>

namespace Vaper
{
    using System.Collections.Specialized;
    using System.ComponentModel;
    using System.ComponentModel.Composition;
    using System.Threading;
    using System.Threading.Tasks;

    using Ensage;
    using Ensage.SDK.Handlers;
    using Ensage.SDK.Helpers;
    using Ensage.SDK.Orbwalker;
    using Ensage.SDK.Service;

    public abstract class BaseHero : ControllableService, IHero
    {
        public TaskHandler KillstealHandler { get; set; }

        public VaperMenu Menu { get; private set; }

        [Import(typeof(IEnsageWorkUnit))]
        internal IEnsageWorkUnit Ensage { get; private set; }

        internal bool IsKillstealing { get; private set; }

        internal Hero Owner { get; private set; }

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

        protected abstract IOrbwalkingMode GetOrbwalkingMode();

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

            this.Ensage.Orbwalker.RegisterMode(this.GetOrbwalkingMode());

            this.KillstealHandler = UpdateManager.Run(this.OnKillsteal, true, this.Menu.General.Killsteal);

            this.Menu.General.Killsteal.PropertyChanged += this.KillstealPropertyChanged;
            this.Ensage.Inventory.CollectionChanged += this.InventoryChanged;
        }

        protected override void OnDeactivate()
        {
            this.Ensage.Inventory.CollectionChanged -= this.InventoryChanged;
            this.Menu.General.Killsteal.PropertyChanged -= this.KillstealPropertyChanged;

            this.KillstealHandler.Cancel();

            this.Menu.Dispose();
        }

        protected virtual async Task OnKillsteal(CancellationToken cancellationToken)
        {
        }
    }
}