// <copyright file="Rubick.cs" company="Ensage">
//    Copyright (c) 2017 Ensage.
// </copyright>

namespace Vaper.Heroes
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;
    using System.Threading;
    using System.Threading.Tasks;

    using Ensage;
    using Ensage.SDK.Abilities.Items;
    using Ensage.SDK.Abilities.npc_dota_hero_rubick;
    using Ensage.SDK.Extensions;
    using Ensage.SDK.Handlers;
    using Ensage.SDK.Helpers;
    using Ensage.SDK.Inventory.Metadata;
    using Ensage.SDK.Menu;

    using log4net;

    using PlaySharp.Toolkit.Helper.Annotations;
    using PlaySharp.Toolkit.Logging;

    using SharpDX;

    using Vaper.OrbwalkingModes;
    using Vaper.OrbwalkingModes.Combo;

    public class AbilityStolenInfo
    {
        public AbilityStolenInfo(Ability ability)
        {
            this.AbilityId = ability.Id;
            this.GameTime = Game.GameTime;
            this.Cooldown = ability.Cooldown;

            if (this.Cooldown <= 0)
            {
                this.Cooldown = ability.CooldownLength;
            }
        }

        public AbilityId AbilityId { get; private set; }

        public float Cooldown { get; private set; }

        public float GameTime { get; private set; }

        public void Update(Ability ability)
        {
            this.AbilityId = ability.Id;
            this.GameTime = Game.GameTime;
            this.Cooldown = ability.Cooldown;

            if (this.Cooldown <= 0)
            {
                this.Cooldown = ability.CooldownLength;
            }
        }
    }

    [PublicAPI]
    [ExportHero(HeroId.npc_dota_hero_rubick)]
    public class Rubick : BaseHero
    {
        public static readonly Vector3[] CliffPositions =
            {
                new Vector3(-4357.313f, -1281.781f, 664f),
                new Vector3(-4616.688f, -1319.156f, 512f),
                new Vector3(-4064f, -1312f, 512f),
                new Vector3(-3104f, 800f, 512f),
                new Vector3(-2784.938f, 800f, 512f),
                new Vector3(-3569.875f, 3347.156f, 384f),
                new Vector3(-3868.75f, 3332.594f, 384f),
                new Vector3(-1299.406f, 2814.719f, 528f),
                new Vector3(1019.5f, 4593.438f, 656f),
                new Vector3(-1017.313f, -4601.125f, 664f),
                new Vector3(1788.094f, -2794.969f, 536f),
                new Vector3(5119.875f, -752.5f, 656f),
                new Vector3(5121.75f, 774.0313f, 656f),
                new Vector3(-5116.188f, 2072.469f, 664f)
            };

        private static readonly ILog Log = AssemblyLogs.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private static readonly AbilityId[] PriorityAbilityIds =
            {
                // ultimates
                AbilityId.enigma_black_hole,
                AbilityId.magnataur_reverse_polarity,
                AbilityId.luna_eclipse,
                AbilityId.earthshaker_echo_slam,
                AbilityId.juggernaut_omni_slash,
                AbilityId.death_prophet_exorcism,
                AbilityId.warlock_rain_of_chaos,
                AbilityId.zuus_thundergods_wrath,
                AbilityId.skywrath_mage_mystic_flare,
                AbilityId.queenofpain_sonic_wave,
                AbilityId.obsidian_destroyer_sanity_eclipse,
                AbilityId.venomancer_poison_nova,
                AbilityId.doom_bringer_doom,
                AbilityId.crystal_maiden_freezing_field,
                AbilityId.dark_seer_wall_of_replica,
                AbilityId.omniknight_guardian_angel,
                AbilityId.razor_eye_of_the_storm,
                AbilityId.antimage_mana_void,
                AbilityId.alchemist_chemical_rage,
                AbilityId.bane_fiends_grip,
                AbilityId.kunkka_ghostship,
                AbilityId.necrolyte_reapers_scythe,
                AbilityId.phoenix_supernova,
                AbilityId.puck_dream_coil,
                AbilityId.winter_wyvern_winters_curse,
                AbilityId.pugna_life_drain,

                // special
                AbilityId.pudge_meat_hook,

                // disables
                AbilityId.earthshaker_fissure,
                AbilityId.lion_impale,
                AbilityId.sandking_burrowstrike,
                AbilityId.tiny_avalanche,
                AbilityId.sven_storm_bolt,
                AbilityId.skeleton_king_hellfire_blast,
                AbilityId.vengefulspirit_magic_missile,
                AbilityId.chaos_knight_chaos_bolt,
                AbilityId.slardar_slithereen_crush,

                // others
                AbilityId.spirit_breaker_charge_of_darkness,
                AbilityId.spirit_breaker_nether_strike,
            };

        private readonly Dictionary<Hero, Ability> castedAbilities = new Dictionary<Hero, Ability>(5);

        private readonly HashSet<AbilityStolenInfo> stolenInfos = new HashSet<AbilityStolenInfo>();

        private Tuple<Ability, float> nextStealAbility;

        [ItemBinding]
        public item_blink Blink { get; private set; }

        public MenuItem<bool> CliffItem { get; private set; }

        [ItemBinding]
        public item_cyclone Euls { get; private set; }

        public rubick_fade_bolt FadeBolt { get; private set; }

        [ItemBinding]
        public item_force_staff ForceStaff { get; private set; }

        [ItemBinding]
        public item_glimmer_cape GlimmerCape { get; private set; }

        public rubick_spell_steal Steal { get; private set; }

        public Ability StolenAbility
        {
            get
            {
                return this.Owner.Spellbook.SpellD;
            }
        }

        public rubick_telekinesis Telekinesis { get; private set; }

        public TaskHandler UpdateHandler { get; private set; }

        protected override ComboOrbwalkingMode GetComboOrbwalkingMode()
        {
            return new RubickOrbwalker(this);
        }

        protected override void OnActivate()
        {
            base.OnActivate();

            this.castedAbilities.Clear();
            this.stolenInfos.Clear();

            this.Telekinesis = this.Ensage.AbilityFactory.GetAbility<rubick_telekinesis>();
            this.FadeBolt = this.Ensage.AbilityFactory.GetAbility<rubick_fade_bolt>();
            this.Steal = this.Ensage.AbilityFactory.GetAbility<rubick_spell_steal>();

            var factory = this.Menu.Hero.Factory;
            this.CliffItem = factory.Item("Put on cliff", true);

            AbilityDetector.AbilityCasted += this.AbilityCasted;
            Drawing.OnDraw += this.OnDraw;

            this.UpdateHandler = UpdateManager.Run(this.OnUpdate);
        }

        protected override void OnDeactivate()
        {
            Drawing.OnDraw -= this.OnDraw;
            AbilityDetector.AbilityCasted -= this.AbilityCasted;
            this.UpdateHandler.Cancel();

            base.OnDeactivate();
        }

        protected override async Task OnKillsteal(CancellationToken token)
        {
            if (Game.IsPaused || !this.Owner.IsAlive || !this.FadeBolt.CanBeCasted)
            {
                await Task.Delay(125, token);
                return;
            }

            var killstealTarget = EntityManager<Hero>.Entities.FirstOrDefault(
                x => x.IsAlive
                     && (x.Team != this.Owner.Team)
                     && !x.IsIllusion
                     && this.FadeBolt.CanHit(x)
                     && !x.IsLinkensProtected()
                     && (this.FadeBolt.GetDamage(x) > x.Health));

            if (killstealTarget != null)
            {
                if (this.FadeBolt.UseAbility(killstealTarget))
                {
                    var castDelay = this.FadeBolt.GetCastDelay(killstealTarget);
                    await this.AwaitKillstealDelay(castDelay, token);
                }
            }

            await Task.Delay(125, token);
        }

        private void AbilityCasted(Unit sender, AbilityEventArgs e)
        {
            var hero = sender as Hero;
            if ((hero != null) && sender.IsEnemy(this.Owner) && !sender.IsIllusion)
            {
                Log.Debug($"{sender.Name} casted {e.Ability.Name}");
                this.castedAbilities[hero] = e.Ability;
            }
        }

        private int GetAbilityPriority(Ability ability)
        {
            for (var i = 0; i < PriorityAbilityIds.Length; i++)
            {
                if (PriorityAbilityIds[i] == ability.Id)
                {
                    return i;
                }
            }

            // TODO: mb better logic for other spells
            var priority = PriorityAbilityIds.Length + 1;
            if (ability.AbilityType != AbilityType.Ultimate)
            {
                priority += 10;
                if (!this.Owner.HasAghanimsScepter())
                {
                    return int.MaxValue;
                }
            }

            return priority;
        }

        private bool HasHigherPriority(Ability ability, Ability testAbility)
        {
            return this.GetAbilityPriority(ability) > this.GetAbilityPriority(testAbility);
        }

        private void OnDraw(EventArgs args)
        {
            if (this.nextStealAbility == null)
            {
                return;
            }

            var time = Game.GameTime;
            if ((time - this.nextStealAbility.Item2) > 2.0f)
            {
                this.nextStealAbility = null;
                return;
            }

            Vector2 screenPos;
            if (Drawing.WorldToScreen(this.Owner.Position + new Vector3(0, 0, this.Owner.HealthBarOffset), out screenPos))
            {
                screenPos += new Vector2(0, -65);
                try
                {
                    var texture = Drawing.GetTexture($"materials/ensage_ui/spellicons/{this.nextStealAbility.Item1.Name}.vmat");
                    Drawing.DrawRect(screenPos, new Vector2(32, 32), texture);
                }
                catch (DotaTextureNotFoundException e)
                {
                    Drawing.DrawText($"Stealing {this.nextStealAbility.Item1.Name}", screenPos, Color.Yellow, FontFlags.DropShadow | FontFlags.AntiAlias);
                }
            }
        }

        private async Task OnUpdate(CancellationToken token)
        {
            var heroesToRemove = this.castedAbilities.Keys.Where(hero => !hero.IsValid || !hero.IsVisible || !hero.IsAlive).ToList();
            foreach (var hero in heroesToRemove)
            {
                this.castedAbilities.Remove(hero);
            }

            // Log.Debug($"{this.Owner.Position.X}f, {this.Owner.Position.Y}f, {this.Owner.Position.Z}f ");
            if (!this.Owner.IsChanneling())
            {
                if (!this.Owner.IsInvisible() && this.Steal.CanBeCasted)
                {
                    // get best possible steal target
                    var time = Game.GameTime;
                    Ability bestSteal = null;
                    foreach (var castedAbility in this.castedAbilities)
                    {
                        var hero = castedAbility.Key;
                        if (!this.Steal.CanHit(hero))
                        {
                            continue;
                        }

                        var ability = castedAbility.Value;
                        if ((ability.Id != this.StolenAbility.Id) && ((bestSteal == null) || this.HasHigherPriority(bestSteal, ability)))
                        {
                            // test if ability was already stolen and is still on cd
                            var info = this.stolenInfos.FirstOrDefault(x => x.AbilityId == ability.Id);
                            if ((info == null) || ((time - info.GameTime) > info.Cooldown))
                            {
                                if (info != null)
                                {
                                    Log.Debug($"info: {time} - {info.GameTime} = {time - info.GameTime} > {info.Cooldown}");
                                }

                                bestSteal = ability;
                            }
                        }
                    }

                    if ((bestSteal != null) && ((this.StolenAbility == null) || (this.StolenAbility.Cooldown > 0) || this.HasHigherPriority(this.StolenAbility, bestSteal)))
                    {
                        if (this.GetAbilityPriority(bestSteal) != int.MaxValue)
                        {
                            if ((this.StolenAbility != null) && !this.StolenAbility.AbilityBehavior.HasFlag(AbilityBehavior.Passive))
                            {
                                var info = this.stolenInfos.FirstOrDefault(x => x.AbilityId == this.StolenAbility.Id);
                                if (info != null)
                                {
                                    info.Update(this.StolenAbility);
                                    Log.Debug($"updating info for {this.StolenAbility.Name}");
                                }
                                else
                                {
                                    this.stolenInfos.Add(new AbilityStolenInfo(this.StolenAbility));
                                    Log.Debug($"adding info for {this.StolenAbility.Name}");
                                }
                            }

                            this.nextStealAbility = new Tuple<Ability, float>(bestSteal, time);

                            var target = (Unit)bestSteal.Owner;
                            Log.Debug($"stealing {bestSteal} from {target.Name}!");
                            this.Steal.UseAbility(target);
                            await Task.Delay(this.Steal.GetHitTime(target) + 250, token);
                        }
                    }
                }

                if (this.CliffItem && this.Telekinesis.CanBeCasted && (Game.ShortLevelName == "start"))
                {
                    var radius = this.Telekinesis.LandAbility.Radius;

                    var availableCliffs = CliffPositions.Where(x => x.Distance(this.Owner.Position) < (this.Telekinesis.CastRange + radius));
                    foreach (var cliff in availableCliffs)
                    {
                        var enemy = EntityManager<Hero>.Entities.FirstOrDefault(
                            x => x.IsVisible
                                 && x.IsAlive
                                 && this.Owner.IsEnemy(x)
                                 && !x.IsIllusion
                                 && this.Telekinesis.CanHit(x)
                                 && (x.Distance2D(cliff) <= radius));

                        if (enemy != null)
                        {
                            this.Telekinesis.UseAbility(enemy);
                            await Task.Delay(this.Telekinesis.GetCastDelay(enemy) + 500, token);
                            this.Telekinesis.LandAbility.UseAbility(cliff);
                            await Task.Delay(this.Telekinesis.LandAbility.GetCastDelay(enemy), token);
                            break;
                        }
                    }
                }
            }

            await Task.Delay(125, token);
        }
    }
}