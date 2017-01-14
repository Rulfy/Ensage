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
using Zaio.Helpers;
using Zaio.Interfaces;

namespace Zaio.Heroes
{
    [Hero(ClassID.CDOTA_Unit_Hero_EmberSpirit)]
    internal class EmberSpirit : ComboHero
    {
        private static readonly ILog Log = AssemblyLogs.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private static readonly string[] SupportedAbilities =
        {
            "ember_spirit_searing_chains",
            "ember_spirit_sleight_of_fist",
            "ember_spirit_flame_guard",
            "ember_spirit_fire_remnant"
        };

        private static readonly string[] KillstealAbilities =
        {
            "ember_spirit_searing_chains",
            "ember_spirit_sleight_of_fist",
            "ember_spirit_fire_remnant"
        };

        private MenuItem _minimumRemnantItem;
        private int MinimumRemnants => _minimumRemnantItem.GetValue<Slider>().Value;

        private int CurrentRemnants
            => MyHero.FindModifier("modifier_ember_spirit_fire_remnant_charge_counter")?.StackCount ?? 0;

        private IEnumerable<Unit> Remnants
            => ObjectManager.GetEntities<Unit>().Where(x => x.Name == "npc_dota_ember_spirit_remnant");

        public override void OnLoad()
        {
            base.OnLoad();

            var heroMenu = new Menu("Ember Spirit", "zaioEmber Spirit", false, "npc_dota_hero_ember_spirit", true);

            heroMenu.AddItem(new MenuItem("zaioEmberSpiritAbilitiesText", "Supported Abilities"));
            var supportedStuff = new MenuItem("zaioEmberSpiritAbilities", string.Empty);
            supportedStuff.SetValue(new AbilityToggler(SupportedAbilities.ToDictionary(x => x, y => true)));
            heroMenu.AddItem(supportedStuff);

            heroMenu.AddItem(new MenuItem("zaioEmberSpiritKillstealAbilitiesText", "Supported Killsteal Abilities"));
            var supportedKillsteal = new MenuItem("zaioEmberSpiritKillstealAbilities", string.Empty);
            supportedKillsteal.SetValue(new AbilityToggler(KillstealAbilities.ToDictionary(x => x, y => true)));
            heroMenu.AddItem(supportedKillsteal);

            _minimumRemnantItem =
                new MenuItem("zaioEmberSpiritMinimumRemnant", "Save Remnants").SetValue(new Slider(1, 0, 3));
            _minimumRemnantItem.Tooltip = "Minimum Remnants to keep.";
            heroMenu.AddItem(_minimumRemnantItem);

            ZaioMenu.LoadHeroSettings(heroMenu);
        }

        /*
        modifier_ember_spirit_sleight_of_fist_caster
        modifier_ember_spirit_sleight_of_fist_caster_invulnerability
        */

