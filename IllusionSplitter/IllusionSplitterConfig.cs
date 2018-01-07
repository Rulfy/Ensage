// <copyright file="IllusionSplitterConfig.cs" company="Ensage">
//    Copyright (c) 2018 Ensage.
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
        public IllusionSplitterConfig(IRendererManager renderer)
        {
            renderer.TextureManager.LoadFromDota("bottle_illusion", @"resource\flash3\images\items\bottle_illusion.png");
            renderer.TextureManager.LoadFromDota("item_manta", @"resource\flash3\images\items\manta.png");
            renderer.TextureManager.LoadFromDota("naga_siren_mirror_image", @"resource\flash3\images\spellicons\naga_siren_mirror_image.png");
            renderer.TextureManager.LoadFromDota("terrorblade_conjure_image", @"resource\flash3\images\spellicons\terrorblade_conjure_image.png");
            renderer.TextureManager.LoadFromDota("phantom_lancer_doppelwalk", @"resource\flash3\images\spellicons\phantom_lancer_doppelwalk.png");
        }

        [Item("Randomize Split Angle")]
        [Tooltip("Randomizes the split angle for illusions.")]
        [DefaultValue(true)]
        public bool AngleRandomizer { get; set; }

        [Item("Illusion Range")]
        [Tooltip("The range to find illusions near your hero.")]
        public Slider IlluRange { get; set; } = new Slider(600, 100, 2000);

        [Item("Minimum Move Range")]
        [Tooltip("Minimum range to move the illusions.")]
        public Slider MinMoveRange { get; set; } = new Slider(800, 100, 2000);

        [Item("Move Hero")]
        [Tooltip("Moves your hero to your mouse position, while pressing the split hotkey.")]
        [DefaultValue(true)]
        public bool MoveHero { get; set; }

        [Item("Hotkey")]
        public HotkeySelector SplitterHotkey { get; set; } = new HotkeySelector(Key.None);

        [Item("Use Abilities")]
        [Tooltip("Uses your spells and items to create illusions before splitting them.")]
        public PicturePicker UseAbilities { get; set; } = new PicturePicker(
            true,
            "bottle_illusion",
            "item_manta",
            "naga_siren_mirror_image",
            "terrorblade_conjure_image",
            "phantom_lancer_doppelwalk");
    }
}