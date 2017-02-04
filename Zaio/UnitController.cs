using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Ensage;
using Ensage.Common.Extensions;
using Ensage.Common.Threading;
using log4net;
using PlaySharp.Toolkit.Logging;
using SharpDX;
using Zaio.Helpers;

namespace Zaio.Interfaces
{
    internal class UnitController
    {
        private static readonly ILog Log = AssemblyLogs.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private readonly Ability _aura;
        private readonly float _auraRadius;
        private readonly ComboHero _myHero;
        private float _lastAttackingTime;

        public UnitController(ComboHero myHero, Unit unit)
        {
            ControlledUnit = unit;
            _myHero = myHero;


            var spells =
                ControlledUnit.Spellbook.Spells.Where(
                    x =>
                        x.AbilityBehavior.HasFlag(AbilityBehavior.Aura) ||
                        x.AbilityBehavior.HasFlag(AbilityBehavior.Passive) &&
                        x.AbilitySpecialData.Count() <= 3 && x.DamageType == DamageType.None);
            foreach (var spell in spells)
            {
                var abilitySpecialData = spell.AbilitySpecialData.FirstOrDefault(x => x.Name.Contains("radius"));
                if (abilitySpecialData != null)
                {
                    _auraRadius = abilitySpecialData.Value; // presence_radius, vampiric_aura_radius, radius
                    _aura = spell;
                    break;
                }
            }
            Log.Debug($"HasAura: {_aura != null} | {_aura?.Name} | {_auraRadius}");
        }

        public Unit ControlledUnit { get; }

        private bool HasActiveSkill
            =>
                ControlledUnit.Spellbook.Spells.Any(
                    x => x.IsActiveAbility() && x.CanBeCasted() && x.ManaCost <= ControlledUnit.Mana);

        public async Task Tick()
        {
            if (!ControlledUnit.IsValid || !ControlledUnit.IsAlive || !ControlledUnit.IsControllable)
            {
                return;
            }
            switch (ZaioMenu.ActiveControlMode)
            {
                case ActiveControlMode.Auto:
                    await Auto();
                    break;
                case ActiveControlMode.Follow:
                    await Follow();
                    break;
                case ActiveControlMode.AttackComboTarget:
                    await AttackComboTarget();
                    break;
            }
        }

        protected async Task Follow()
        {
            var hero = _myHero.Hero;
            var distance = hero.Distance2D(ControlledUnit);

            var enemies =
                ObjectManager.GetEntitiesParallel<Hero>()
                             .Where(x => x.IsValid && x.IsAlive && x.Team != hero.Team && x.Distance2D(hero) < 1000);
            if (!enemies.Any())
            {
                ControlledUnit.Follow(hero);
                Log.Debug($"follow");
            }
            else
            {
                var pos = Vector3.Zero;
                foreach (var enemy in enemies)
                {
                    pos += enemy.NetworkPosition;
                }
                pos /= enemies.Count();
                var dir = pos - hero.NetworkPosition;
                dir.Normalize();
                if (_aura != null)
                {
                    dir *= _auraRadius / 2;
                }
                else
                {
                    dir *= 500;
                }
                pos = hero.NetworkPosition - dir;
                if (ControlledUnit.Distance2D(pos) < 75)
                {
                    ControlledUnit.Hold();
                }
                else
                {
                    ControlledUnit.Move(pos);
                }
                Log.Debug($"follow away from enemy");
            }
            await Await.Delay(100);
        }

        protected async Task Auto()
        {
            if (ControlledUnit.AttackCapability == AttackCapability.None)
            {
                Log.Debug($"no attack");
                await Follow();
                return;
            }
            var hero = _myHero.Hero;
            var healthPerc = (float) ControlledUnit.Health / ControlledUnit.MaximumHealth;
            var time = Game.RawGameTime;

            if (_aura != null && (healthPerc <= 0.25f || !HasActiveSkill))
            {
                await Follow();
                return;
            }

            if ((hero.IsAttacking() || time - _lastAttackingTime <= hero.AttackBackswing() * 2) &&
                _myHero.ComboTarget == null)
            {
                _lastAttackingTime = time;
                var target =
                    ObjectManager.GetEntitiesParallel<Unit>()
                                 .FirstOrDefault(
                                     x =>
                                         x.IsValid && x.IsAlive && x.Team != hero.Team && hero.IsAttacking(x));
                if (target != null)
                {
                    Log.Debug($"attack {target.Name}");
                    ControlledUnit.Attack(target);
                    await Await.Delay(250);
                }
                else
                {
                    Log.Debug($"is attacking but no target");
                    await Follow();
                }
            }
            else
            {
                Log.Debug($"combo target");
                await AttackComboTarget();
            }
        }

        protected async Task AttackComboTarget()
        {
            var target = _myHero.ComboTarget;
            if (target == null)
            {
                Log.Debug($"no combo target");
                await Follow();
                return;
            }
            var hero = _myHero.Hero;
            foreach (var spell in ControlledUnit.Spellbook.Spells)
            {
                if (spell.IsHidden || !spell.IsActivated || !spell.CanBeCasted() || ControlledUnit.Mana < spell.ManaCost)
                {
                    continue;
                }
                var usedSpell = false;
                if (spell.AbilityBehavior.HasFlag(AbilityBehavior.UnitTarget))
                {
                    if (spell.TargetTeamType.HasFlag(TargetTeamType.All) &&
                        spell.CanBeCasted(target) && spell.CanHit(target))
                    {
                        Log.Debug($"use spell {spell.Name} with type {spell.TargetTeamType}");
                        spell.UseAbility(target);
                        usedSpell = true;
                    }
                    else if (spell.TargetTeamType.HasFlag(TargetTeamType.Allied) && spell.CanHit(hero))
                    {
                        Log.Debug($"use spell {spell.Name} with type {spell.TargetTeamType} on {hero.Name}");
                        spell.UseAbility(hero);
                    }
                    else if (spell.CanBeCasted(target) && spell.CanHit(target))
                    {
                        Log.Debug(
                            $"use spell {spell.Name} with type {spell.TargetTeamType} | {spell.CanBeCasted(hero)} | {spell.CanHit(hero)}");
                        spell.UseAbility(target);
                        usedSpell = true;
                    }
                }
                else if (spell.AbilityBehavior.HasFlag(AbilityBehavior.NoTarget) &&
                         spell.CanBeCasted(target) && spell.CanHit(target))
                {
                    spell.UseAbility();
                    usedSpell = true;
                }
                else if ((spell.AbilityBehavior.HasFlag(AbilityBehavior.AreaOfEffect) ||
                          spell.AbilityBehavior.HasFlag(AbilityBehavior.Point)) &&
                         spell.CanBeCasted(target) && spell.CanHit(target))
                {
                    spell.UseAbility(target.NetworkPosition);
                    usedSpell = true;
                }
                if (usedSpell)
                {
                    Log.Debug($"was using spell {spell.Name}");
                    await Await.Delay((int) (spell.FindCastPoint() * 1000 + Game.Ping + 125));
                }
            }
            Log.Debug($"attack");
            ControlledUnit.Attack(target);
            await Await.Delay(250);
        }

        public override int GetHashCode()
        {
            return (int) ControlledUnit.Handle;
        }
    }
}