        protected override async Task<bool> Killsteal()
        {
            if (await base.Killsteal())
            {
                return true;
            }

            if (MyHero.IsSilenced() ||
                MyHero.HasModifiers(
                    new[] {"modifier_ember_spirit_sleight_of_fist_caster", "modifier_ember_spirit_fire_remnant"}, false))
            {
                return false;
            }

            var sleight = MyHero.GetAbilityById(AbilityId.ember_spirit_sleight_of_fist);
            if (sleight.CanBeCasted())
            {
                var damage = MyHero.MinimumDamage + MyHero.BonusDamage + sleight.GetAbilityData("bonus_hero_damage");
                var radius = sleight.GetAbilityData("radius");

                var enemy =
                    ObjectManager.GetEntitiesParallel<Hero>()
                                 .FirstOrDefault(
                                     x =>
                                         x.IsAlive && x.Team != MyHero.Team && !x.IsIllusion && sleight.CanBeCasted(x) &&
                                         sleight.CanHit(x) &&
                                         x.Health < damage * (1 - x.DamageResist) && !x.CantBeAttacked() && !x.CantBeKilled());
                if (enemy != null)
                {
                    Log.Debug(
                        $"use killsteal W because enough damage {enemy.Health} <= {enemy.Health < damage * (1 - enemy.DamageResist)} ");

                    sleight.UseAbility(enemy.NetworkPosition);
                    await Await.Delay(125);
                    return true;
                }
            }

            var ult = MyHero.GetAbilityById(AbilityId.ember_spirit_fire_remnant);
            var ultActivate = MyHero.GetAbilityById(AbilityId.ember_spirit_activate_fire_remnant);
            if (ult.CanBeCasted() && ultActivate.CanBeCasted() &&
                (MinimumRemnants == 0 || MinimumRemnants < CurrentRemnants))
            {
                var damage = ult.GetAbilityData("damage");
                damage *= GetSpellAmp();
                var enemy =
                    ObjectManager.GetEntitiesParallel<Hero>()
                                 .FirstOrDefault(
                                     x =>
                                         x.IsAlive && x.Team != MyHero.Team && !x.IsIllusion && ult.CanBeCasted(x) &&
                                         ult.CanHit(x) && !x.IsMagicImmune() &&
                                         x.Health < damage * (1 - x.MagicResistance()) && !x.CantBeAttacked() && !x.CantBeKilled());


                if (enemy != null)
                {
                    Log.Debug(
                        $"use killsteal ult because enough damage {enemy.Health} <= {damage * (1 - enemy.MagicResistance())} ");

                    var castPoint = ult.FindCastPoint();
                    var speed = MyHero.MovementSpeed * (ult.GetAbilityData("speed_multiplier") / 100);
                    var time = (castPoint + enemy.Distance2D(MyHero) / speed) * 1000.0f;
                    var predictedPos = Prediction.Prediction.PredictPosition(enemy, (int) time);


                    // test if we already got a remnant near the enemy
                    var radius = ult.GetAbilityData("radius");
                    var remnant = Remnants.FirstOrDefault(unit => unit.Distance2D(enemy) < radius);
                    if (remnant == null)
                    {
                        Log.Debug($"placing remnant first!");
                        ult.UseAbility(predictedPos);
                        await Await.Delay((int) (time + Game.Ping));
                    }
                    else
                    {
                        Log.Debug($"already got a remnant near the enemy PogChamp!");
                    }
                    ultActivate.UseAbility(predictedPos);
                    await Await.Delay(125);
                    return true;
                }
            }

            return false;
        }

