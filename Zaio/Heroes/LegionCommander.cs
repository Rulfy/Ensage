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
    [Hero(ClassID.CDOTA_Unit_Hero_Legion_Commander)]
    internal class LegionCommander : ComboHero
    {
        private static readonly ILog Log = AssemblyLogs.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private static readonly string[] SupportedAbilities =
        {
            "legion_commander_overwhelming_odds",
            "legion_commander_press_the_attack",
            "legion_commander_duel",
            "item_blade_mail",
            "item_armlet"
        };

        private static readonly string[] KillstealAbilities =
        {
            "legion_commander_overwhelming_odds"
        };

        public override void OnLoad()
        {
            base.OnLoad();

            var heroMenu = new Menu("Legion", "zaioLegion", false, "npc_dota_hero_legion_commander", true);

            heroMenu.AddItem(new MenuItem("zaioLegionAbilitiesText", "Supported Abilities"));
            var supportedStuff = new MenuItem("zaioLegionAbilities", string.Empty);
            supportedStuff.SetValue(new AbilityToggler(SupportedAbilities.ToDictionary(x => x, y => true)));
            heroMenu.AddItem(supportedStuff);

            heroMenu.AddItem(new MenuItem("zaioLegionKillstealAbilitiesText", "Supported Killsteal Abilities"));
            var supportedKillsteal = new MenuItem("zaioLegionKillstealAbilities", string.Empty);
            supportedKillsteal.SetValue(new AbilityToggler(KillstealAbilities.ToDictionary(x => x, y => true)));
            heroMenu.AddItem(supportedKillsteal);

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

            var odds = MyHero.Spellbook.SpellQ;
            if (odds.CanBeCasted())
            {
                var damage = odds.GetAbilityData("damage");
                var damagePerUnit = odds.GetAbilityData("damage_per_unit");
                var damagePerHero = odds.GetAbilityData("damage_per_hero");
                var radius = odds.GetAbilityData("radius");

                var enemies =
                    ObjectManager.GetEntitiesParallel<Hero>()
                                 .Where(
                                     x =>
                                         x.IsAlive && x.Team != MyHero.Team && odds.CanBeCasted(x) && odds.CanHit(x) &&
                                         !x.CantBeAttacked() && !x.CantBeKilled());

                var spellAmp = GetSpellAmp();
                foreach (var enemy in enemies)
                {
                    var additionalTargetCount =
                        ObjectManager.GetEntitiesParallel<Unit>()
                                     .Where(
                                         x =>
                                             x.IsValid && x.IsAlive && x != enemy && !x.IsIllusion &&
                                             x.Team != MyHero.Team &&
                                             !x.IsMagicImmune() && x.IsSpawned && x.IsRealUnit() &&
                                             x.Distance2D(enemy) <= radius);

                    var enemyDamage = damage;
                    enemyDamage += additionalTargetCount.Count(x => !(x is Hero)) * damagePerUnit;
                    enemyDamage += additionalTargetCount.Count(x => x is Hero) * damagePerHero;
                    enemyDamage *= spellAmp;

                    if (enemy.Health <= enemyDamage * (1 - enemy.MagicResistance()))
                    {
                        var predictedPos = Prediction.Prediction.PredictPosition(enemy,
                            (int) (odds.FindCastPoint() * 1000.0));
                        Log.Debug(
                            $"using odds to killsteal! {enemyDamage} units: {additionalTargetCount.Count(x => !(x is Hero))} heroes: {additionalTargetCount.Count(x => x is Hero)}");

                        foreach (var unit in additionalTargetCount)
                        {
                            Log.Debug($"{unit.Name}");
                        }
                        odds.UseAbility(predictedPos);
                        await Await.Delay(GetAbilityDelay(enemy, odds));
                        return true;
                    }
                }
            }

            return false;
        }

        public override async Task ExecuteComboAsync(Unit target, CancellationToken tk = new CancellationToken())
        {
            if (MyHero.HasModifier("modifier_legion_commander_duel"))
            {
                return;
            }
            // maybe got some pre damage
            var odds = MyHero.Spellbook.SpellQ;
            if (odds.CanBeCasted(target) && MyHero.Mana > 300 && odds.CanHit(target))
            {
                var radius = odds.GetAbilityData("radius");
                var targets =
                    ObjectManager.GetEntitiesParallel<Unit>()
                                 .Where(
                                     x =>
                                         x.IsAlive && x.Team != MyHero.Team && x != target && !x.IsMagicImmune() &&
                                         x.IsRealUnit() &&
                                         x.Distance2D(target) <= radius);
                var heroes = targets.Where(x => x is Hero);
                if (heroes.Any() || targets.Count() >= 5)
                {
                    Log.Debug($"Using Q with {heroes.Count()} heroes and {targets.Count()} targets");

                    var predictedPos = Prediction.Prediction.PredictPosition(target,
                        (int) (odds.FindCastPoint() * 1000.0));
                    odds.UseAbility(predictedPos);
                    await Await.Delay(GetAbilityDelay(target, odds), tk);
                }
                else
                {
                    Log.Debug($"NOT using Q sionce only {heroes.Count()} heroes and {targets.Count()} targets");
                }
            }

            await UseItems(tk);

            // press the attack for teh damage
            var duel = MyHero.Spellbook.SpellR;
            if (IsInRange(duel.GetCastRange()))
            {
                var enemyHealth = (float) target.Health / target.MaximumHealth;
                if (!MyHero.HasModifier("modifier_press_the_attack") && enemyHealth > 0.33f)
                {
                    var pressTheAttack = MyHero.Spellbook.SpellW;
                    if (pressTheAttack.CanBeCasted())
                    {
                        pressTheAttack.UseAbility(MyHero);
                        await Await.Delay((int) (pressTheAttack.FindCastPoint() * 1000.0 + Game.Ping), tk);
                    }
                }
                var armlet = MyHero.GetItemById(ItemId.item_armlet);
                if (armlet != null && !armlet.IsToggled)
                {
                    Log.Debug($"toggling armlet");
                    armlet.ToggleAbility();
                }
            }
            // check if we are near the enemy
            if (!await MoveOrBlinkToEnemy(tk))
            {
                return;
            }
            // make him disabled
            if (await DisableEnemy(tk) == DisabledState.UsedAbilityToDisable)
            {
                Log.Debug($"disabled!");
                // return;
            }

            // test if ulti is good
            if (duel.CanBeCasted(target) && await HasNoLinkens(target, tk))
            {
                var bladeMail = MyHero.GetItemById(ItemId.item_blade_mail);
                if (bladeMail != null && bladeMail.CanBeCasted())
                {
                    Log.Debug($"using blademail");
                    bladeMail.UseAbility();
                    await Await.Delay(100, tk);
                }

                var lotus = MyHero.GetItemById(ItemId.item_lotus_orb);
                if (lotus != null && lotus.CanBeCasted())
                {
                    Log.Debug($"using lotus orb before call");
                    lotus.UseAbility(MyHero);
                    await Await.Delay(100, tk);
                }

                var mjollnir = MyHero.GetItemById(ItemId.item_mjollnir);
                if (mjollnir != null && mjollnir.CanBeCasted())
                {
                    Log.Debug($"using mjollnir before call");
                    mjollnir.UseAbility(MyHero);
                    await Await.Delay(100, tk);
                }

                Log.Debug($"using duel");
                duel.UseAbility(target);
                await Await.Delay(GetAbilityDelay(target, duel), tk);
                return;
            }
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