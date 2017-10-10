// <copyright file="Program.cs" company="Ensage">
//    Copyright (c) 2017 Ensage.
// </copyright>

namespace CampInformer
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    using Ensage;
    using Ensage.SDK.Helpers;
    using Ensage.SDK.Service;
    using Ensage.SDK.Service.Metadata;

    using SharpDX;

    [ExportPlugin("CampInformer", StartupMode.Auto)]
    internal class Program : Plugin
    {
        private readonly List<string> creepNames = new List<string>()
                                                       {
                                                           "alpha_wolf",
                                                           "black_dragon",
                                                           "big_thunder_lizard",
                                                           "centaur_khan",
                                                           "dark_troll_warlord",
                                                           "enraged_wildkin",
                                                           "forest_troll_high_priest",
                                                           "ghost",
                                                           "granite_golem",
                                                           "gnoll_assassin",
                                                           "harpy_storm",
                                                           "mud_golem",
                                                           "ogre_magi",
                                                           "polar_furbolg_ursa_warrior",
                                                           "satyr_hellcaller",
                                                           "satyr_soulstealer",
                                                           "kobold_taskmaster",
                                                           "prowler_shaman"
                                                       };

        private List<Creep> creepList = new List<Creep>();

        protected override void OnActivate()
        {
            Drawing.OnDraw += this.Drawing_OnDraw;
            UpdateManager.Subscribe(this.OnUpdate, 1000);
        }

        protected override void OnDeactivate()
        {
            UpdateManager.Unsubscribe(this.OnUpdate);
            Drawing.OnDraw -= this.Drawing_OnDraw;
        }

        private void Drawing_OnDraw(EventArgs args)
        {
            foreach (var creep in this.creepList.Where(x => x.IsValid))
            {
                Vector2 screenPos;
                if (Drawing.WorldToScreen(creep.Position, out screenPos))
                {
                    var text = Game.Localize(creep.Name);
                    var textSize = Drawing.MeasureText(text, "Arial", Drawing.DefaultTextSize, FontFlags.DropShadow);
                    Drawing.DrawText(text, new Vector2(screenPos.X - (textSize.X / 2), screenPos.Y - (textSize.Y / 2)), Color.White, FontFlags.DropShadow);
                }
            }
        }

        private void OnUpdate()
        {
            this.creepList = EntityManager<Creep>.Entities.Where(x => x.IsWaitingToSpawn && x.Team == Team.Neutral && this.creepNames.Contains(x.Name.Substring(17))).ToList();
        }
    }
}