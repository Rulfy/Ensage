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
            "item_lotus_orb",
            "item_mjollnir",
            "item_armlet"
        };

        private static readonly string[] KillstealAbilities =
        {
            "legion_commander_overwhelming_odds"
        };

        private Ability _duelAbility;
        private Ability _oddsAbility;
        private Ability _pressTheAttackAbility;

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

            OnLoadMenuItems(supportedStuff, supportedKillsteal);

            ZaioMenu.LoadHeroSettings(heroMenu);

            _oddsAbility = MyHero.GetAbilityById(AbilityId.legion_commander_overwhelming_odds);
            _duelAbility = MyHero.GetAbilityById(AbilityId.legion_commander_duel);
            _pressTheAttackAbility = MyHero.GetAbilityById(AbilityId.legion_commander_press_the_attack);
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

            if (_oddsAbility.IsKillstealAbilityEnabled() && _oddsAbility.CanBeCasted())
            {
                var damage = _oddsAbility.GetAbilityData("damage");
                var damagePerUnit = _oddsAbility.GetAbilityData("damage_per_unit");
                var damagePerHero = _oddsAbility.GetAbilityData("damage_per_hero");
                var radius = _oddsAbility.GetAbilityData("radius");

                var enemies =
                    ObjectManager.GetEntitiesParallel<Hero>()
                                 .Where(
                                     x =>
                                         x.IsAlive && x.Team != MyHero.Team && _oddsAbility.CanBeCasted(x) &&
                                         _oddsAbility.CanHit(x) &&
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
                            (int) (_oddsAbility.FindCastPoint() * 1000.0));
                        Log.Debug(
                            $"using odds to killsteal! {enemyDamage} units: {additionalTargetCount.Count(x => !(x is Hero))} heroes: {additionalTargetCount.Count(x => x is Hero)}");

                        foreach (var unit in additionalTargetCount)
                        {
                            Log.Debug($"{unit.Name}");
                        }
                        _oddsAbility.UseAbility(predictedPos);
                        await Await.Delay(GetAbilityDelay(enemy, _oddsAbility));
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
            if (!MyHero.IsSilenced() && _oddsAbility.IsAbilityEnabled() && _oddsAbility.CanBeCasted(target) && MyHero.Mana > 300 &&
                _oddsAbility.CanHit(target))
            {
                var radius = _oddsAbility.GetAbilityData("radius");
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
                        (int) (_oddsAbility.FindCastPoint() * 1000.0));
                    _oddsAbility.UseAbility(predictedPos);
                    await Await.Delay(GetAbilityDelay(target, _oddsAbility), tk);
                }
                else
                {
                    Log.Debug($"NOT using Q sionce only {heroes.Count()} heroes and {targets.Count()} targets");
                }
            }

            await UseItems(target, tk);

            // press the attack for teh damage
            if (_duelAbility.IsAbilityEnabled() && IsInRange(_duelAbility.GetCastRange()))
            {
                var enemyHealth = (float) target.Health / target.MaximumHealth;
                if (!MyHero.IsSilenced() && !MyHero.HasModifier("modifier_press_the_attack") && enemyHealth > 0.33f)
                {
                    if (_pressTheAttackAbility.IsAbilityEnabled() && _pressTheAttackAbility.CanBeCasted())
                    {
                        _pressTheAttackAbility.UseAbility(MyHero);
                        await Await.Delay((int) (_pressTheAttackAbility.FindCastPoint() * 1000.0 + Game.Ping), tk);
                    }
                }
                var armlet = MyHero.GetItemById(ItemId.item_armlet);
                if (armlet != null && armlet.IsAbilityEnabled() && !armlet.IsToggled)
                {
                    Log.Debug($"toggling armlet");
                    armlet.ToggleAbility();
                }
            }
            // check if we are near the enemy
            if (!await MoveOrBlinkToEnemy(target, tk))
            {
                return;
            }
            // make him disabled
            if (await DisableEnemy(target, tk) == DisabledState.UsedAbilityToDisable)
            {
                Log.Debug($"disabled!");
                // return;
            }

            // test if ulti is good
            if (!MyHero.IsSilenced() && _duelAbility.IsAbilityEnabled() && _duelAbility.CanBeCasted(target) && await HasNoLinkens(target, tk))
            {
                var bladeMail = MyHero.GetItemById(ItemId.item_blade_mail);
                if (bladeMail != null && bladeMail.IsAbilityEnabled() && bladeMail.CanBeCasted())
                {
                    Log.Debug($"using blademail");
                    bladeMail.UseAbility();
                    await Await.Delay(ItemDelay, tk);
                }

                var lotus = MyHero.GetItemById(ItemId.item_lotus_orb);
                if (lotus != null && lotus.IsAbilityEnabled() && lotus.CanBeCasted())
                {
                    Log.Debug($"using lotus orb before call");
                    lotus.UseAbility(MyHero);
                    await Await.Delay(ItemDelay, tk);
                }

                var mjollnir = MyHero.GetItemById(ItemId.item_mjollnir);
                if (mjollnir != null &&mjollnir.IsAbilityEnabled() && mjollnir.CanBeCasted())
                {
                    Log.Debug($"using mjollnir before call");
                    mjollnir.UseAbility(MyHero);
                    await Await.Delay(ItemDelay, tk);
                }

                Log.Debug($"using duel");
                _duelAbility.UseAbility(target);
                await Await.Delay(GetAbilityDelay(target, _duelAbility), tk);
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