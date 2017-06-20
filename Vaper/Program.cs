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
    using Ensage.SDK.Service;
    using Ensage.SDK.Service.Metadata;

    [ExportPlugin("Vaper", HeroId.npc_dota_hero_axe, HeroId.npc_dota_hero_phantom_assassin)]
    public class Program : Plugin
    {
        private readonly Hero owner;

        private Lazy<IHero, IHeroMetadata> hero;

        [ImportingConstructor]
        public Program([Import] IServiceContext context)
        {
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