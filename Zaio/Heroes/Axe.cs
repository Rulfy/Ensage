using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Ensage;
using Ensage.Common.Enums;
using Ensage.Common.Extensions;
using Ensage.Common.Extensions.SharpDX;
using Ensage.Common.Menu;
using Ensage.Common.Threading;
using log4net;
using PlaySharp.Toolkit.Logging;
using Zaio.Helpers;
using Zaio.Interfaces;

namespace Zaio.Heroes
{
    [Hero(ClassID.CDOTA_Unit_Hero_Axe)]
    internal class Axe : ComboHero
    {
        private static readonly ILog Log = AssemblyLogs.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private static readonly string[] SupportedAbilities =
        {
            "axe_berserkers_call",
            "axe_culling_blade",
            "item_blade_mail",
            "item_lotus_orb",
            "item_mjollnir"
        };

        private static readonly string[] KillstealAbilities =
        {
            "axe_culling_blade"
        };

        private Ability _callAbility;
        private Ability _ultAbility;

        public override void OnLoad()
        {
            base.OnLoad();

            var heroMenu = new Menu("Axe", "zaioAxe", false, "npc_dota_hero_axe", true);

            heroMenu.AddItem(new MenuItem("zaioAxeAbilitiesText", "Supported Abilities"));
            var supportedStuff = new MenuItem("zaioAxeAbilities", string.Empty);
            supportedStuff.SetValue(new AbilityToggler(SupportedAbilities.ToDictionary(x => x, y => true)));
            heroMenu.AddItem(supportedStuff);

            heroMenu.AddItem(new MenuItem("zaioAxeKillstealAbilitiesText", "Supported Killsteal Abilities"));
            var supportedKillsteal = new MenuItem("zaioAxeKillstealAbilities", string.Empty);
            supportedKillsteal.SetValue(new AbilityToggler(KillstealAbilities.ToDictionary(x => x, y => true)));
            heroMenu.AddItem(supportedKillsteal);

            ZaioMenu.LoadHeroSettings(heroMenu);

            _callAbility = MyHero.GetAbilityById(AbilityId.axe_berserkers_call);
            _ultAbility = MyHero.GetAbilityById(AbilityId.axe_culling_blade);
        }

