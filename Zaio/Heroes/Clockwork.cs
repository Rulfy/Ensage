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
using Zaio.Prediction;

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

        private static readonly string[] KillstealAbilities =
        {
            "rattletrap_rocket_flare",
            "rattletrap_hookshot"
        };

        private MenuItem _circleTillHook;

        private bool ShouldCircleHook => _circleTillHook.GetValue<bool>();

        public override void OnLoad()
        {
            base.OnLoad();

            var heroMenu = new Menu("Clockwork", "zaioClockwork", false, "npc_dota_hero_rattletrap", true);

            heroMenu.AddItem(new MenuItem("zaioClockworkAbilitiesText", "Supported Abilities"));
            var supportedStuff = new MenuItem("zaioClockworkAbilities", string.Empty);
            supportedStuff.SetValue(new AbilityToggler(SupportedAbilities.ToDictionary(x => x, y => true)));
            heroMenu.AddItem(supportedStuff);

            heroMenu.AddItem(new MenuItem("zaioClockworkKillstealAbilitiesText", "Supported Killsteal Abilities"));
            var supportedKillsteal = new MenuItem("zaioClockworkKillstealAbilities", string.Empty);
            supportedKillsteal.SetValue(new AbilityToggler(KillstealAbilities.ToDictionary(x => x, y => true)));
            heroMenu.AddItem(supportedKillsteal);

            _circleTillHook = new MenuItem("zaioClockworkStopTillHook", "Circle on blocked Hook").SetValue(true);
            _circleTillHook.Tooltip = "Moves on a circle when your hook is blocked.";
            heroMenu.AddItem(_circleTillHook);

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
                                         x.Distance2D(MyHero) < 5000 && !x.IsIllusion &&
                                         x.Health < damage * (1 - x.MagicResistance()) && !x.CantBeAttacked() &&
                                         !x.CantBeKilled());

                if (enemy != null)
                {
                    var speed = flare.GetAbilityData("speed");
                    var time = enemy.Distance2D(MyHero) / speed * 1000.0f;

                    var predictedPos = Prediction.Prediction.PredictPosition(enemy, (int) time);
                    Log.Debug($"use flare killsteal");
                    flare.UseAbility(predictedPos);
                    await Await.Delay(GetAbilityDelay(enemy, flare));
                    return true;
                }
            }

            if (Target != null)
            {
                return false;
            }

            var ult = MyHero.Spellbook.SpellR;
            if (MyHero.HasItem(ClassID.CDOTA_Item_UltimateScepter) && ult.CanBeCasted())
            {
                var damage = ult.GetAbilityData("damage");
                damage *= GetSpellAmp();

                var enemies =
                    ObjectManager.GetEntitiesParallel<Hero>()
                                 .Where(
                                     x =>
                                         x.IsAlive && x.Team != MyHero.Team && ult.CanBeCasted(x) && ult.CanHit(x) &&
                                         !x.IsIllusion &&
                                         x.Health < damage * (1 - x.MagicResistance()) && !x.CantBeAttacked() &&
                                         !x.CantBeKilled());

                var speed = ult.GetAbilityData("speed");
                var radius = ult.GetAbilityData("latch_radius") * 2;

                foreach (var enemy in enemies)
                {
                    var time = enemy.Distance2D(MyHero) / speed * 1000.0f;
                    var predictedPos = Prediction.Prediction.PredictPosition(enemy, (int) time, true);
                    if (predictedPos == Vector3.Zero)
                    {
                        continue;
                    }

                    var rec = new Geometry.Polygon.Rectangle(MyHero.NetworkPosition, predictedPos, radius);

                    // test for enemies in range
                    var isUnitBlocking = ObjectManager.GetEntitiesParallel<Unit>()
                                                      .Any(
                                                          x =>
                                                              x.IsValid && x != enemy && x.IsAlive && x != MyHero &&
                                                              x.IsSpawned && x.IsRealUnit() &&
                                                              x.Distance2D(enemy) >= radius &&
                                                              rec.IsInside(x.NetworkPosition));
                    if (!isUnitBlocking)
                    {
                        Log.Debug($"use ult for killsteal");
                        ult.UseAbility(predictedPos);
                        await Await.Delay(GetAbilityDelay(enemy, ult));
                        return true;
                    }
                }
            }

            return false;
        }

        public override async Task ExecuteComboAsync(Unit target, CancellationToken tk = new CancellationToken())
        {
            var ult = MyHero.Spellbook.SpellR;

            if (!MyHero.IsSilenced() && ult.CanBeCasted(target) && ult.CanHit(target))
            {
                var speed = ult.GetAbilityData("speed");
                var radius = ult.GetAbilityData("latch_radius") * 2;


                var time = target.Distance2D(MyHero) / speed * 1000.0f;
                var predictedPos = Prediction.Prediction.PredictPosition(target, (int) time, true);
                if (predictedPos != Vector3.Zero)
                {
                    var rec = new Geometry.Polygon.Rectangle(MyHero.NetworkPosition, predictedPos, radius);

                    // test for enemies in range
                    var isUnitBlocking = ObjectManager.GetEntitiesParallel<Unit>()
                                                      .Any(
                                                          x =>
                                                              x.IsValid && x != target && x.IsAlive && x != MyHero &&
                                                              x.IsSpawned && x.IsRealUnit() &&
                                                              x.Distance2D(target) >= radius &&
                                                              rec.IsInside(x.NetworkPosition));
                    if (!isUnitBlocking)
                    {
                        Log.Debug($"use ult");
                        ult.UseAbility(predictedPos);
                        await Await.Delay(GetAbilityDelay(target, ult), tk);
                    }
                    else if (ShouldCircleHook)
                    {
                        //MyHero.Hold();
                        var dir = (Game.MousePosition - target.NetworkPosition).Normalized();
                        var distance = (target.NetworkPosition - MyHero.NetworkPosition).Length();
                        var targetPos = target.NetworkPosition + dir * distance;
                        MyHero.Move(Prediction.Prediction.PredictMyRoute(MyHero, 500, targetPos).Last());
                        return;
                    }
                }
            }

            await UseItems(target, tk);

            // make him disabled
            if (await DisableEnemy(target, tk) == DisabledState.UsedAbilityToDisable)
            {
                Log.Debug($"disabled!");
                // return;
            }

            // check if we are near the enemy
            if (!await MoveOrBlinkToEnemy(target, tk))
            {
                Log.Debug($"return because of blink");
                return;
            }

            if (!MyHero.IsSilenced())
            {
                var cogs = MyHero.Spellbook.SpellW;
                if (cogs.CanBeCasted())
                {
                    var radius = cogs.GetAbilityData("cogs_radius");
                    if (target.Distance2D(MyHero) <= radius)
                    {
                        Log.Debug($"use cogs");
                        cogs.UseAbility();
                        await Await.Delay((int) (cogs.FindCastPoint() * 1000.0 + 125 + Game.Ping), tk);

                        var bladeMail = MyHero.GetItemById(ItemId.item_blade_mail);
                        if (bladeMail != null && bladeMail.CanBeCasted())
                        {
                            Log.Debug($"using blademail");
                            bladeMail.UseAbility();
                            await Await.Delay(ItemDelay, tk);
                        }
                    }
                }

                var q = MyHero.Spellbook.SpellQ;
                if (q.CanBeCasted(target))
                {
                    Log.Debug($"use Q");
                    q.UseAbility();
                    await Await.Delay((int) (q.FindCastPoint() * 1000.0 + Game.Ping), tk);
                }

                var flare = MyHero.Spellbook.SpellQ;
                if (flare.CanBeCasted(target))
                {
                    var speed = flare.GetAbilityData("speed");
                    var time = target.Distance2D(MyHero) / speed * 1000.0f;

                    var predictedPos = Prediction.Prediction.PredictPosition(target, (int) time);

                    Log.Debug($"use flare");
                    flare.UseAbility(predictedPos);
                    await Await.Delay(GetAbilityDelay(target, flare), tk);
                }
            }

            await HasNoLinkens(target, tk);

            if (ZaioMenu.ShouldUseOrbwalker)
            {
                Orbwalk();
            }
            else
            {
                MyHero.Attack(target);
                await Await.Delay(125, tk);
            }
        }
    }
}