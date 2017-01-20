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

            var impale = MyHero.Spellbook.SpellQ;
            if (Target == null && impale.CanBeCasted())
            {
                var damage = (float) impale.GetDamage(impale.Level - 1);
                damage *= GetSpellAmp();
                var enemy =
                    ObjectManager.GetEntitiesParallel<Hero>()
                                 .FirstOrDefault(
                                     x =>
                                         x.IsAlive && x.Team != MyHero.Team && !x.IsIllusion && impale.CanBeCasted(x) &&
                                         impale.CanHit(x) &&
                                         x.Health < damage * (1 - x.MagicResistance()) && !x.IsLinkensProtected() &&
                                         !x.CantBeAttacked() && !x.CantBeKilled());

                if (enemy != null)
                {
                    Log.Debug($"use impale killsteal {enemy.Health} < {damage * (1 - enemy.MagicResistance())}");

                    var castPoint = impale.FindCastPoint();
                    var speed = impale.GetAbilityData("speed");
                    var time = (castPoint + enemy.Distance2D(MyHero) / speed) * 1000.0f;

                    var predictedPos = Prediction.Prediction.PredictPosition(enemy, (int) time);

                    impale.UseAbility(predictedPos);

                    await Await.Delay((int) (castPoint * 1000.0 + Game.Ping));
                    return true;
                }
            }

            var manaBurn = MyHero.Spellbook.SpellW;
            if (manaBurn.CanBeCasted())
            {
                var intMultiplier = manaBurn.GetAbilityData("float_multiplier");
                var enemy =
                    ObjectManager.GetEntitiesParallel<Hero>()
                                 .FirstOrDefault(
                                     x =>
                                         x.IsAlive && x.Team != MyHero.Team && !x.IsIllusion && manaBurn.CanBeCasted(x) &&
                                         manaBurn.CanHit(x) && !x.IsLinkensProtected() && !x.CantBeAttacked() &&
                                         !x.CantBeKilled() &&
                                         x.Health <
                                         Math.Min(intMultiplier * x.TotalIntelligence, x.Mana) * GetSpellAmp() *
                                         (1 - x.MagicResistance()));

                if (enemy != null)
                {
                    Log.Debug(
                        $"use manaburn killsteal {enemy.Health} < {Math.Min(intMultiplier * enemy.TotalIntelligence, enemy.Mana) * GetSpellAmp() * (1 - enemy.MagicResistance())}");
                    manaBurn.UseAbility(enemy);
                    await Await.Delay((int) (manaBurn.FindCastPoint() * 1000.0 + Game.Ping));
                    return true;
                }
            }
            return false;
        }

        public override async Task ExecuteComboAsync(Unit target, CancellationToken tk = new CancellationToken())
        {
            if (!MyHero.HasModifier("modifier_nyx_assassin_vendetta"))
            {
                var stun = MyHero.Spellbook.SpellQ;

                var manaNeeded = stun.CanBeCasted(Target) ? stun.ManaCost + 100 : 0;
                if (manaNeeded <= MyHero.Mana)
                {
                    await HasNoLinkens(Target, tk);
                    await UseItems(tk);

                    // make him disabled
                    if (await DisableEnemy(tk) == DisabledState.UsedAbilityToDisable)
                    {
                        Log.Debug($"disabled!");
                        // return;
                    }
                }
                if (stun.CanBeCasted(Target) && stun.CanHit(Target))
                {
                    var castPoint = stun.FindCastPoint();
                    var speed = stun.GetAbilityData("speed");
                    var time = (castPoint + Target.Distance2D(MyHero) / speed) * 1000.0f;

                    var predictedPos = Prediction.Prediction.PredictPosition(Target, (int) time);
                    if (MyHero.Distance2D(predictedPos) <= stun.GetCastRange())
                    {
                        stun.UseAbility(predictedPos);

                        Log.Debug($"Use stun");
                        await Await.Delay((int) (castPoint * 1000.0 + Game.Ping), tk);
                    }
                }

                var manaBurn = MyHero.Spellbook.SpellW;
                Log.Debug($"Use manaburn {manaBurn.CanBeCasted(Target)} | {manaBurn.CanHit(Target)}");
                if (manaBurn.CanBeCasted(Target) && Target.Mana > 100 && manaBurn.CanHit(Target))
                {
                    manaBurn.UseAbility(Target);
                    Log.Debug($"Use manaburn");
                    await Await.Delay((int) (manaBurn.FindCastPoint() * 1000.0 + Game.Ping), tk);
                }
            }

            // check if we are near the enemy
            var ult = MyHero.GetAbilityById(AbilityId.nyx_assassin_vendetta);
            if (!await MoveOrBlinkToEnemy(tk))
            {
                if (ult.CanBeCasted() && !MyHero.HasModifier("modifier_nyx_assassin_vendetta"))
                {
                    Log.Debug($"going invis boys since too far");
                    ult.UseAbility();
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
                MyHero.Attack(Target);
                await Await.Delay(125, tk);
            }
        }
    }
}