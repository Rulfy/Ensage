// <copyright file="FailSwitchConfig.cs" company="Ensage">
//    Copyright (c) 2018 Ensage.
// </copyright>

namespace FailSwitch
{
    using System.ComponentModel;

    using Ensage.SDK.Menu;
    using Ensage.SDK.Menu.Items;
    using Ensage.SDK.Renderer;

    [Menu("FailSwitch")]
    public class FailSwitchConfig
    {
        public FailSwitchConfig(IRendererManager renderer)
        {
            renderer.TextureManager.LoadFromDota("enigma_black_hole", @"resource\flash3\images\spellicons\enigma_black_hole.png");
            renderer.TextureManager.LoadFromDota("puck_dream_coil", @"resource\flash3\images\spellicons\puck_dream_coil.png");
            renderer.TextureManager.LoadFromDota("obsidian_destroyer_sanity_eclipse", @"resource\flash3\images\spellicons\obsidian_destroyer_sanity_eclipse.png");
            renderer.TextureManager.LoadFromDota("faceless_void_chronosphere", @"resource\flash3\images\spellicons\faceless_void_chronosphere.png");

            renderer.TextureManager.LoadFromDota("magnataur_reverse_polarity", @"resource\flash3\images\spellicons\magnataur_reverse_polarity.png");
            renderer.TextureManager.LoadFromDota("tidehunter_ravage", @"resource\flash3\images\spellicons\tidehunter_ravage.png");
            renderer.TextureManager.LoadFromDota("axe_berserkers_call", @"resource\flash3\images\spellicons\axe_berserkers_call.png");
            renderer.TextureManager.LoadFromDota("centaur_hoof_stomp", @"resource\flash3\images\spellicons\centaur_hoof_stomp.png");
            renderer.TextureManager.LoadFromDota("slardar_slithereen_crush", @"resource\flash3\images\spellicons\slardar_slithereen_crush.png");
            renderer.TextureManager.LoadFromDota("earthshaker_echo_slam", @"resource\flash3\images\spellicons\earthshaker_echo_slam.png");

            renderer.TextureManager.LoadFromDota("doom_bringer_doom", @"resource\flash3\images\spellicons\doom_bringer_doom.png");
            renderer.TextureManager.LoadFromDota("lion_finger_of_death", @"resource\flash3\images\spellicons\lion_finger_of_death.png");
            renderer.TextureManager.LoadFromDota("lina_laguna_blade", @"resource\flash3\images\spellicons\lina_laguna_blade.png");
            renderer.TextureManager.LoadFromDota("antimage_mana_void", @"resource\flash3\images\spellicons\antimage_mana_void.png");
            renderer.TextureManager.LoadFromDota("legion_commander_duel", @"resource\flash3\images\spellicons\legion_commander_duel.png");
            renderer.TextureManager.LoadFromDota("bloodseeker_rupture", @"resource\flash3\images\spellicons\bloodseeker_rupture.png");
            renderer.TextureManager.LoadFromDota("terrorblade_sunder", @"resource\flash3\images\spellicons\terrorblade_sunder.png");
            renderer.TextureManager.LoadFromDota("bane_fiends_grip", @"resource\flash3\images\spellicons\bane_fiends_grip.png");
        }

        [Item("AoE Abilities")]
        public ImageToggler AreaSpells { get; set; } = new ImageToggler(
            true,
            "enigma_black_hole",
            "puck_dream_coil",
            "obsidian_destroyer_sanity_eclipse",
            "faceless_void_chronosphere");

        [Item("No Target Abilities")]
        public ImageToggler NoTargetSpells { get; set; } = new ImageToggler(
            true,
            "magnataur_reverse_polarity",
            "tidehunter_ravage",
            "axe_berserkers_call",
            "centaur_hoof_stomp",
            "slardar_slithereen_crush",
            "earthshaker_echo_slam");

        [Item("Target Abilities")]
        public ImageToggler TargetSpells { get; set; } = new ImageToggler(
            true,
            "doom_bringer_doom",
            "lion_finger_of_death",
            "lina_laguna_blade",
            "antimage_mana_void",
            "legion_commander_duel",
            "bloodseeker_rupture",
            "terrorblade_sunder",
            "bane_fiends_grip");

        [Item("Fix Target Ability")]
        [Tooltip("Tries to find a better target when failing targeted-abilities.")]
        [DefaultValue(true)]
        public bool FindRealTarget { get; set; }

        [Item("Target Search Range")]
        [Tooltip("Searches for a better target in this range")]
        public Slider<float> TargetSearchRange { get; set; } = new Slider<float>(500, 0, 1000);

        [Item("Toggle Power Treads")]
        [DefaultValue(true)]
        public bool TogglePowerTreads { get; set; }

        [Item("Ping Guard")]
        [Tooltip("Won't toggle boots back on certain abilities when having a ping higher than this value")]
        public Slider<float> PingGuard { get; set; } = new Slider<float>(50, 0, 150);
    }
}