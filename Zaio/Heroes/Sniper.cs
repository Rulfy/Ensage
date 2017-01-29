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
using SharpDX;
using Zaio.Helpers;
using Zaio.Interfaces;

namespace Zaio.Heroes
{
    [Hero(ClassID.CDOTA_Unit_Hero_Sniper)]
    internal class Sniper : ComboHero
    {
        private static readonly ILog Log = AssemblyLogs.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private static readonly string[] SupportedAbilities =
        {
            "sniper_shrapnel",
            "sniper_assassinate"
        };

        private static readonly string[] KillstealAbilities =
        {
            "sniper_assassinate"
        };

        private Ability _shrapnelAbility;

        private List<ShrapnelInfo> _shrapnelInfo = new List<ShrapnelInfo>();
        private Ability _ultAbility;

        public override void OnLoad()
        {
            base.OnLoad();

            var heroMenu = new Menu("Sniper", "zaioSniper", false, "npc_dota_hero_sniper", true);

            heroMenu.AddItem(new MenuItem("zaioSniperAbilitiesText", "Supported Abilities"));
            var supportedStuff = new MenuItem("zaioSniperAbilities", string.Empty);
            supportedStuff.SetValue(new AbilityToggler(SupportedAbilities.ToDictionary(x => x, y => true)));
            heroMenu.AddItem(supportedStuff);

            heroMenu.AddItem(new MenuItem("zaioSniperKillstealAbilitiesText", "Supported Killsteal Abilities"));
            var supportedKillsteal = new MenuItem("zaioSniperKillstealAbilities", string.Empty);
            supportedKillsteal.SetValue(new AbilityToggler(KillstealAbilities.ToDictionary(x => x, y => true)));
            heroMenu.AddItem(supportedKillsteal);

            ZaioMenu.LoadHeroSettings(heroMenu);

            _shrapnelAbility = MyHero.GetAbilityById(AbilityId.sniper_shrapnel);
            _ultAbility = MyHero.GetAbilityById(AbilityId.sniper_assassinate);
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

            if (_ultAbility.CanBeCasted() && !IsEnemyNear())
            {
                var hasAgha = MyHero.HasItem(ClassID.CDOTA_Item_UltimateScepter);
                if (hasAgha)
                {
                    var critBonus = _ultAbility.GetAbilityData("scepter_crit_bonus"); // 280
                    var damage = critBonus / 100.0f * (MyHero.MinimumDamage + MyHero.BonusDamage);

                    var enemy =
                        ObjectManager.GetEntitiesParallel<Hero>()
                                     .FirstOrDefault(
                                         x =>
                                             x.IsAlive && x.Team != MyHero.Team && !x.IsIllusion &&
                                             _ultAbility.CanBeCasted(x) &&
                                             _ultAbility.CanHit(x) &&
                                             x.Health < damage * (1 - x.PhysicalResistance()) && !x.CantBeAttacked() &&
                                             !x.CantBeKilled());
                    if (enemy != null)
                    {
                        Log.Debug(
                            $"use killsteal agha ult because enough damage {enemy.Health} <= {damage * (1 - enemy.PhysicalResistance())} ");
                        _ultAbility.UseAbility(enemy.NetworkPosition);
                        await Await.Delay(GetAbilityDelay(enemy.NetworkPosition, _ultAbility));
                        return true;
                    }
                }
                else
                {
                    var damage = (float) _ultAbility.GetDamage(_ultAbility.Level - 1);
                    damage *= GetSpellAmp();

                    var enemy =
                        ObjectManager.GetEntitiesParallel<Hero>()
                                     .FirstOrDefault(
                                         x =>
                                             x.IsAlive && x.Team != MyHero.Team && !x.IsIllusion &&
                                             _ultAbility.CanBeCasted(x) &&
                                             _ultAbility.CanHit(x) && !x.IsMagicImmune() && !x.IsLinkensProtected() &&
                                             x.Health < damage * (1 - x.MagicResistance()) && !x.CantBeAttacked() &&
                                             !x.CantBeKilled());
                    if (enemy != null)
                    {
                        Log.Debug(
                            $"use killsteal ult because enough damage {enemy.Health} <= {damage * (1 - enemy.MagicResistance())} ");
                        _ultAbility.UseAbility(enemy);
                        await Await.Delay(GetAbilityDelay(enemy, _ultAbility));
                        return true;
                    }
                }
            }

            return false;
        }

