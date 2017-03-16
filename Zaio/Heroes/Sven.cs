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
using AbilityId = Ensage.Common.Enums.AbilityId;

namespace Zaio.Heroes
{
    [Hero(ClassID.CDOTA_Unit_Hero_Sven)]
    internal class Sven : ComboHero
    {
        private static readonly ILog Log = AssemblyLogs.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private static readonly string[] SupportedAbilities =
        {
            "sven_storm_bolt",
            "sven_warcry",
            "sven_gods_strength",
            "item_mask_of_madness"
        };

        private static readonly string[] KillstealAbilities =
        {
            "sven_storm_bolt"
        };

        private Ability _stormboltAbility;
        private Ability _ultAbility;
        private Ability _warcryAbility;

        public override void OnLoad()
        {
            base.OnLoad();

            var heroMenu = new Menu("Sven", "zaioSven", false, "npc_dota_hero_sven", true);

            heroMenu.AddItem(new MenuItem("zaioSvenAbilitiesText", "Supported Abilities"));
            var supportedStuff = new MenuItem("zaioSvenAbilities", string.Empty);
            supportedStuff.SetValue(new AbilityToggler(SupportedAbilities.ToDictionary(x => x, y => true)));
            heroMenu.AddItem(supportedStuff);

            heroMenu.AddItem(new MenuItem("zaioSvenKillstealAbilitiesText", "Supported Killsteal Abilities"));
            var supportedKillsteal = new MenuItem("zaioSvenKillstealAbilities", string.Empty);
            supportedKillsteal.SetValue(new AbilityToggler(KillstealAbilities.ToDictionary(x => x, y => true)));
            heroMenu.AddItem(supportedKillsteal);

            OnLoadMenuItems(supportedStuff, supportedKillsteal);

            ZaioMenu.LoadHeroSettings(heroMenu);

            _stormboltAbility = MyHero.GetAbilityById(AbilityId.sven_storm_bolt);
            _warcryAbility = MyHero.GetAbilityById(AbilityId.sven_warcry);
            _ultAbility = MyHero.GetAbilityById(AbilityId.sven_gods_strength);
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

            if (_stormboltAbility.IsKillstealAbilityEnabled() && _stormboltAbility.CanBeCasted())
            {
                var damage = (float) _stormboltAbility.GetDamage(_stormboltAbility.Level - 1);
                damage *= GetSpellAmp();

                var enemy =
                    ObjectManager.GetEntitiesParallel<Hero>()
                                 .FirstOrDefault(
                                     x =>
                                         x.IsAlive && x.Team != MyHero.Team && !x.IsIllusion &&
                                         _stormboltAbility.CanBeCasted(x) &&
                                         _stormboltAbility.CanHit(x) && !x.IsMagicImmune() && !x.IsLinkensProtected() &&
                                         x.Health < damage * (1 - x.MagicResistance()) && !x.CantBeAttacked() &&
                                         !x.CantBeKilled());
                if (enemy != null)
                {
                    Log.Debug(
                        $"use killsteal stormbolt because enough damage {enemy.Health} <= {damage * (1 - enemy.MagicResistance())} ");
                    _stormboltAbility.UseAbility(enemy);
                    await Await.Delay(GetAbilityDelay(enemy, _stormboltAbility));
                    return true;
                }
            }

            return false;
        }


        public override async Task ExecuteComboAsync(Unit target, CancellationToken tk = new CancellationToken())
        {
            await HasNoLinkens(target, tk);
            await UseItems(target, tk);

            if (await DisableEnemy(target, tk) == DisabledState.UsedAbilityToDisable)
            {
                Log.Debug($"disabled!");
            }

            if (!MyHero.IsSilenced())
            {
                if (_stormboltAbility.IsAbilityEnabled() && _stormboltAbility.CanBeCasted(target) && _stormboltAbility.CanHit(target))
                {
                    _stormboltAbility.UseAbility(target);
                    Log.Debug($"stormbolt used");
                    await Await.Delay((int) (_stormboltAbility.FindCastPoint() * 1000.0 + Game.Ping), tk);
                }

                if (MyHero.Distance2D(target) <= 400)
                {
                    if (_warcryAbility.IsAbilityEnabled() && _warcryAbility.CanBeCasted())
                    {
                        _warcryAbility.UseAbility();
                        Log.Debug($"warcry used");
                        await Await.Delay(100, tk);
                    }

                    if (_ultAbility.IsAbilityEnabled() && _ultAbility.CanBeCasted())
                    {
                        Log.Debug($"use ult");
                        _ultAbility.UseAbility();
                        await Await.Delay(100, tk);
                    }
                }
            }

            // check if we are near the enemy
            if (!await MoveOrBlinkToEnemy(target, tk))
            {
                Log.Debug($"return because of blink");
                return;
            }
            //cast mom if all of our skills are on cooldown
            var mom = MyHero.GetItemById(ItemId.item_mask_of_madness);
            if (mom != null && mom.IsAbilityEnabled() && MyHero.CanAttack() && !_stormboltAbility.CanBeCasted() &&
                !_warcryAbility.CanBeCasted() && !_ultAbility.CanBeCasted() && mom.CanBeCasted())
            {
                Log.Debug($"Use mom");
                mom.UseAbility();
                await Await.Delay(250, tk);
            }

            if (ZaioMenu.ShouldUseOrbwalker && !target.IsStunned())
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