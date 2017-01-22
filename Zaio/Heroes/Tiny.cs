using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using Ensage;
using Ensage.Common;
using Ensage.Common.Combo;
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
    [Hero(ClassID.CDOTA_Unit_Hero_Tiny)]
    internal class Tiny : ComboHero
    {
        private static readonly ILog Log = AssemblyLogs.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private static readonly string[] SupportedAbilities =
        {
            "tiny_avalanche",
            "tiny_toss"
        };

        private Ability _avalancheAbility;

        private Combo _throwBackCombo;

        private MenuItem _throwBackComboKey;
        private MenuItem _throwBackHeroCount;
        private Ability _tossAbility;

        private int ThrowBackHeroCount => _throwBackHeroCount.GetValue<Slider>().Value;

        public override void OnLoad()
        {
            base.OnLoad();

            var heroMenu = new Menu("Tiny", "zaioTiny", false, "npc_dota_hero_tiny", true);

            heroMenu.AddItem(new MenuItem("zaioTinyAbilitiesText", "Supported Abilities"));
            var supportedStuff = new MenuItem("zaioTinyAbilities", string.Empty);
            supportedStuff.SetValue(new AbilityToggler(SupportedAbilities.ToDictionary(x => x, y => true)));
            heroMenu.AddItem(supportedStuff);

            _throwBackComboKey =
                new MenuItem("zaioTinyThrowBack", "Throw Back").SetValue(new KeyBind(0, KeyBindType.Press));
            _throwBackComboKey.Tooltip = "Throws an enemy back to your allies or under your tower.";
            _throwBackComboKey.ValueChanged += _throwBackComboKey_ValueChanged;
            heroMenu.AddItem(_throwBackComboKey);

            _throwBackHeroCount =
                new MenuItem("zaioTinyThrowBackHeroCount", "Throw Back Hero Count").SetValue(new Slider(2, 1, 4));
            _throwBackHeroCount.Tooltip = "How many allied heroes must be close to throw back an enemy.";
            heroMenu.AddItem(_throwBackHeroCount);

            ZaioMenu.LoadHeroSettings(heroMenu);

            _avalancheAbility = MyHero.GetAbilityById(AbilityId.tiny_avalanche);
            _tossAbility = MyHero.GetAbilityById(AbilityId.tiny_toss);

            _throwBackCombo = new Combo(ThrowBack,
                KeyInterop.KeyFromVirtualKey((int) _throwBackComboKey.GetValue<KeyBind>().Key));
            _throwBackCombo.Activate();
        }

        private async Task ThrowBack(CancellationToken tk)
        {
            var tossSource = TargetSelector.ClosestToMouse(MyHero);
            if (tossSource == null)
            {
                await Await.Delay(100, tk);
                return;
            }
            // test if toss/av combo is working
            if (_tossAbility.CanBeCasted())
            {
                Log.Debug($"use throwbacktoss");
                var tossRange = _tossAbility.GetCastRange();

                var grab = _tossAbility.GetAbilityData("grab_radius");
                var closestUnit =
                    ObjectManager.GetEntitiesParallel<Unit>()
                                 .Where(
                                     x =>
                                         x.IsValid && x.IsAlive && x != MyHero && x.Distance2D(MyHero) <= grab &&
                                         x.IsRealUnit())
                                 .OrderBy(x => x.Distance2D(MyHero))
                                 .FirstOrDefault();
                Log.Debug($"Closest unit for toss: {closestUnit?.Name}");
                // if (closestUnit == tossSource )
                {
                    // check if we can throw him to allied heroes (min 2) 
                    var targetHeroes =
                        ObjectManager.GetEntitiesParallel<Hero>()
                                     .Where(
                                         x =>
                                             x.IsValid && x.IsAlive && x.Team == MyHero.Team && x != MyHero &&
                                             x.Distance2D(MyHero) <= tossRange);
                    foreach (var target in targetHeroes)
                    {
                        if (
                            ObjectManager.GetEntitiesParallel<Hero>()
                                         .Count(
                                             x =>
                                                 x.IsValid && x.IsAlive && x.Team == MyHero.Team && x != target &&
                                                 x != MyHero &&
                                                 x.Distance2D(target) < 500) == ThrowBackHeroCount - 1)
                        {
                            if (closestUnit == tossSource)
                            {
                                _tossAbility.UseAbility(target);
                                Log.Debug($"use toss back to allied heroes! {target.Name}");
                                await Await.Delay(100, tk);
                            }
                            else
                            {
                                await MoveOrBlinkToEnemy(tk, 0.1f, 0.1f, tossSource);
                                Log.Debug($"return because of blink");
                                return;
                            }
                        }
                    }
                    // check if we can throw him under the tower
                    var towers =
                        ObjectManager.GetEntitiesParallel<Tower>()
                                     .Where(
                                         x =>
                                             x.IsValid && x.IsAlive && x.Team == MyHero.Team &&
                                             x.Distance2D(MyHero) - x.AttackRange <= tossRange);
                    var towerTarget =
                        ObjectManager.GetEntitiesParallel<Unit>()
                                     .Where(
                                         x =>
                                             x.IsValid && x.IsAlive && x.Team == MyHero.Team && !(x is Building) &&
                                             x.Distance2D(MyHero) <= tossRange &&
                                             towers.Any(y => y.Distance2D(x) <= y.AttackRange))
                                     .OrderBy(x => towers.Min(y => y.Distance2D(x))).FirstOrDefault();
                    if (towerTarget != null)
                    {
                        if (closestUnit == tossSource)
                        {
                            _tossAbility.UseAbility(towerTarget);
                            Log.Debug($"use toss back to tower {towerTarget.Name}");
                            await Await.Delay(100, tk);
                        }
                        else
                        {
                            await MoveOrBlinkToEnemy(tk, 0.1f, 0.1f, tossSource);
                            Log.Debug($"return because of blink");
                        }
                    }
                }
            }
        }

        private void _throwBackComboKey_ValueChanged(object sender, OnValueChangeEventArgs e)
        {
            var newKey = e.GetNewValue<KeyBind>().Key;
            if (e.GetOldValue<KeyBind>().Key != newKey)
            {
                _throwBackCombo.Key = KeyInterop.KeyFromVirtualKey((int) newKey);
            }
        }

        public override async Task ExecuteComboAsync(Unit target, CancellationToken tk = new CancellationToken())
        {
            await HasNoLinkens(Target, tk);
            await UseItems(tk);

            // make him disabled
            if (await DisableEnemy(tk) == DisabledState.UsedAbilityToDisable)
            {
                Log.Debug($"disabled!");
                // return;
            }

            var manta = MyHero.GetItemById(ItemId.item_manta);
            if (manta != null && manta.CanBeCasted() && MyHero.IsSilenced())
            {
                Log.Debug($"use manta 1 because silenced");
                manta.UseAbility();
                await Await.Delay(125, tk);
                manta = null;
            }

            // test if toss/av combo is working
            if (_tossAbility.CanBeCasted(Target) && _tossAbility.CanHit(Target))
            {
                Log.Debug($"use toss");
                var grab = _tossAbility.GetAbilityData("grab_radius");
                var closestUnit =
                    ObjectManager.GetEntitiesFast<Unit>()
                                 .Where(x => x != MyHero && x.IsAlive && x.Distance2D(MyHero) <= grab && x.IsRealUnit())
                                 .OrderBy(x => x.Distance2D(MyHero))
                                 .FirstOrDefault();
                Log.Debug($"Closest unit for toss: {closestUnit?.Name}");
                if (closestUnit == Target )
                {
                    _tossAbility.UseAbility(Target);
                    Log.Debug($"use toss!!");
                    await Await.Delay(100, tk);
                }
            }
            if (_avalancheAbility.CanBeCasted(Target) && _avalancheAbility.CanHit(Target))
            {
                Log.Debug($"use avalanche");
                _avalancheAbility.UseAbility(Target.NetworkPosition);
                await Await.Delay(100, tk);
            }

            // check if we are near the enemy
            if (!await MoveOrBlinkToEnemy(tk))
            {
                Log.Debug($"return because of blink");
                return;
            }

            if (manta != null && manta.CanBeCasted())
            {
                Log.Debug($"Use manta");
                manta.UseAbility();
                await Await.Delay(250, tk);
            }

            if (ZaioMenu.ShouldUseOrbwalker && !Target.HasModifier("modifier_tiny_toss"))
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