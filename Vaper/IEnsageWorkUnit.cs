// <copyright file="IEnsageWorkUnit.cs" company="Ensage">
//    Copyright (c) 2017 Ensage.
// </copyright>

namespace Vaper
{
    using Ensage.SDK.Abilities;
    using Ensage.SDK.Input;
    using Ensage.SDK.Inventory;
    using Ensage.SDK.Orbwalker;
    using Ensage.SDK.Renderer;
    using Ensage.SDK.Renderer.Particle;
    using Ensage.SDK.Service;
    using Ensage.SDK.TargetSelector;

    public interface IEnsageWorkUnit
    {
        AbilityFactory AbilityFactory { get; }

        IServiceContext Context { get; }

        IInputManager Input { get; }

        IInventoryManager Inventory { get; }

        IOrbwalkerManager Orbwalker { get; }

        IParticleManager Particle { get; }

        IRendererManager Renderer { get; }

        ITargetSelectorManager TargetSelector { get; }
    }
}