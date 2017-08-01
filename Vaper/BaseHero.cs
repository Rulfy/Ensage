// <copyright file="BaseHero.cs" company="Ensage">
//    Copyright (c) 2017 Ensage.
// </copyright>

namespace Vaper
{
    using System;
    using System.Collections.Specialized;
    using System.ComponentModel;
    using System.ComponentModel.Composition;
    using System.Diagnostics;
    using System.Reflection;
    using System.Threading;
    using System.Threading.Tasks;

    using Ensage;
    using Ensage.SDK.Handlers;
    using Ensage.SDK.Helpers;
    using Ensage.SDK.Service;

    using log4net;

    using PlaySharp.Toolkit.Logging;

    using SharpDX;

    using Color = System.Drawing.Color;

    public abstract class BaseHero : ControllableService, IHero
    {
        private static readonly ILog Log = AssemblyLogs.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        public VaperMenu Menu { get; private set; }

        [Import(typeof(IServiceContext))]
        internal IServiceContext Context { get; private set; }

        internal bool IsKillstealing { get; private set; }

        internal Hero Owner { get; private set; }

        protected TaskHandler KillstealHandler { get; set; }

        protected ComboOrbwalkingMode ComboOrbwalkingMode { get; private set; }

        protected HarrasOrbwalkingMode HarrasOrbwalkingMode { get; private set; }

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

        protected abstract ComboOrbwalkingMode GetComboOrbwalkingMode();

        protected virtual HarrasOrbwalkingMode GetHarrasOrbwalkingMode()
        {
            return new HarrasOrbwalkingMode(this);
        }

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
            this.Context.Inventory.Attach(this);
            this.Owner = (Hero)this.Context.Owner;
            this.Menu = new VaperMenu(this.Owner.HeroId);

            this.ComboOrbwalkingMode = this.GetComboOrbwalkingMode();
            this.Context.Orbwalker.RegisterMode(this.ComboOrbwalkingMode);

            this.HarrasOrbwalkingMode = this.GetHarrasOrbwalkingMode();
            this.Context.Orbwalker.RegisterMode(this.HarrasOrbwalkingMode);

            this.KillstealHandler = UpdateManager.Run(this.OnKillsteal, true, this.Menu.General.Killsteal);
            UpdateManager.Subscribe(this.OnUpdateParticles);

            this.Menu.General.DrawTargetLine.PropertyChanged += this.DrawTargetLinePropertyChanged;
            this.Menu.General.Killsteal.PropertyChanged += this.KillstealPropertyChanged;
            this.Context.Inventory.CollectionChanged += this.InventoryChanged;

            // Intro
            this.Context.Renderer.Draw += this.IntroDraw;
            UpdateManager.Run(
                async token =>
                    {
                        var particleCount = 4;
                        var rnd = new Random();
                        var stopwatch = new Stopwatch();
                        stopwatch.Start();

                        while (stopwatch.ElapsedMilliseconds < (10 * 1000))
                        {
                            for (var i = 0; i < particleCount; ++i)
                            {
                                this.Context.Particle.AddOrUpdate(
                                    this.Owner,
                                    $"vaper_smoke_{i}",
                                    "particles/world_environmental_fx/rune_ambient_01_smoke.vpcf",
                                    ParticleAttachment.AbsOrigin,
                                    false,
                                    0,
                                    this.Owner.Position + new Vector3(rnd.NextFloat(-500, 500), rnd.NextFloat(-500, 500), 0));
                            }

                            await Task.Delay(125, token);
                        }

                        stopwatch.Stop();

                        Log.Debug($"deleting vape particles");
                        this.Context.Renderer.Draw -= this.IntroDraw;
                        for (var i = 0; i < particleCount; ++i)
                        {
                            this.Context.Particle.Remove($"vaper_smoke_{i}");
                        }
                    },
                false);
        }

        protected override void OnDeactivate()
        {
            this.Context.Inventory.Detach(this);

            this.Context.Inventory.CollectionChanged -= this.InventoryChanged;
            this.Menu.General.Killsteal.PropertyChanged -= this.KillstealPropertyChanged;
            this.Menu.General.DrawTargetLine.PropertyChanged -= this.DrawTargetLinePropertyChanged;

            UpdateManager.Unsubscribe(this.OnUpdateParticles);
            this.KillstealHandler.Cancel();

            this.Context.Orbwalker.UnregisterMode(this.HarrasOrbwalkingMode);
            this.Context.Orbwalker.UnregisterMode(this.ComboOrbwalkingMode);

            this.Menu.Dispose();
        }

        protected virtual async Task OnKillsteal(CancellationToken token)
        {
        }

        protected virtual void OnUpdateParticles()
        {
            if ((this.ComboOrbwalkingMode == null) || !this.Menu.General.DrawTargetLine)
            {
                return;
            }

            if (this.ComboOrbwalkingMode.CanExecute && (this.ComboOrbwalkingMode.CurrentTarget != null))
            {
                this.Context.Particle.DrawTargetLine(this.Owner, "vaper_targetLine", this.ComboOrbwalkingMode.CurrentTarget.Position);
            }
            else
            {
                this.Context.Particle.Remove("vaper_targetLine");
            }
        }

        private void DrawTargetLinePropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (!this.Menu.General.DrawTargetLine)
            {
                this.Context.Particle.Remove("vaper_targetLine");
            }
        }

        private void IntroDraw(object sender, EventArgs e)
        {
            Vector2 screenPos;
            if (Drawing.WorldToScreen(this.Owner.Position + new Vector3(-140, -50, 0), out screenPos))
            {
                this.Context.Renderer.DrawText(screenPos, "Vaper loaded", Color.Gold, 35);
            }
        }
    }
}