        protected async Task<bool> MoveOrBlinkToEnemyAxe(Unit target, CancellationToken tk = default(CancellationToken), float minimumRange = 0.0f, float maximumRange = 0.0f)
        {
            var distance = MyHero.Distance2D(target) - target.HullRadius - MyHero.HullRadius;

            var testRange = maximumRange == 0.0f ? MyHero.GetAttackRange() : maximumRange;
            if (distance <= testRange)
            {
                return true;
            }

            if (!MyHero.IsMuted())
            {
                if (ZaioMenu.ShouldUseBlinkDagger)
                {
                    var blink = MyHero.Inventory.Items.FirstOrDefault(x => x.ClassID == ClassID.CDOTA_Item_BlinkDagger);
                    if (blink != null && blink.CanBeCasted())
                    {
                        var blinkRange = blink.AbilitySpecialData.First(x => x.Name == "blink_range").Value;
                        if (distance <= blinkRange)
                        {
                            if (minimumRange == 0.0f)
                            {
                                minimumRange = MyHero.GetAttackRange() / 2;
                            }

                            var pos = (target.NetworkPosition - MyHero.NetworkPosition).Normalized();
                            pos *= minimumRange;
                            pos = target.NetworkPosition - pos;
                            if (target.IsMoving)
                            {
                               var moves = Ensage.Common.Prediction.InFront(target, 75);
                               blink.UseAbility(moves);
                            }
                            blink.UseAbility(pos);
                            await Await.Delay((int) (MyHero.GetTurnTime(pos) * 1000) + ItemDelay, tk);
                            return false;
                        }
                    }
                }
                var phaseBoots = MyHero.Inventory.Items.FirstOrDefault(x => x.Name == "item_phase_boots");
                if (phaseBoots != null && phaseBoots.CanBeCasted() && !MyHero.IsInvisible())
                {
                    phaseBoots.UseAbility();
                    await Await.Delay(ItemDelay, tk);
                }
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
            return false;
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

            if (_ultAbility.CanBeCasted())
            {
                var threshold =
                    _ultAbility.GetAbilityData(MyHero.HasItem(ClassID.CDOTA_Item_UltimateScepter)
                        ? "kill_threshold_scepter"
                        : "kill_threshold");

                var enemy = ObjectManager.GetEntitiesParallel<Hero>().FirstOrDefault(
                    x =>
                        x.IsValid && x.IsAlive && x.Team != MyHero.Team && !x.IsIllusion &&
                        _ultAbility.CanBeCasted(x) && _ultAbility.CanHit(x) && x.Health < threshold &&
                        !x.IsLinkensProtected() &&
                        !x.CantBeAttacked() && !x.CantBeKilledByAxeUlt());
                if (enemy != null)
                {
                    Log.Debug($"using ult on {enemy.Name}: {enemy.Health} < {threshold}");
                    _ultAbility.UseAbility(enemy);
                    await Await.Delay(GetAbilityDelay(enemy, _ultAbility));
                    return true;
                }
            }
            return false;
        }


        public override async Task ExecuteComboAsync(Unit target, CancellationToken tk = new CancellationToken())
        {
            _ultAbility = MyHero.Spellbook.SpellR;
            if (!MyHero.IsSilenced() && _ultAbility.CanBeCasted(target) && _ultAbility.CanHit(target) &&
                await HasNoLinkens(target, tk))
            {
                var threshold =
                    _ultAbility.GetAbilityData(MyHero.HasItem(ClassID.CDOTA_Item_UltimateScepter)
                        ? "kill_threshold_scepter"
                        : "kill_threshold");
                if (target.Health < threshold)
                {
                    Log.Debug($"using ult {target.Health} < {threshold}");
                    _ultAbility.UseAbility(target);
                    await Await.Delay(GetAbilityDelay(target, _ultAbility), tk);
                }
            }

            await HasNoLinkens(target, tk);

            // check if we are near the enemy
            if (!await MoveOrBlinkToEnemyAxe(target, tk))
            {
                Log.Debug($"return because of blink");
                return;
            }

            _callAbility = MyHero.Spellbook.SpellQ;
            if (!MyHero.IsSilenced() && _callAbility.CanBeCasted(target))
            {
                var delay = _callAbility.FindCastPoint() * 1000 + Game.Ping;
                var radius = _callAbility.GetAbilityData("radius");
                if (Prediction.Prediction.PredictPosition(target, (int) delay).Distance2D(MyHero) <= radius)
                {
                    
                    var useCall = true;
                    if (target.HasModifier("modifier_legion_commander_duel") || target.PhysicalResistance() == 1.0f)
                    {
                        useCall =
                            ObjectManager.GetEntitiesParallel<Hero>()
                                         .Any(
                                             x =>
                                                 x.IsValid && x.IsAlive && !x.IsIllusion && x.Team != MyHero.Team &&
                                                 x != target && x.Distance2D(MyHero) <= radius);
                    }
                    if (useCall)
                    {
                        Log.Debug($"using call");
                        _callAbility.UseAbility();
                        await Await.Delay((int) (_callAbility.FindCastPoint() * 1000.0 + Game.Ping), tk);
                    }

                    var bladeMail = MyHero.GetItemById(ItemId.item_blade_mail);
                    if (bladeMail != null && bladeMail.CanBeCasted())
                    {
                        Log.Debug($"using blademail after call");
                        bladeMail.UseAbility();
                        await Await.Delay(ItemDelay, tk);
                    }

                    var lotus = MyHero.GetItemById(ItemId.item_lotus_orb);
                    if (lotus != null && lotus.CanBeCasted())
                    {
                        Log.Debug($"using lotus orb after call");
                        lotus.UseAbility(MyHero);
                        await Await.Delay(ItemDelay, tk);
                    }

                    var mjollnir = MyHero.GetItemById(ItemId.item_mjollnir);
                    if (mjollnir != null && mjollnir.CanBeCasted())
                    {
                        Log.Debug($"using mjollnir after call");
                        mjollnir.UseAbility(MyHero);
                        await Await.Delay(ItemDelay, tk);
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
