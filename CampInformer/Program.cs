using System;
using System.Collections.Generic;
using System.Linq;
using Ensage;
using SharpDX;
using SharpDX.Direct3D9;

namespace CampInformer
{
    class Program
    {
        private static List<string> _creepNames;
        private static Font _textFont;

        static void Main(string[] args)
        {
            _creepNames = new List<string>()
            {
                "alpha_wolf", "black_dragon", "big_thunder_lizard", "centaur_khan", "dark_troll_warlord", "enraged_wildkin", 
                "forest_troll_high_priest", "ghost", "granite_golem", "gnoll_assassin", "harpy_storm", "mud_golem", "ogre_magi", 
                "polar_furbolg_ursa_warrior", "satyr_hellcaller", "satyr_soulstealer", "kobold_taskmaster"
            };
            _textFont = new Font(
                    Drawing.Direct3DDevice,
                    new FontDescription
                    {
                        FaceName = "Calibri",
                        Height = 14,
                        OutputPrecision = FontPrecision.Default,
                        Quality = FontQuality.Default
                    });

            Drawing.OnPreReset += Drawing_OnPreReset;
            Drawing.OnPostReset += Drawing_OnPostReset;
            Drawing.OnEndScene += Drawing_OnEndScene;

            AppDomain.CurrentDomain.DomainUnload += CurrentDomain_DomainUnload;
        }

        static void CurrentDomain_DomainUnload(object sender, EventArgs e)
        {
             _textFont.Dispose();
        }

        static void Drawing_OnPostReset(EventArgs args)
        {
            _textFont.OnResetDevice();
        }

        static void Drawing_OnPreReset(EventArgs args)
        {
            _textFont.OnLostDevice();
        }

        static void Drawing_OnEndScene(EventArgs args)
        {
            //foreach (var creep in _creeps)
            var creeps = EntityList.GetEntities<Creep>().Where(x => x.WaitingToSpawn && x.Team == Team.Neutral).ToList();
            if (creeps.Any())
            {
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
                        var text = Game.Localize(creep.Name);
                        var textSize = _textFont.MeasureText(null, text, FontDrawFlags.Center);
                        var x = (int)screenPos.X - textSize.Width / 2;
                        var y = (int)screenPos.Y - textSize.Height / 2;
                        _textFont.DrawText(null, text, x, y, Color.White);
                    }
                }
            }
        }
    }
}