        private bool IsEnemyNear(Unit exceptionUnit = null)
        {
            return
                ObjectManager.GetEntitiesParallel<Hero>()
                             .Any(
                                 x =>
                                     x.IsAlive && x.Team != MyHero.Team && !x.IsIllusion &&
                                     (exceptionUnit == null || x != exceptionUnit) &&
                                     x.Distance2D(MyHero) <= MyHero.GetAttackRange());
        }

        private bool IsUnderShrapnel(Vector3 pos)
        {
            var radius = _shrapnelAbility.GetAbilityData("radius");
            var duration = _shrapnelAbility.GetAbilityData("duration");
            _shrapnelInfo = _shrapnelInfo.Where(x => Game.RawGameTime - x.Time <= duration).ToList();
            return _shrapnelInfo.Any(x => x.Position.Distance2D(pos) <= radius);
        }

        public override async Task ExecuteComboAsync(Unit target, CancellationToken tk = new CancellationToken())
        {
            if (!MyHero.IsSilenced())
            {
                if (_ultAbility.CanBeCasted(target) && _ultAbility.CanHit(target) && !IsEnemyNear() &&
                    !MyHero.IsInvisible())
                {
                    var hasAgha = MyHero.HasItem(ClassID.CDOTA_Item_UltimateScepter);
                    if (hasAgha)
                    {
                        var critBonus = _ultAbility.GetAbilityData("scepter_crit_bonus"); // 280
                        var damage = critBonus / 100.0f * (MyHero.MinimumDamage + MyHero.BonusDamage);

                        if (target.Health < damage * (1 - target.PhysicalResistance()) || target.IsStunned() ||
                            target.IsHexed())
                        {
                            Log.Debug(
                                $"use agha ult, damage {target.Health} <= {damage * (1 - target.PhysicalResistance())} ");
                            _ultAbility.UseAbility(target.NetworkPosition);
                            await Await.Delay(GetAbilityDelay(target, _ultAbility), tk);
                            return;
                        }
                    }
                    else if (!target.IsLinkensProtected() || target.MagicResistance() == 1.0f)
                    {
                        var damage = (float) _ultAbility.GetDamage(_ultAbility.Level - 1);
                        damage *= GetSpellAmp();

                        if (target.Health < damage * (1 - target.MagicResistance()) || target.IsStunned() ||
                            target.IsHexed())
                        {
                            Log.Debug(
                                $"use ult, damage {target.Health} <= {damage * (1 - target.MagicResistance())} ");
                            _ultAbility.UseAbility(target);
                            await Await.Delay(GetAbilityDelay(target, _ultAbility), tk);
                            return;
                        }
                    }
                }

                if (_shrapnelAbility.CanBeCasted(target) && _shrapnelAbility.CanHit(target))
                {
                    var castPoint = _shrapnelAbility.FindCastPoint() * 1000.0f;
                    var delay = _shrapnelAbility.GetAbilityData("damage_delay") * 1000.0f;
                    var pos = Prediction.Prediction.PredictPosition(target, (int) (castPoint + delay + Game.Ping));
                    if (!IsUnderShrapnel(pos))
                    {
                        _shrapnelInfo.Add(new ShrapnelInfo(pos, Game.RawGameTime));
                        _shrapnelAbility.UseAbility(pos);
                        Log.Debug($"using shrapnel!");
                        await Await.Delay(GetAbilityDelay(pos, _shrapnelAbility), tk);
                    }
                }
            }

            // make him disabled
            if (await DisableEnemy(tk) == DisabledState.UsedAbilityToDisable)
            {
                Log.Debug($"disabled!");
                // return;
            }

            await UseItems(tk);

            // check if we are near the enemy
            if (!await MoveOrBlinkToEnemy(tk))
            {
                Log.Debug($"return because of blink");
                return;
            }

            if (ZaioMenu.ShouldUseOrbwalker)
            {
                Orbwalk();
                Log.Debug($"orbwalking");
            }
            else
            {
                MyHero.Attack(target);
                await Await.Delay(125, tk);
            }
        }

        private struct ShrapnelInfo
        {
            public Vector3 Position;
            public readonly float Time;

            public ShrapnelInfo(Vector3 position, float time)
            {
                Position = position;
                Time = time;
            }
        }
    }
}