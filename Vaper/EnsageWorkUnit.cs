// <copyright file="EnsageWorkUnit.cs" company="Ensage">
//    Copyright (c) 2017 Ensage.
// </copyright>

namespace Vaper
{
    using System;
    using System.ComponentModel.Composition;

    using Ensage.SDK.Abilities;
    using Ensage.SDK.Input;
    using Ensage.SDK.Inventory;
    using Ensage.SDK.Orbwalker;
    using Ensage.SDK.Renderer;
    using Ensage.SDK.Renderer.Particle;
    using Ensage.SDK.Service;
    using Ensage.SDK.TargetSelector;

    [Export(typeof(IEnsageWorkUnit))]
    public class EnsageWorkUnit : IEnsageWorkUnit
    {
        private readonly Lazy<IInputManager> inputManager;

        private readonly Lazy<IInventoryManager> inventoryManager;

        private readonly Lazy<AbilityFactory> lazyAbilityFactory;

        private readonly Lazy<IServiceContext> lazyContext;

        private readonly Lazy<IOrbwalkerManager> orbwalkerManager;

        private readonly Lazy<IParticleManager> particleManager;

        private readonly Lazy<IRendererManager> rendererManager;

        private readonly Lazy<ITargetSelectorManager> targetSelectorManager;

        private AbilityFactory abilityFactory;

        private IServiceContext context;

        private IInputManager input;

        private IInventoryManager inventory;

        private IOrbwalkerManager orbwalker;

        private IParticleManager particle;

        private IRendererManager renderer;

        private ITargetSelectorManager targetSelector;

        [ImportingConstructor]
        public EnsageWorkUnit(
            [Import] Lazy<IServiceContext> context,
            [Import] Lazy<IOrbwalkerManager> orbwalkerManager,
            [Import] Lazy<IInputManager> inputManager,
            [Import] Lazy<IInventoryManager> inventoryManager,
            [Import] Lazy<IParticleManager> particleManager,
            [Import] Lazy<IRendererManager> rendererManager,
            [Import] Lazy<ITargetSelectorManager> targetSelectorManager,
            [Import] Lazy<AbilityFactory> lazyAbilityFactory)
        {
            this.lazyContext = context;
            this.orbwalkerManager = orbwalkerManager;
            this.inventoryManager = inventoryManager;
            this.particleManager = particleManager;
            this.rendererManager = rendererManager;
            this.targetSelectorManager = targetSelectorManager;
            this.lazyAbilityFactory = lazyAbilityFactory;
            this.inputManager = inputManager;
        }

        public AbilityFactory AbilityFactory
        {
            get
            {
                if (this.abilityFactory == null)
                {
                    this.abilityFactory = this.lazyAbilityFactory.Value;
                }

                return this.abilityFactory;
            }
        }

        public IServiceContext Context
        {
            get
            {
                if (this.context == null)
                {
                    this.context = this.lazyContext.Value;
                }

                return this.context;
            }
        }

        public IInputManager Input
        {
            get
            {
                if (this.input == null)
                {
                    this.input = this.inputManager.Value;
                    this.input.Activate();
                }

                return this.input;
            }
        }

        public IInventoryManager Inventory
        {
            get
            {
                if (this.inventory == null)
                {
                    this.inventory = this.inventoryManager.Value;
                    this.inventory.Activate();
                }

                return this.inventory;
            }
        }

        public IOrbwalkerManager Orbwalker
        {
            get
            {
                if (this.orbwalker == null)
                {
                    this.orbwalker = this.orbwalkerManager.Value;
                    this.orbwalker.Activate();
                }

                return this.orbwalker;
            }
        }

        public IParticleManager Particle
        {
            get
            {
                if (this.particle == null)
                {
                    this.particle = this.particleManager.Value;
                }

                return this.particle;
            }
        }

        public IRendererManager Renderer
        {
            get
            {
                if (this.renderer == null)
                {
                    this.renderer = this.rendererManager.Value;
                }

                return this.renderer;
            }
        }

        public ITargetSelectorManager TargetSelector
        {
            get
            {
                if (this.targetSelector == null)
                {
                    this.targetSelector = this.targetSelectorManager.Value;
                    this.targetSelector.Activate();
                }

                return this.targetSelector;
            }
        }
    }
}