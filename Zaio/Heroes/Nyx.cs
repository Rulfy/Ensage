using System;
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
    [Hero(ClassID.CDOTA_Unit_Hero_Nyx_Assassin)]
    internal class Nyx : ComboHero
    {
        private static readonly ILog Log = AssemblyLogs.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private static readonly string[] SupportedAbilities =
        {
            "nyx_assassin_impale",
            "nyx_assassin_mana_burn",
            "nyx_assassin_spiked_carapace",
            "nyx_assassin_vendetta"
        };

        private static readonly string[] KillstealAbilities =
        {
            "nyx_assassin_impale",
            "nyx_assassin_mana_burn"
        };

        private Ability _manaBurnAbility;
        private Ability _stunAbility;

        private Ability _ultAbility;

        public override void OnLoad()
        {
            base.OnLoad();

            var heroMenu = new Menu("Nyx", "zaioNyx", false, "npc_dota_hero_nyx_assassin", true);

            heroMenu.AddItem(new MenuItem("zaioNyxAbilitiesText", "Supported Abilities"));
            var supportedStuff = new MenuItem("zaioNyxAbilities", string.Empty);
            supportedStuff.SetValue(new AbilityToggler(SupportedAbilities.ToDictionary(x => x, y => true)));
            heroMenu.AddItem(supportedStuff);

            heroMenu.AddItem(new MenuItem("zaioNyxKillstealAbilitiesText", "Supported Killsteal Abilities"));
            var supportedKillsteal = new MenuItem("zaioNyxKillstealAbilities", string.Empty);
            supportedKillsteal.SetValue(new AbilityToggler(KillstealAbilities.ToDictionary(x => x, y => true)));
            heroMenu.AddItem(supportedKillsteal);

            OnLoadMenuItems(supportedStuff, supportedKillsteal);

            ZaioMenu.LoadHeroSettings(heroMenu);

            _ultAbility = MyHero.GetAbilityById(AbilityId.nyx_assassin_vendetta);
            _manaBurnAbility = MyHero.GetAbilityById(AbilityId.nyx_assassin_mana_burn);
            _stunAbility = MyHero.GetAbilityById(AbilityId.nyx_assassin_impale);
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

            if (Target == null && _stunAbility.IsKillstealAbilityEnabled() && _stunAbility.CanBeCasted())
            {
                var damage = (float) _stunAbility.GetDamage(_stunAbility.Level - 1);
                damage *= GetSpellAmp();
                var enemy =
                    ObjectManager.GetEntitiesParallel<Hero>()
                                 .FirstOrDefault(
                                     x =>
                                         x.IsAlive && x.Team != MyHero.Team && !x.IsIllusion &&
                                         _stunAbility.CanBeCasted(x) &&
                                         _stunAbility.CanHit(x) &&
                                         x.Health < damage * (1 - x.MagicResistance()) && !x.IsLinkensProtected() &&
                                         !x.CantBeAttacked() && !x.CantBeKilled());

                if (enemy != null)
                {
                    Log.Debug($"use impale killsteal {enemy.Health} < {damage * (1 - enemy.MagicResistance())}");

                    var castPoint = _stunAbility.FindCastPoint();
                    var speed = _stunAbility.GetAbilityData("speed");
                    var time = (castPoint + enemy.Distance2D(MyHero) / speed) * 1000.0f;

                    var predictedPos = Prediction.Prediction.PredictPosition(enemy, (int) time);

                    _stunAbility.UseAbility(predictedPos);

                    await Await.Delay(GetAbilityDelay(predictedPos, _stunAbility));
                    return true;
                }
            }

            if (_manaBurnAbility.IsKillstealAbilityEnabled() &&_manaBurnAbility.CanBeCasted())
            {
                var intMultiplier = _manaBurnAbility.GetAbilityData("float_multiplier");
                var enemy =
                    ObjectManager.GetEntitiesParallel<Hero>()
                                 .FirstOrDefault(
                                     x =>
                                         x.IsAlive && x.Team != MyHero.Team && !x.IsIllusion &&
                                         _manaBurnAbility.CanBeCasted(x) &&
                                         _manaBurnAbility.CanHit(x) && !x.IsLinkensProtected() && !x.CantBeAttacked() &&
                                         !x.CantBeKilled() &&
                                         x.Health <
                                         Math.Min(intMultiplier * x.TotalIntelligence, x.Mana) * GetSpellAmp() *
                                         (1 - x.MagicResistance()));

                if (enemy != null)
                {
                    Log.Debug(
                        $"use manaburn killsteal {enemy.Health} < {Math.Min(intMultiplier * enemy.TotalIntelligence, enemy.Mana) * GetSpellAmp() * (1 - enemy.MagicResistance())}");
                    _manaBurnAbility.UseAbility(enemy);
                    await Await.Delay(GetAbilityDelay(enemy, _manaBurnAbility));
                    return true;
                }
            }
            return false;
        }

        public override async Task ExecuteComboAsync(Unit target, CancellationToken tk = new CancellationToken())
        {
            if (!MyHero.HasModifier("modifier_nyx_assassin_vendetta") && !MyHero.IsSilenced())
            {
                var manaNeeded = _stunAbility.CanBeCasted(target) || !_stunAbility.IsAbilityEnabled() ? _stunAbility.ManaCost + 100 : 0;
                if (manaNeeded <= MyHero.Mana)
                {
                    await HasNoLinkens(target, tk);
                    await UseItems(target, tk);

                    // make him disabled
                    if (await DisableEnemy(target, tk) == DisabledState.UsedAbilityToDisable)
                    {
                        Log.Debug($"disabled!");
                        // return;
                    }
                }
                if (_stunAbility.IsAbilityEnabled() && _stunAbility.CanBeCasted(target) && _stunAbility.CanHit(target))
                {
                    var castPoint = _stunAbility.FindCastPoint();
                    var speed = _stunAbility.GetAbilityData("speed");
                    var time = (castPoint + target.Distance2D(MyHero) / speed) * 1000.0f;

                    var predictedPos = Prediction.Prediction.PredictPosition(target, (int) time);
                    if (MyHero.Distance2D(predictedPos) <= _stunAbility.GetCastRange())
                    {
                        _stunAbility.UseAbility(predictedPos);

                        Log.Debug($"Use stun");
                        await Await.Delay(GetAbilityDelay(predictedPos, _stunAbility), tk);
                    }
                }

                Log.Debug($"Use manaburn {_manaBurnAbility.CanBeCasted(target)} | {_manaBurnAbility.CanHit(target)}");
                if (_manaBurnAbility.IsAbilityEnabled() && _manaBurnAbility.CanBeCasted(target) && target.Mana > 100 && _manaBurnAbility.CanHit(target))
                {
                    _manaBurnAbility.UseAbility(target);
                    Log.Debug($"Use manaburn");
                    await Await.Delay(GetAbilityDelay(target, _manaBurnAbility), tk);
                }
            }

            // check if we are near the enemy

            if (!await MoveOrBlinkToEnemy(target, tk))
            {
                if (_ultAbility.IsAbilityEnabled() && !MyHero.IsSilenced() && _ultAbility.CanBeCasted() &&
                    !MyHero.HasModifier("modifier_nyx_assassin_vendetta"))
                {
                    Log.Debug($"going invis boys since too far");
                    _ultAbility.UseAbility();
                    await Await.Delay(125, tk);
                }
                Log.Debug($"move or blink");
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