        public override async Task ExecuteComboAsync(Unit target, CancellationToken tk = new CancellationToken())
        {
            var stun = MyHero.GetAbilityById(AbilityId.ember_spirit_searing_chains);
            if (
                MyHero.HasModifiers(new[]
                {
                    "modifier_ember_spirit_sleight_of_fist_caster",
                    "modifier_ember_spirit_fire_remnant"
                }, false))
            {
                Log.Debug($"in sleight mode");
                if (stun.CanBeCasted(Target) && stun.CanHit(Target) && !Target.IsMagicImmune())
                {
                    Log.Debug($"use our Q because we are using W or ult and are near the target!");
                    stun.UseAbility();
                    await Await.Delay(125, tk);
                }
                return;
            }
            HasNoLinkens(Target);
            await UseItems(tk);

            // make him disabled
            if (DisableEnemy(tk) == DisabledState.UsedAbilityToDisable)
            {
                Log.Debug($"disabled!");
                // return;
            }

            var sleight = MyHero.GetAbilityById(AbilityId.ember_spirit_sleight_of_fist);
            if (sleight.CanBeCasted(Target) && sleight.CanHit(Target))
            {
                Log.Debug($"using sleigth");
                sleight.UseAbility(Target.NetworkPosition);
                await Await.Delay(1, tk);
                return;
            }

            if (stun.CanBeCasted(Target) && stun.CanHit(Target))
            {
                Log.Debug($"use our Q");
                stun.UseAbility();
                await Await.Delay(125, tk);
            }

            var ult = MyHero.GetAbilityById(AbilityId.ember_spirit_fire_remnant);
            var distance = ult.CastRange;

            var shield = MyHero.GetAbilityById(AbilityId.ember_spirit_flame_guard);
            if (shield.CanBeCasted())
            {
                var hasEnemies = ObjectManager.GetEntitiesParallel<Hero>()
                                              .Any(
                                                  x =>
                                                      x.IsValid && x.IsAlive && x.Team != MyHero.Team &&
                                                      (ult.CanBeCasted() && x.Distance2D(MyHero) < distance ||
                                                       !ult.CanBeCasted() && x.Distance2D(MyHero) < 800));
                if (hasEnemies)
                {
                    shield.UseAbility();
                    await Await.Delay(125, tk);
                }
            }

            var ultActivate = MyHero.GetAbilityById(AbilityId.ember_spirit_activate_fire_remnant);
            if (!IsInRange(MyHero.AttackRange * 2.0f) && !Target.IsMagicImmune())
            {
                if (ult.CanBeCasted() && ultActivate.CanBeCasted() &&
                    (MinimumRemnants == 0 || MinimumRemnants < CurrentRemnants))
                {
                    var castPoint = ult.FindCastPoint();
                    var speed = MyHero.MovementSpeed * (ult.GetAbilityData("speed_multiplier") / 100);
                    var time = (castPoint + Target.Distance2D(MyHero) / speed) * 1000.0f;
                    var predictedPos = Prediction.Prediction.PredictPosition(Target, (int) time);

                    // test if we already got a remnant near the enemy
                    var radius = ult.GetAbilityData("radius");
                    var remnant = Remnants.FirstOrDefault(unit => unit.Distance2D(Target) < radius);
                    if (remnant == null)
                    {
                        Log.Debug($"placing remnant first to approach!");
                        ult.UseAbility(predictedPos);
                        await Await.Delay((int) (time + Game.Ping), tk);
                    }
                    else
                    {
                        Log.Debug($"already got a remnant near the enemy PogChamp to approach!");
                    }
                    ultActivate.UseAbility(predictedPos);
                    await Await.Delay(1, tk);
                    return;
                }
            }

            // check if we are near the enemy
            if (!await MoveOrBlinkToEnemy(tk))
            {
                Log.Debug($"return because of blink");
                return;
            }

            if (shield.CanBeCasted())
            {
                shield.UseAbility();
                await Await.Delay(125, tk);
            }

            if (ult.CanBeCasted(Target) && ultActivate.CanBeCasted() && ult.CanHit(Target) && !Target.IsMagicImmune() &&
                (MinimumRemnants == 0 || MinimumRemnants < CurrentRemnants))
            {
                var damage = ult.GetAbilityData("damage");
                damage *= GetSpellAmp();
                if (Target.Health < damage * (1 - Target.MagicResistance()))
                {
                    var castPoint = ult.FindCastPoint();
                    var speed = MyHero.MovementSpeed * (ult.GetAbilityData("speed_multiplier") / 100);
                    var time = (castPoint + Target.Distance2D(MyHero) / speed) * 1000.0f;
                    var predictedPos = Prediction.Prediction.PredictPosition(Target, (int) time);

                    // test if we already got a remnant near the enemy
                    var radius = ult.GetAbilityData("radius");
                    var remnant = Remnants.FirstOrDefault(unit => unit.Distance2D(Target) < radius);
                    if (remnant == null)
                    {
                        Log.Debug($"placing remnant first to kill!");
                        ult.UseAbility(predictedPos);
                        await Await.Delay((int) (time + Game.Ping), tk);
                    }
                    else
                    {
                        Log.Debug($"already got a remnant near the enemy PogChamp to kill!");
                    }
                    ultActivate.UseAbility(predictedPos);
                    await Await.Delay(1, tk);
                    return;
                }
            }

            if (ZaioMenu.ShouldUseOrbwalker)
            {
                Orbwalk();
                Log.Debug($"orbwalking");
            }
            else
            {
                MyHero.Attack(Target);
                await Await.Delay(125, tk);
            }
        }
    }
}