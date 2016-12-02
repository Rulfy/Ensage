using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Ensage;
using Ensage.Common;
using Ensage.Common.Extensions;
using Ensage.Common.Extensions.SharpDX;
using Ensage.Common.Threading;
using Ensage.Items;
using log4net;
using PlaySharp.Toolkit.Logging;
using SharpDX;

namespace IllusionSplitter
{
    class Program
    {
        private static readonly ILog Log = AssemblyLogs.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
        private static bool _added;

        static void Main()
        {
            Events.OnLoad += Events_OnLoad;
        }

        private static void Events_OnLoad(object sender, EventArgs e)
        {
            if (!AssemblyMenu.BuildMenu())
                return;

            AssemblyMenu.SplitterHotkeyPressed += AssemblyMenu_SplitterHotkeyPressed;
        }

#pragma warning disable 1998
        private static async void GameDispatcher_OnIngameUpdate(EventArgs args)
#pragma warning restore 1998
        {
            Await.Block("splitterLogic", SplitterLogic);
        }

        private static void AssemblyMenu_SplitterHotkeyPressed(object sender, EventArgs e)
        {
            var hero = ObjectManager.LocalHero;
            if (hero == null || _added || Game.IsPaused)
                return;
            _added = true;
            GameDispatcher.OnIngameUpdate += GameDispatcher_OnIngameUpdate;
            
        }

        private static async Task UseSpells(Hero hero)
        {
            var mirrorImage = hero.FindSpell("naga_siren_mirror_image");
            if (mirrorImage != null && mirrorImage.CanBeCasted())
            {
                mirrorImage.UseAbility();
                int delay = (int) ((mirrorImage.GetCastPoint(0) +
                                   mirrorImage.AbilitySpecialData.First(x => x.Name == "invuln_duration").Value)*1000.0f) +
                            250 + (int)Game.Ping;
                Log.Debug($"using mirror image with delay {delay}");
                await Await.Delay(delay);
                return;
            }

            var conjureImage = hero.FindSpell("terrorblade_conjure_image");
            if (conjureImage != null && conjureImage.CanBeCasted())
            {
                conjureImage.UseAbility();
                int delay = (int)(conjureImage.GetCastPoint(0) * 1000.0f + 250.0f) + (int)Game.Ping;
                Log.Debug($"using conjure image with delay {delay}");
                await Await.Delay(delay);
                return;
            }

            var doppelWalk = hero.FindSpell("phantom_lancer_doppelwalk");
            if (doppelWalk != null && doppelWalk.CanBeCasted())
            {
                var pos = Game.MousePosition - hero.Position;
                if (pos.Length() > doppelWalk.CastRange)
                {
                    pos.Normalize();
                    pos *= doppelWalk.CastRange;
                }

                doppelWalk.UseAbility(hero.Position + pos);
                int delay = (int)(doppelWalk.GetCastPoint(0) +
                                   doppelWalk.AbilitySpecialData.First(x => x.Name == "delay").Value) * 1000 +
                            250 +(int)Game.Ping;
                Log.Debug($"using doppel walk with delay {delay}");
                await Await.Delay(delay);
                // ReSharper disable once RedundantJumpStatement
                return;
            }
        }

        private static async Task SplitterLogic()
        {
            var hero = ObjectManager.LocalHero;
            bool casted = false;
            // checks for items
            if (AssemblyMenu.ShouldUseItems)
            {
                var manta = hero.FindItem("item_manta");
                if (manta != null && manta.CanBeCasted())
                {
                    Log.Debug("Used manta");
                    manta.UseAbility();
                    casted = true;

                    await Await.Delay(250+(int)Game.Ping);
                }
                if (!casted)
                {
                    var bottle = hero.FindItem("item_bottle") as Bottle;
                    if (bottle != null && bottle.StoredRune == RuneType.Illusion)
                    {
                        Log.Debug("Used bottle");
                        bottle.UseAbility();
                        casted = true;

                        await Await.Delay(125 + (int)Game.Ping);
                    }
                }
            }

            if (!casted && AssemblyMenu.ShouldUseSpells)
            {
                await UseSpells(hero);
            }

            Vector3 heroTargetDirection;
            if (AssemblyMenu.ShouldMoveHero)
            {
                Log.Debug($"Move hero to postition {Game.MousePosition}");
                hero.Move(Game.MousePosition);
                heroTargetDirection = Game.MousePosition - hero.Position;
            }
            else
            {
                heroTargetDirection = hero.InFront(250) - hero.Position;
                Log.Debug($"Hero target dir {heroTargetDirection}");
            }

            var illusions =
                ObjectManager.GetEntitiesFast<Hero>()
                    .Where(x => x.IsIllusion && x.IsAlive && x.IsControllable && x.Distance2D(hero) < AssemblyMenu.IllusionRange)
                    .ToList();
            if (!illusions.Any())
            {
                GameDispatcher.OnIngameUpdate -= GameDispatcher_OnIngameUpdate;
                _added = false;
                return;
            }

            Vector3 middlePosition = illusions.Aggregate(hero.Position, (current, illusion) => current + illusion.Position);
            var unitCount = illusions.Count + 1;

            middlePosition /= unitCount;
            float illuAngle = 360.0f / unitCount;

            Random random = null;
            if (AssemblyMenu.ShouldRandomizeAngle)
                random = new Random();

            foreach (var illusion in illusions)
            {
                if (random != null)
                {
                    var randomAngle = random.NextFloat(1, illuAngle / unitCount);
                    heroTargetDirection = heroTargetDirection.Rotated(MathUtil.DegreesToRadians(illuAngle + randomAngle));
                }
                else
                    heroTargetDirection = heroTargetDirection.Rotated(MathUtil.DegreesToRadians(illuAngle));

                var dir = heroTargetDirection.Normalized();
                dir *= AssemblyMenu.MinimumMoveRange;
                var movePos = middlePosition + dir;
                Log.Debug($"Move illu to {movePos}");
                illusion.Move(movePos);
            }
            GameDispatcher.OnIngameUpdate -= GameDispatcher_OnIngameUpdate;
            _added = false;
        }
    }
}
