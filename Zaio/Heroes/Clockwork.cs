using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Ensage;
using Ensage.Common.Enums;
using Ensage.Common.Extensions;
using Ensage.Common.Menu;
using Ensage.Common.Threading;
using log4net;
using PlaySharp.Toolkit.Logging;
using SharpDX;
using Zaio.Helpers;
using Zaio.Interfaces;

namespace Zaio.Heroes
{
    [Hero(ClassID.CDOTA_Unit_Hero_Rattletrap)]
    internal class Clockwork : ComboHero
    {
        private static readonly ILog Log = AssemblyLogs.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private static readonly string[] SupportedAbilities =
        {
            "rattletrap_battery_assault",
            "rattletrap_power_cogs",
            "rattletrap_rocket_flare",
            "rattletrap_hookshot",
            "item_blade_mail"
        };

        private readonly List<Vector3> pos = new List<Vector3>();

        private readonly List<Geometry.Polygon.Rectangle> rects = new List<Geometry.Polygon.Rectangle>();

        public override void OnLoad()
        {
            base.OnLoad();

            var heroMenu = new Menu("Clockwork", "zaioClockwork", false, "npc_dota_hero_rattletrap", true);

            heroMenu.AddItem(new MenuItem("zaioClockworkAbilitiesText", "Supported Abilities"));
            var supportedStuff = new MenuItem("zaioClockworkAbilities", string.Empty);
            supportedStuff.SetValue(new AbilityToggler(SupportedAbilities.ToDictionary(x => x, y => true)));
            heroMenu.AddItem(supportedStuff);

            ZaioMenu.LoadHeroSettings(heroMenu);
        }


        protected override async Task<bool> Killsteal()
        {
            if (await base.Killsteal())
            {
                return true;
            }

            if (MyHero.IsSilenced())
            {
                return false;
            }

            var flare = MyHero.Spellbook.SpellE;
            if (flare.CanBeCasted())
            {
                var damage = (float) flare.GetDamage(flare.Level - 1);
                damage *= GetSpellAmp();
                var enemy =
                    ObjectManager.GetEntitiesParallel<Hero>()
                                 .FirstOrDefault(
                                     x =>
                                         x.IsAlive && x.Team != MyHero.Team && flare.CanBeCasted(x) &&
                                         x.Distance2D(MyHero) < 5000 &&
                                         x.Health < damage * (1 - x.MagicDamageResist));

                if (enemy != null)
                {
                    var speed = flare.GetAbilityData("speed");
                    var time = enemy.Distance2D(MyHero) / speed * 1000.0f;

                    var predictedPos = Prediction.Prediction.PredictPosition(enemy, (int) time);
                    Log.Debug($"use flare killsteal");
                    flare.UseAbility(predictedPos);
                    await Await.Delay((int) (flare.FindCastPoint() * 1000.0 + Game.Ping));
                    return true;
                }
            }

            var ult = MyHero.Spellbook.SpellR;
            if (ult.CanBeCasted())
            {
                var damage = ult.GetAbilityData("damage");
                damage *= GetSpellAmp();

                var enemies =
                    ObjectManager.GetEntitiesParallel<Hero>()
                                 .Where(
                                     x =>
                                         x.IsAlive && x.Team != MyHero.Team && ult.CanBeCasted(x) && ult.CanHit(x) &&
                                         x.Health < damage * (1 - x.MagicDamageResist));

                var speed = ult.GetAbilityData("speed");
                var radius = ult.GetAbilityData("latch_radius");

                foreach (var enemy in enemies)
                {
                    var time = enemy.Distance2D(MyHero) / speed * 1000.0f;
                    var predictedPos = Prediction.Prediction.PredictPosition(enemy, (int) time);
                    var rec = new Geometry.Polygon.Rectangle(MyHero.NetworkPosition, predictedPos, radius);

                    // test for enemies in range
                    var isUnitBlocking = ObjectManager.GetEntitiesParallel<Unit>()
                                                      .Any(
                                                          x =>
                                                              x.IsValid && x != enemy && x.IsAlive && x != MyHero &&
                                                              x.IsSpawned && x.IsRealUnit2() &&
                                                              x.Distance2D(enemy) >= radius &&
                                                              rec.IsInside(x.NetworkPosition));
                    if (!isUnitBlocking)
                    {
                        Log.Debug($"use ult for killsteal");
                        ult.UseAbility(predictedPos);
                        await Await.Delay((int) (ult.FindCastPoint() * 1000.0 + Game.Ping));
                        return true;
                    }
                }
            }

            return false;
        }

