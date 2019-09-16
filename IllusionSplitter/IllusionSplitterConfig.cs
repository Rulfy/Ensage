// <copyright file="IllusionSplitterConfig.cs" company="Ensage">
//     Copyright (c) 2018 Ensage.
// </copyright>

namespace IllusionSplitter
{
    using System.ComponentModel;
    using System.Windows.Input;

    using Ensage.SDK.Menu;
    using Ensage.SDK.Menu.Items;
    using Ensage.SDK.Renderer;

    [Menu("Illusion Splitter")]
    public class IllusionSplitterConfig
    {
        public IllusionSplitterConfig(IRenderManager renderer)
        {
            renderer.TextureManager.LoadAbilityFromDota("item_bottle_illusion");
            renderer.TextureManager.LoadAbilityFromDota("item_manta");
            renderer.TextureManager.LoadAbilityFromDota("naga_siren_mirror_image");
            renderer.TextureManager.LoadAbilityFromDota("terrorblade_conjure_image");
            renderer.TextureManager.LoadAbilityFromDota("phantom_lancer_doppelwalk");
        }

        [Item("Randomize Split Angle")]
        [Tooltip("Randomizes the split angle for illusions.")]
        [DefaultValue(true)]
        public bool AngleRandomizer { get; set; }

        [Item("Illusion Range")]
        [Tooltip("The range to find illusions near your hero.")]
        public Slider<float> IlluRange { get; set; } = new Slider<float>(600, 100, 2000);

        [Item("Minimum Move Range")]
        [Tooltip("Minimum range to move the illusions.")]
        public Slider<float> MinMoveRange { get; set; } = new Slider<float>(800, 100, 2000);

        [Item("Move Hero")]
        [Tooltip("Moves your hero to your mouse position, while pressing the split hotkey.")]
        [DefaultValue(true)]
        public bool MoveHero { get; set; }

        [Item("Hotkey")]
        public HotkeySelector SplitterHotkey { get; set; } = new HotkeySelector(Key.None);

        [Item("Use Abilities")]
        [Tooltip("Uses your spells and items to create illusions before splitting them.")]
        public ImageToggler UseAbilities { get; set; } = new ImageToggler(
            true,
            "item_bottle_illusion",
            "item_manta",
            "naga_siren_mirror_image",
            "terrorblade_conjure_image",
            "phantom_lancer_doppelwalk");
    }
}