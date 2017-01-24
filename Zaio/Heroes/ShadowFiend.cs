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
    [Hero(ClassID.CDOTA_Unit_Hero_Nevermore)]
    internal class ShadowFiend : ComboHero
    {
        private static readonly ILog Log = AssemblyLogs.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private static readonly string[] SupportedAbilities =
        {
            "nevermore_shadowraze1",
            //"nevermore_shadowraze2",
            //"nevermore_shadowraze3",
            "nevermore_requiem"
        };

        private static readonly string[] KillstealAbilities =
        {
            "nevermore_shadowraze1"
        };

        private Ability _raze1Ability;
        private Ability _raze2Ability;
        private Ability _raze3Ability;
        private Ability _ultAbility;

        public override void OnLoad()
        {
            base.OnLoad();

            var heroMenu = new Menu("ShadowFiend", "zaioShadowFiend", false, "npc_dota_hero_nevermore", true);

            heroMenu.AddItem(new MenuItem("zaioShadowFiendAbilitiesText", "Supported Abilities"));
            var supportedStuff = new MenuItem("zaioShadowFiendAbilities", string.Empty);
            supportedStuff.SetValue(new AbilityToggler(SupportedAbilities.ToDictionary(x => x, y => true)));
            heroMenu.AddItem(supportedStuff);

            heroMenu.AddItem(new MenuItem("zaioShadowFiendKillstealAbilitiesText", "Supported Killsteal Abilities"));
            var supportedKillsteal = new MenuItem("zaioShadowFiendKillstealAbilities", string.Empty);
            supportedKillsteal.SetValue(new AbilityToggler(KillstealAbilities.ToDictionary(x => x, y => true)));
            heroMenu.AddItem(supportedKillsteal);

            ZaioMenu.LoadHeroSettings(heroMenu);

            _raze1Ability = MyHero.GetAbilityById(AbilityId.nevermore_shadowraze1);
            _raze2Ability = MyHero.GetAbilityById(AbilityId.nevermore_shadowraze2);
            _raze3Ability = MyHero.GetAbilityById(AbilityId.nevermore_shadowraze3);
            _ultAbility = MyHero.GetAbilityById(AbilityId.nevermore_requiem);
        }

        private async Task<bool> UseKillstealRaze(Ability ability, float spellAmp)
        {
            if (ability.CanBeCasted())
            {
                var range = ability.GetAbilityData("shadowraze_range");
                var radius = ability.GetAbilityData("shadowraze_radius");
                var point = MyHero.InFront(range);
                var damage = ability.GetDamage(ability.Level - 1) * spellAmp;
                var delay = ability.FindCastPoint() * 1000.0f;
                var enemy =
                    ObjectManager.GetEntitiesParallel<Hero>()
                                 .FirstOrDefault(
                                     x =>
                                         x.IsAlive && x.Team != MyHero.Team && !x.IsIllusion && ability.CanBeCasted(x) &&
                                         x.Health < damage * (1 - x.MagicResistance()) && !x.CantBeAttacked() &&
                                         !x.CantBeKilled() &&
                                         Prediction.Prediction.PredictPosition(x, (int) delay).Distance2D(point) <=
                                         radius);
                if (enemy != null)
                {
                    Log.Debug($"using {ability.Name} on {enemy.Name}");
                    ability.UseAbility();
                    await Await.Delay((int) (ability.FindCastPoint() * 1000 + Game.Ping));
                    return true;
                }
            }
            return false;
        }

        private async Task<bool> UseRazeOnTarget(Unit target, Ability ability)
        {
            if (ability.CanBeCasted(target) && !target.IsMagicImmune())
            {
                var range = ability.GetAbilityData("shadowraze_range");
                var radius = ability.GetAbilityData("shadowraze_radius");
                var point = MyHero.InFront(range);
                var delay = ability.FindCastPoint() * 1000.0f;
                var pos = Prediction.Prediction.PredictPosition(target, (int) delay);
                if (pos.Distance2D(point) <= radius)
                {
                    Log.Debug($"using {ability.Name}");
                    ability.UseAbility();
                    await Await.Delay((int) (ability.FindCastPoint() * 1000 + Game.Ping));
                    return true;
                }
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

            var spellAmp = GetSpellAmp();
            if (await UseKillstealRaze(_raze1Ability, spellAmp))
            {
                return true;
            }
            if (await UseKillstealRaze(_raze2Ability, spellAmp))
            {
                return true;
            }
            if (await UseKillstealRaze(_raze3Ability, spellAmp))
            {
                return true;
            }

            return false;
        }


        public override async Task ExecuteComboAsync(Unit target, CancellationToken tk = new CancellationToken())
        {
            var eulsModifier = target.FindModifier("modifier_eul_cyclone");
            if (eulsModifier == null && _ultAbility.CanBeCasted(target) && !MyHero.IsVisibleToEnemies ||
                eulsModifier != null && _ultAbility.CanBeCasted())
            {
                if (MyHero.IsInvisible() || eulsModifier != null)
                {
                    // Log.Debug($"using invis ult on enemy");
                    var distance = target.Distance2D(MyHero);
                    if (_ultAbility.IsInAbilityPhase)
                    {
                        if (distance > 400)
                        {
                            Log.Debug($"stopping ult since enemy too far!");
                            MyHero.Stop();
                            await Await.Delay(100, tk);
                        }
                        else
                        {
                            Log.Debug($"dist{distance}");
                            return;
                        }
                    }
                    if (distance > 50)
                    {
                        Log.Debug($"approaching target {distance}");
                        MyHero.Move(target.NetworkPosition);
                    }
                    else if (eulsModifier == null || eulsModifier.RemainingTime < _ultAbility.FindCastPoint())
                    {
                        Log.Debug($"{_ultAbility.IsInAbilityPhase}");
                        if (!_ultAbility.IsInAbilityPhase)
                        {
                            Log.Debug($"using ult on {target.Name}");
                            _ultAbility.UseAbility();
                            await Await.Delay(250, tk);
                        }
                    }
                    return;
                }
                else
                {
                    var shadowBlade = MyHero.GetItemById(ItemId.item_invis_sword) ??
                                      MyHero.GetItemById(ItemId.item_silver_edge);
                    var distance = MyHero.Distance2D(target);
                    if (shadowBlade != null && shadowBlade.CanBeCasted() && distance < 6000)
                    {
                        Log.Debug($"using invis");
                        shadowBlade.UseAbility();
                        await Await.Delay(500, tk);
                        return;
                    }
                }
            }

            var euls = MyHero.GetItemById(ItemId.item_cyclone);
            if (euls != null && euls.CanBeCasted(target) && _ultAbility.CanBeCasted(target))
            {
                if (euls.CanHit(target))
                {
                    Log.Debug($"using euls to disable enemy before stun");
                    euls.UseAbility(target);
                    await Await.Delay(125, tk);
                    return;
                }
                // check if we are near the enemy
                if (!await MoveOrBlinkToEnemy(tk, 0.1f, euls.GetCastRange()))
                {
                    Log.Debug($"return because of blink and euls ready");
                    return;
                }
            }


            await HasNoLinkens(target, tk);
            await UseItems(tk);

            await UseRazeOnTarget(target, _raze1Ability);
            await UseRazeOnTarget(target, _raze2Ability);
            await UseRazeOnTarget(target, _raze3Ability);

            // make him disabled
            if (await DisableEnemy(tk) == DisabledState.UsedAbilityToDisable)
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