        public override async Task ExecuteComboAsync(Unit target, CancellationToken tk = new CancellationToken())
        {
            var ult = MyHero.Spellbook.SpellR;

            if (ult.CanBeCasted(Target) && ult.CanHit(Target))
            {
                var speed = ult.GetAbilityData("speed");
                var radius = ult.GetAbilityData("latch_radius");


                var time = Target.Distance2D(MyHero) / speed * 1000.0f;
                var predictedPos = Prediction.Prediction.PredictPosition(Target, (int) time);
                var rec = new Geometry.Polygon.Rectangle(MyHero.NetworkPosition, predictedPos, radius);

                // test for enemies in range
                var isUnitBlocking = ObjectManager.GetEntitiesParallel<Unit>()
                                                  .Any(
                                                      x =>
                                                          x.IsValid && x != Target && x.IsAlive && x != MyHero &&
                                                          x.IsSpawned && x.IsRealUnit2() &&
                                                          x.Distance2D(Target) >= radius &&
                                                          rec.IsInside(x.NetworkPosition));
                if (!isUnitBlocking)
                {
                    Log.Debug($"use ult");
                    ult.UseAbility(predictedPos);
                    await Await.Delay((int) (ult.FindCastPoint() * 1000.0 + Game.Ping), tk);
                }
            }

            await UseItems(tk);

            // make him disabled
            if (DisableEnemy(tk) == DisabledState.UsedAbilityToDisable)
            {
                Log.Debug($"disabled!");
                // return;
            }

            // check if we are near the enemy
            if (!await MoveOrBlinkToEnemy(tk))
            {
                Log.Debug($"return because of blink");
                return;
            }

            var cogs = MyHero.Spellbook.SpellW;
            if (cogs.CanBeCasted())
            {
                var radius = cogs.GetAbilityData("cogs_radius");
                if (Target.Distance2D(MyHero) <= radius)
                {
                    Log.Debug($"use cogs");
                    cogs.UseAbility();
                    await Await.Delay((int) (cogs.FindCastPoint() * 1000.0 + 125 + Game.Ping), tk);

                    var bladeMail = MyHero.GetItemById(ItemId.item_blade_mail);
                    if (bladeMail != null && bladeMail.CanBeCasted())
                    {
                        Log.Debug($"using blademail");
                        bladeMail.UseAbility();
                    }
                }
            }

            var q = MyHero.Spellbook.SpellQ;
            if (q.CanBeCasted(Target))
            {
                Log.Debug($"use Q");
                q.UseAbility();
                await Await.Delay((int) (q.FindCastPoint() * 1000.0 + Game.Ping), tk);
            }

            var flare = MyHero.Spellbook.SpellQ;
            if (flare.CanBeCasted(Target))
            {
                var speed = flare.GetAbilityData("speed");
                var time = Target.Distance2D(MyHero) / speed * 1000.0f;

                var predictedPos = Prediction.Prediction.PredictPosition(Target, (int) time);

                Log.Debug($"use flare");
                flare.UseAbility(predictedPos);
                await Await.Delay((int) (flare.FindCastPoint() * 1000.0 + Game.Ping), tk);
            }

            if (ZaioMenu.ShouldUseOrbwalker)
            {
                Orbwalk();
            }
            else
            {
                MyHero.Attack(Target);
                await Await.Delay(125, tk);
            }
        }
    }
}