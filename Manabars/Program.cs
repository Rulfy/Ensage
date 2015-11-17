namespace Manabars
{
    using System;
    using System.Linq;

    using Ensage;
    using Ensage.Common;

    using SharpDX;

    internal class Program
    {
        #region Methods

        private static void Drawing_OnDraw(EventArgs args)
        {
            if (!Game.IsInGame)
            {
                return;
            }
            var player = ObjectMgr.LocalPlayer;
            if (player == null || player.Team == Team.Observer)
            {
                return;
            }

            var enemies =
                ObjectMgr.GetEntities<Hero>()
                    .Where(x => x.IsVisible && x.IsAlive && x.MaximumMana > 0 && !x.IsIllusion && x.Team != player.Team)
                    .ToList();
            foreach (var enemy in enemies)
            {
                var start = HUDInfo.GetHPbarPosition(enemy) + new Vector2(0, HUDInfo.GetHpBarSizeY(enemy) + 1);
                var manaperc = enemy.Mana / enemy.MaximumMana;
                var size = new Vector2(HUDInfo.GetHPBarSizeX(enemy), HUDInfo.GetHpBarSizeY(enemy) / 2);
                // Draw background
                Drawing.DrawRect(start, size + new Vector2(1, 1), new Color(0, 0, 50, 150));
                // Draw manabar
                Drawing.DrawRect(start, new Vector2(size.X * manaperc, size.Y), Color.RoyalBlue);
                // Draw frame
                Drawing.DrawRect(start + new Vector2(-1, -1), size + new Vector2(3, 3), Color.Black, true);
                // Draw text
                var text = string.Format("{0} / {1}", (int)enemy.Mana, (int)enemy.MaximumMana);
                var textSize = Drawing.MeasureText(text, "Arial", new Vector2(size.Y * 2, size.X), FontFlags.AntiAlias);
                var textPos = start + new Vector2(size.X / 2 - textSize.X / 2, -textSize.Y / 2 + 2);
                Drawing.DrawText(
                    text,
                    textPos,
                    new Vector2(size.Y * 2, size.X),
                    Color.White,
                    FontFlags.AntiAlias | FontFlags.DropShadow);
            }
        }

        private static void Main()
        {
            Drawing.OnDraw += Drawing_OnDraw;
        }

        #endregion
    }
}