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
        public FailSwitchConfig(IRenderManager renderer)
        {
            renderer.TextureManager.LoadAbilityFromDota("enigma_black_hole");
            renderer.TextureManager.LoadAbilityFromDota("puck_dream_coil");
            renderer.TextureManager.LoadAbilityFromDota("obsidian_destroyer_sanity_eclipse");
            renderer.TextureManager.LoadAbilityFromDota("faceless_void_chronosphere");
            renderer.TextureManager.LoadAbilityFromDota("lina_light_strike_array");
            renderer.TextureManager.LoadAbilityFromDota("magnataur_reverse_polarity");
            renderer.TextureManager.LoadAbilityFromDota("tidehunter_ravage");
            renderer.TextureManager.LoadAbilityFromDota("axe_berserkers_call");
            renderer.TextureManager.LoadAbilityFromDota("centaur_hoof_stomp");
            renderer.TextureManager.LoadAbilityFromDota("slardar_slithereen_crush");
            renderer.TextureManager.LoadAbilityFromDota("earthshaker_echo_slam");
            renderer.TextureManager.LoadAbilityFromDota("doom_bringer_doom");
            renderer.TextureManager.LoadAbilityFromDota("lion_finger_of_death");
            renderer.TextureManager.LoadAbilityFromDota("lina_laguna_blade");
            renderer.TextureManager.LoadAbilityFromDota("antimage_mana_void");
            renderer.TextureManager.LoadAbilityFromDota("legion_commander_duel");
            renderer.TextureManager.LoadAbilityFromDota("bloodseeker_rupture");
            renderer.TextureManager.LoadAbilityFromDota("terrorblade_sunder");
            renderer.TextureManager.LoadAbilityFromDota("bane_fiends_grip");
        }

        [Item("AoE Abilities")]
        public ImageToggler AreaSpells { get; set; } = new ImageToggler(
            true,
            "enigma_black_hole",
            "puck_dream_coil",
            "obsidian_destroyer_sanity_eclipse",
            "faceless_void_chronosphere",
            "lina_light_strike_array");

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