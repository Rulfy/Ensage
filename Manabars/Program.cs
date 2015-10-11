using System;
using System.Linq;
using Ensage;
using SharpDX;

namespace Manabars
{
    class Program
    {
        static void Main(string[] args)
        {
            Drawing.OnDraw += Drawing_OnDraw;
        }

        static void Drawing_OnDraw(EventArgs args)
        {
            if (!Game.IsInGame)
                return;
            var player = ObjectMgr.LocalPlayer;
            if (player == null || player.Team == Team.Observer)
                return;

            var enemies = ObjectMgr.GetEntities<Hero>().Where(x => x.IsVisible && x.IsAlive && x.MaximumMana != 0 && x.Team != player.Team).ToList();
            foreach (var enemy in enemies)
            {
                Vector2 screenPos;
                var enemyPos = enemy.Position + new Vector3(0, 0, enemy.HealthBarOffset);
                if (!Drawing.WorldToScreen(enemyPos, out screenPos))
                    continue;

                var start = screenPos + new Vector2(-51, -22);
                var manaDelta = new Vector2(100 * enemy.Mana / enemy.MaximumMana, 0);

                // Draw background
                Drawing.DrawRect(start + manaDelta, new Vector2(101, 5) - manaDelta, new Color(0xE4, 0xDD, 0xDD, 0x20));
                // Draw manabar
                Drawing.DrawRect(start, manaDelta + new Vector2(0, 5), Color.RoyalBlue);
                // Draw frame
                Drawing.DrawRect(start + new Vector2(-1, -1), new Vector2(102, 7), Color.DarkBlue, true);
                // Draw text
                var text = string.Format("{0} / {1}", (int)enemy.Mana, (int)enemy.MaximumMana);
                var textSize = Drawing.MeasureText(text, "Arial", new Vector2(10, 150), FontFlags.None);
                var textPos = start + new Vector2(51 - textSize.X / 2, -textSize.Y / 2 + 2);
                Drawing.DrawText(text, textPos, new Vector2(10, 150), Color.White, FontFlags.AntiAlias | FontFlags.DropShadow);
            }
        }
    }
}
