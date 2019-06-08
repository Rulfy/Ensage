// <copyright file="Program.cs" company="Ensage">
//    Copyright (c) 2018 Ensage.
// </copyright>

namespace VisibleByEnemy
{
    using System;
    using System.ComponentModel.Composition;
    using System.Linq;

    using Ensage;
    using Ensage.SDK.Helpers;
    using Ensage.SDK.Renderer.Particle;
    using Ensage.SDK.Service;
    using Ensage.SDK.Service.Metadata;

    [ExportPlugin("VisibleByEnemy", StartupMode.Auto)]
    public class Program : Plugin
    {
        private readonly IServiceContext context;

        private readonly Unit owner;

        private readonly Lazy<IParticleManager> particleManager;

        private VisibleByEnemyConfig config;

        [ImportingConstructor]
        public Program([Import] IServiceContext context, [Import] Lazy<IParticleManager> particleManager)
        {
            this.owner = context.Owner;
            this.context = context;
            this.particleManager = particleManager;
        }

        protected override void OnActivate()
        {
            this.config = new VisibleByEnemyConfig();
            this.context.MenuManager.RegisterMenu(this.config);
            UpdateManager.Subscribe(this.LoopEntities, 250);
        }

        protected override void OnDeactivate()
        {
            UpdateManager.Unsubscribe(this.LoopEntities);
            this.context.MenuManager.DeregisterMenu(this.config);
        }

        private static bool IsMine(Entity sender)
        {
            return sender.ClassId == ClassId.CDOTA_NPC_TechiesMines;
        }

        private static bool IsUnit(Unit sender)
        {
            return !(sender is Hero)
                   && !(sender is Building)
                   && (sender.ClassId != ClassId.CDOTA_BaseNPC_Creep_Lane && sender.ClassId != ClassId.CDOTA_BaseNPC_Creep_Siege || sender.IsControllable)
                   && sender.ClassId != ClassId.CDOTA_NPC_TechiesMines
                   && sender.ClassId != ClassId.CDOTA_NPC_Observer_Ward
                   && sender.ClassId != ClassId.CDOTA_NPC_Observer_Ward_TrueSight;
        }

        private static bool IsWard(Entity sender)
        {
            return sender.ClassId == ClassId.CDOTA_NPC_Observer_Ward || sender.ClassId == ClassId.CDOTA_NPC_Observer_Ward_TrueSight;
        }

        private void HandleEffect(Unit unit, bool visible)
        {
            if (visible && unit.IsAlive)
            {
                this.particleManager.Value.AddOrUpdate(unit, $"unit_{unit.Handle}", "particles/items_fx/aura_shivas.vpcf", ParticleAttachment.AbsOriginFollow);
                this.particleManager.Value.Release($"unit_{unit.Handle}");
            }
            else
            {
                this.particleManager.Value.Remove($"unit_{unit.Handle}");
            }
        }

        private void LoopEntities()
        {
            if (this.config.AlliedHeroes)
            {
                foreach (var hero in EntityManager<Hero>.Entities.Where(x => x.Team == this.owner.Team))
                {
                    this.HandleEffect(hero, hero.IsVisibleToEnemies);
                }
            }

            if (this.config.BuildingsItem)
            {
                foreach (var building in EntityManager<Building>.Entities.Where(x => x.Team == this.owner.Team))
                {
                    this.HandleEffect(building, building.IsVisibleToEnemies);
                }
            }

            var units = EntityManager<Unit>.Entities.Where(x => x.Team == this.owner.Team).ToList();
            if (this.config.WardsItem)
            {
                foreach (var ward in units.Where(IsWard))
                {
                    this.HandleEffect(ward, ward.IsVisibleToEnemies);
                }
            }

            if (this.config.MinesItem)
            {
                foreach (var mine in units.Where(IsMine))
                {
                    this.HandleEffect(mine, mine.IsVisibleToEnemies);
                }
            }

            if (this.config.UnitsItem)
            {
                foreach (var unit in units.Where(IsUnit))
                {
                    this.HandleEffect(unit, unit.IsVisibleToEnemies);
                }
            }
        }
    }
}