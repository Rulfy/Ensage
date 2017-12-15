// <copyright file="Frosty.cs" company="Ensage">
//    Copyright (c) 2017 Ensage.
// </copyright>

namespace Frosty
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel.Composition;
    using System.Linq;
    using System.Reflection;
    using System.Threading;
    using System.Threading.Tasks;

    using Ensage;
    using Ensage.SDK.Abilities.npc_dota_hero_invoker;
    using Ensage.SDK.Extensions;
    using Ensage.SDK.Geometry;
    using Ensage.SDK.Handlers;
    using Ensage.SDK.Helpers;
    using Ensage.SDK.Service;
    using Ensage.SDK.Service.Metadata;

    using log4net;

    using PlaySharp.Toolkit.Logging;

    using SharpDX;

    using Color = System.Drawing.Color;

    internal enum MinigameState
    {
        Unknown,

        Unsupported,

        PartialSupported,

        Supported
    }

    [ExportPlugin("Frosty")]
    public class Frosty : Plugin
    {
        private static readonly ILog Log = AssemblyLogs.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private readonly IServiceContext context;

        private Hero hero;

        private bool loaded;

        private TaskHandler mainLoopHandler;

        private MinigameState minigameState = MinigameState.Unknown;

        [ImportingConstructor]
        public Frosty([Import] IServiceContext context)
        {
            this.context = context;
        }

        protected override void OnActivate()
        {
            if (!Game.IsEventGame || Game.LevelName != "maps/frostivus_festival.vpk")
            {
                return;
            }

            this.loaded = true;
            this.context.Renderer.Draw += this.OnDraw;
            Entity.OnParticleEffectAdded += this.Entity_OnParticleEffectAdded;
            this.mainLoopHandler = UpdateManager.Run(this.MainLoop);
        }

        protected override void OnDeactivate()
        {
            if (!this.loaded)
            {
                return;
            }

            Entity.OnParticleEffectAdded -= this.Entity_OnParticleEffectAdded;
            ObjectManager.OnAddLinearProjectile += this.ObjectManager_OnAddLinearProjectile;
            this.context.Renderer.Draw -= this.OnDraw;
            this.mainLoopHandler?.Cancel();
        }

        private async Task ArrowPractice()
        {
            this.minigameState = MinigameState.PartialSupported;

            var bells = new[] { new Vector3(-3648, -4578, 128), new Vector3(-3069, -4570, 128), new Vector3(-4659, -4578, 128) };
            var myPos = this.hero.Position;
            var pengus = EntityManager<Entity>.Entities.Where(x => x.IsAlive && x.Name == "chain_frost_game_penguin").ToList();

            foreach (var bell in bells)
            {
                var dir = (bell - myPos).Normalized();

                var polygon = new Polygon();
                polygon.Add(myPos);

                polygon.Add(myPos + (dir.Rotated(this.DegreeToRadian(-90f)) * (115f / 2f)));
                polygon.Add(myPos + (dir.Rotated(this.DegreeToRadian(90f)) * (115f / 2f)));
                polygon.Add(bell + (dir.Rotated(this.DegreeToRadian(-90f)) * (115f / 2f)));
                polygon.Add(bell + (dir.Rotated(this.DegreeToRadian(90f)) * (115f / 2f)));

                if (pengus.All(x => !polygon.IsInside(x.Position)))
                {
                    this.hero.Spellbook.Spell1.UseAbility(bell);
                    break;
                }
            }
        }

        private float DegreeToRadian(float angle)
        {
            return (float)((Math.PI * angle) / 180.0f);
        }

        private void Entity_OnParticleEffectAdded(Entity sender, ParticleEffectAddedEventArgs args)
        {
            Log.Debug($"ParticleEffectAdded {args.Name}");
        }

        private async Task FollowTheLeader()
        {
            this.minigameState = MinigameState.Supported;

            var modifier = this.hero.GetModifierByName("modifier_invoked_spell_to_cast");
            if (modifier == null)
            {
                Log.Debug($"no modifier found");
                return;
            }

            Log.Debug($"invoke: {modifier.TextureName}");
            switch (modifier.TextureName)
            {
                case "invoker_alacrity":
                    this.context.AbilityFactory.GetAbility<invoker_alacrity>().Invoke();
                    break;
                case "invoker_chaos_meteor":
                    this.context.AbilityFactory.GetAbility<invoker_chaos_meteor>().Invoke();
                    break;
                case "invoker_cold_snap":
                    this.context.AbilityFactory.GetAbility<invoker_cold_snap>().Invoke();
                    break;
                case "invoker_deafening_blast":
                    this.context.AbilityFactory.GetAbility<invoker_deafening_blast>().Invoke();
                    break;
                case "invoker_emp":
                    this.context.AbilityFactory.GetAbility<invoker_emp>().Invoke();
                    break;
                case "invoker_forge_spirit":
                    this.context.AbilityFactory.GetAbility<invoker_forge_spirit>().Invoke();
                    break;
                case "invoker_ghost_walk":
                    this.context.AbilityFactory.GetAbility<invoker_ghost_walk>().Invoke();
                    break;
                case "invoker_ice_wall":
                    this.context.AbilityFactory.GetAbility<invoker_ice_wall>().Invoke();
                    break;
                case "invoker_sun_strike":
                    this.context.AbilityFactory.GetAbility<invoker_sun_strike>().Invoke();
                    break;
                case "invoker_tornado":
                    this.context.AbilityFactory.GetAbility<invoker_tornado>().Invoke();
                    break;
            }
        }

        private async Task MainLoop(CancellationToken token)
        {
            foreach (var obj in ObjectManager.LocalPlayer.Selection.OfType<Unit>())
            {
                Log.Debug($"Obj: {obj.Name} | {obj.Team} | {obj.Animation.Name} | {obj.MoveCapability}");
                foreach (var modifier in obj.Modifiers)
                {
                    Log.Debug($"\t{modifier.Name} | {modifier.TextureName}");
                }

                foreach (var spellbookSpell in obj.Spellbook.Spells)
                {
                    Log.Debug($"\t\t{spellbookSpell.Name} | {spellbookSpell.AbilitySlot}");
                }
            }

            var query = ObjectManager.LocalPlayer.QueryUnit as Unit;
            if (query != null)
            {
                Log.Debug($"query: {query.Name} | {query.Team} | {query.Animation.Name}");
                foreach (var modifier in query.Modifiers)
                {
                    Log.Debug($"\t{modifier.Name} | {modifier.TextureName}");
                }
            }

            this.minigameState = MinigameState.Unknown;

            this.hero = ObjectManager.LocalHero;
            if (this.hero == null || !this.hero.IsAlive)
            {
                Log.Debug($"{Game.MousePosition}");

                await Task.Delay(250, token);
                return;
            }

            Log.Debug($"{Game.MousePosition} - {this.hero.Position} -> {Game.MousePosition.Distance(this.hero.Position)}");

            this.minigameState = MinigameState.Unsupported;

            // https://dota2.gamepedia.com/Frostivus_2017
            var heroId = this.hero.HeroId;
            switch (heroId)
            {
                case HeroId.npc_dota_hero_weaver:
                    await this.RedLightGreenLight();
                    break;

                case HeroId.npc_dota_hero_invoker:
                    if (this.hero.HasModifier("modifier_invoked_spell_to_cast"))
                    {
                        await this.FollowTheLeader();
                    }

                    break;
                case HeroId.npc_dota_hero_queenofpain:
                    if (this.hero.HasModifier("modifier_weather_snowstorm"))
                    {
                        await this.SurviveTheWinter();
                    }

                    break;

                case HeroId.npc_dota_hero_spirit_breaker:
                    break;

                case HeroId.npc_dota_hero_techies:
                    if (this.hero.Spellbook.VisibleCount == 0)
                    {
                        await this.MinefieldSurvival();
                    }

                    break;

                case HeroId.npc_dota_hero_mirana:
                    await this.ArrowPractice();
                    break;

                default:
                    this.minigameState = MinigameState.Unsupported;
                    break;
            }

            await Task.Delay(250, token);
        }

        private async Task MinefieldSurvival()
        {
            this.minigameState = MinigameState.Supported;
            var mines = EntityManager<Entity>.Entities.Where(x => x.Name == "techies_game_remote_mine");
            var avoid = new List<Vector3>();
            foreach (var entity in mines)
            {
                avoid.Add(entity.Position);
            }
        }

        private void ObjectManager_OnAddLinearProjectile(LinearProjectileEventArgs args)
        {
            Log.Debug($"OnAddLinearProjectile {args.Projectile.StartPosition}");
        }

        private void OnDraw(object sender, EventArgs e)
        {
            var pos = new Vector2(5, Drawing.Height / 2f);
            switch (this.minigameState)
            {
                case MinigameState.Unknown:
                    this.context.Renderer.DrawText(pos, "Wait for next minigame to start???", Color.Yellow);
                    break;
                case MinigameState.Unsupported:
                    this.context.Renderer.DrawText(pos, "Unsupported", Color.Red);
                    break;
                case MinigameState.PartialSupported:
                    this.context.Renderer.DrawText(pos, "Will help you", Color.Yellow);
                    break;
                case MinigameState.Supported:
                    this.context.Renderer.DrawText(pos, "Just sit back and relax", Color.Green);
                    break;
            }
        }

        private async Task RedLightGreenLight()
        {
            this.minigameState = MinigameState.Supported;

            if (this.hero.HasModifier("modifier_rupture_damage"))
            {
                this.hero.Stop();
                return;
            }

            if (this.hero.Spellbook.Spell1.Cooldown <= 0)
            {
                this.hero.Spellbook.Spell1.UseAbility();
            }

            this.hero.Move(this.hero.Position + new Vector3(250, 0, 0));
        }

        private async Task SurviveTheWinter()
        {
            this.minigameState = MinigameState.PartialSupported;

            // var rune = EntityManager<Rune>.Entities.FirstOrDefault(x => x.Distance2D(this.hero) < 175f);
            // if (rune != null)
            // {
            // this.hero.PickUpRune(rune);
            // }
            var item = EntityManager<PhysicalItem>.Entities.FirstOrDefault(x => x.Distance2D(this.hero) < 175f);
            if (item != null)
            {
                this.hero.PickUpItem(item);
            }
        }
    }
}