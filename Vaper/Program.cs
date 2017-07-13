// <copyright file="Program.cs" company="Ensage">
//    Copyright (c) 2017 Ensage.
// </copyright>

namespace Vaper
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel.Composition;
    using System.Linq;

    using Ensage;
    using Ensage.SDK.Renderer.Particle;
    using Ensage.SDK.Service;
    using Ensage.SDK.Service.Metadata;

    [ExportPlugin("Vaper", HeroId.npc_dota_hero_axe, HeroId.npc_dota_hero_phantom_assassin, HeroId.npc_dota_hero_juggernaut, HeroId.npc_dota_hero_alchemist,
        HeroId.npc_dota_hero_crystal_maiden, HeroId.npc_dota_hero_pudge, HeroId.npc_dota_hero_undying, HeroId.npc_dota_hero_rubick)]
    public class Program : Plugin
    {
        private readonly Hero owner;

        private readonly Lazy<IParticleManager> particleManager;

        private Lazy<IHero, IHeroMetadata> hero;

        [ImportingConstructor]
        public Program([Import] IServiceContext context, [Import] Lazy<IParticleManager> particleManager)
        {
            this.particleManager = particleManager;
            this.owner = context.Owner as Hero;
        }

        [ImportMany(typeof(IHero))]
        protected IEnumerable<Lazy<IHero, IHeroMetadata>> Heros { get; set; }

        protected override void OnActivate()
        {
            this.hero = this.Heros.First(e => e.Metadata.Id == this.owner.HeroId);
            this.hero.Value.Activate();
        }

        protected override void OnDeactivate()
        {
            this.hero.Value.Deactivate();
        }
    }
}