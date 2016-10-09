using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using Ensage;
using Ensage.Common.Extensions;
using SharpDX;

namespace CampInformer
{
    class Program
    {
        private static List<string> _creepNames;

        static void Main(string[] args)
        {
            _creepNames = new List<string>()
            {
                "alpha_wolf", "black_dragon", "big_thunder_lizard", "centaur_khan", "dark_troll_warlord", "enraged_wildkin", 
                "forest_troll_high_priest", "ghost", "granite_golem", "gnoll_assassin", "harpy_storm", "mud_golem", "ogre_magi", 
                "polar_furbolg_ursa_warrior", "satyr_hellcaller", "satyr_soulstealer", "kobold_taskmaster"
            };
            Drawing.OnDraw += Drawing_OnDraw;      
        }

        static void Drawing_OnDraw(EventArgs args)
        {
            if (!Game.IsInGame)
                return;

            var creeps = ObjectManager.GetEntities<Creep>().Where(x => x.IsWaitingToSpawn && x.Team == Team.Neutral).OrderBy(x => x.CreateTime).ToList();

            var drawList = new List<List<Creep>>();
            // Check if these creeps are in our namelist
            foreach (
                var creep in
                    from creep in creeps
                    let name = creep.Name.Substring(17)
                    where _creepNames.Contains(name)
                    select creep)
            {
                Vector2 screenPos;
                if (Drawing.WorldToScreen(creep.Position, out screenPos))
                {
                    // Add creeps to lists to detect stacks
                    var found = false;
                    foreach (var l in drawList)
                    {
                        if (l.Any(c => c.Distance2D(creep) < 300))
                        {
                            l.Add(creep);
                            found = true;
                            break;
                        }
                    }
                    // Create new list if new stack has been found
                    if( !found )
                        drawList.Add(new List<Creep>{creep});
                }
            }
            foreach (var l in drawList)
            {
                var counter = 1;
                foreach (var creep in l)
                {
                    Vector2 screenPos;
                    if (Drawing.WorldToScreen(creep.Position, out screenPos))
                    {
                        var text = $"{counter++} - {Game.Localize(creep.Name)}";
                        var textSize = Drawing.MeasureText(text, "Arial", Drawing.DefaultTextSize, FontFlags.DropShadow);
                        Drawing.DrawText(text, new Vector2(screenPos.X - textSize.X/2, screenPos.Y - textSize.Y/2),
                            Color.White, FontFlags.DropShadow);
                    }
                }
            }
        }
    }
}
