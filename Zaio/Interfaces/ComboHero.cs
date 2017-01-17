using System;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Ensage;
using Ensage.Common;
using Ensage.Common.Combo;
using Ensage.Common.Enums;
using Ensage.Common.Extensions;
using Ensage.Common.Extensions.SharpDX;
using Ensage.Common.Objects.UtilityObjects;
using Ensage.Common.Threading;
using log4net;
using PlaySharp.Toolkit.Logging;
using SharpDX;
using SpacebarToFarm;
using Zaio.Helpers;
using Attribute = Ensage.Attribute;

namespace Zaio.Interfaces
{
    internal enum DisabledState
    {
        NotDisabled,
        AlreadyDisabled,
        UsedAbilityToDisable
    }

    internal abstract class ComboHero : ComboBase, IComboExecutor
    {
        private static readonly ILog Log = AssemblyLogs.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private readonly bool _repeatCombo;


        protected readonly ItemId[] DisableItemList =
        {
            ItemId.item_sheepstick,
            ItemId.item_abyssal_blade,
            ItemId.item_bloodthorn,
            ItemId.item_orchid,
            ItemId.item_heavens_halberd
        };

        protected readonly ItemId[] ItemList =
        {
            ItemId.item_veil_of_discord,
            ItemId.item_ethereal_blade,
            ItemId.item_urn_of_shadows,
            ItemId.item_rod_of_atos,
            ItemId.item_dagon,
            ItemId.item_dagon_2,
            ItemId.item_dagon_3,
            ItemId.item_dagon_4,
            ItemId.item_dagon_5,
            ItemId.item_shivas_guard
        };

        protected readonly ItemId[] LinkensItemList =
        {
            ItemId.item_force_staff,
            ItemId.item_hurricane_pike,
            ItemId.item_heavens_halberd,
            ItemId.item_cyclone,
            ItemId.item_rod_of_atos,
            ItemId.item_orchid,
            ItemId.item_bloodthorn,
            ItemId.item_diffusal_blade,
            ItemId.item_diffusal_blade_2,
            ItemId.item_dagon,
            ItemId.item_dagon_2,
            ItemId.item_dagon_3,
            ItemId.item_dagon_4,
            ItemId.item_dagon_5,
            ItemId.item_sheepstick,
            ItemId.item_abyssal_blade
        };

        private ParticleEffect _attackRangeEffect;
        private bool _executed;
        private float _lastAttackRange;
        protected Hero MyHero;
        protected Orbwalker Orbwalker;
        protected Unit Target;

        protected ComboHero() : base(ZaioMenu.ComboKey)
        {
            _repeatCombo = true;
        }

        protected ComboHero(bool repeatCombo) : base(ZaioMenu.ComboKey)
        {
            _repeatCombo = repeatCombo;
        }

        protected float TotalAttackRange => MyHero.GetAttackRange() + MyHero.HullRadius;

        public abstract Task ExecuteComboAsync(Unit target, CancellationToken tk = new CancellationToken());

        public virtual void OnLoad()
        {
            MyHero = ObjectManager.LocalHero;
            Orbwalker = new Orbwalker(MyHero);

            if (ZaioMenu.ShouldDisplayAttackRange)
            {
                CreateAttackEffect();
            }

            GameDispatcher.OnIngameUpdate += GameDispatcher_OnIngameUpdate;
            ZaioMenu.DisplayAttackRangeChanged += ZaioMenu_DisplayAttackRangeChanged;
            ZaioMenu.ComboKeyChanged += ZaioMenu_ComboKeyChanged;
        }

        private void ZaioMenu_ComboKeyChanged(object sender, KeyEventArgs e)
        {
            Log.Debug($"Combokey changed from {Key} to {e.Value}");
            Key = e.Value;
        }

        private void CreateAttackEffect()
        {
            _lastAttackRange = TotalAttackRange;
            Log.Debug($"Attack range {_lastAttackRange}");
            _attackRangeEffect = MyHero.AddParticleEffect(@"particles\ui_mouseactions\drag_selected_ring.vpcf");
            _attackRangeEffect.SetControlPoint(1, new Vector3(255, 0, 222));
            _attackRangeEffect.SetControlPoint(2, new Vector3(_lastAttackRange, 255, 0));
            _attackRangeEffect.SetControlPoint(3, new Vector3(5, 0, 0));
        }

        private void DestroyAttackEffect()
        {
            if (_attackRangeEffect != null)
            {
                try
                {
                    _attackRangeEffect.Dispose();
                }
                catch (ParticleEffectNotFoundException)
                {
                }
                finally
                {
                    _attackRangeEffect = null;
                }
            }
        }

        private void ZaioMenu_DisplayAttackRangeChanged(object sender, BoolEventArgs e)
        {
            Log.Debug($"display attack range chagned to {e.Value}");
            if (e.Value)
            {
                CreateAttackEffect();
            }
            else
            {
                DestroyAttackEffect();
            }
        }

        private void GameDispatcher_OnIngameUpdate(EventArgs args)
        {
            if (ZaioMenu.ShouldKillSteal && !Game.IsPaused && MyHero.IsAlive &&
                (!ZaioMenu.ShouldBlockKillStealWhileComboing || Target == null || !CanExecute()))
            {
                Await.Block("zaio_killstealer", Killsteal);
            }
            else
            {
                Await.Block("zaio.killstealerSleep", Sleep);
            }

            if (TotalAttackRange != _lastAttackRange)
            {
                if (ZaioMenu.ShouldDisplayAttackRange)
                {
                    DestroyAttackEffect();
                    CreateAttackEffect();
                }
            }
        }

        private async Task Sleep()
        {
            await Task.Delay(500);
        }

        public virtual void OnClose()
        {
            GameDispatcher.OnIngameUpdate -= GameDispatcher_OnIngameUpdate;
            ZaioMenu.DisplayAttackRangeChanged -= ZaioMenu_DisplayAttackRangeChanged;
            ZaioMenu.ComboKeyChanged -= ZaioMenu_ComboKeyChanged;
            DestroyAttackEffect();
        }

        public virtual void OnDraw()
        {
            if (_executed && Target != null && Target.IsAlive && MyHero.IsAlive)
            {
                Drawing.DrawText($"Killing {Game.Localize(Target.Name)}", Game.MouseScreenPosition + new Vector2(28, 5),
                    new Vector2(24, 200), Color.Red, FontFlags.AntiAlias | FontFlags.DropShadow);
            }
        }

        protected override async Task Execute(CancellationToken token)
        {
            if (!ZaioMenu.ShouldLockTarget || Target == null || !Target.IsAlive)
            {
                //Log.Debug($"Find new target");
                // todo: more select0rs
                Target = TargetSelector.ClosestToMouse(MyHero);
                if (Target == null)
                {
                    switch (ZaioMenu.NoTargetMode)
                    {
                        case NoTargetMode.Move:
                            MyHero.Move(Game.MousePosition);
                            break;
                        case NoTargetMode.AttackMove:
                            if (!MyHero.IsAttacking())
                            {
                                MyHero.Attack(Game.MousePosition);
                            }
                            break;
                    }
                    await Await.Delay(125, token);
                    return;
                }
            }

            try
            {
                await ExecuteComboAsync(Target, token);
                await Await.Delay(1, token);
            }
            finally
            {
                _executed = true;
            }
        }

        protected override bool CanExecute()
        {
            if (Game.IsPaused || !MyHero.IsAlive)
            {
                return false;
            }

            if (!base.CanExecute())
            {
                Target = null;
                _executed = false;
                return false;
            }

            if (_repeatCombo)
            {
                return true;
            }

            return !_executed;
        }

        protected bool IsInRange(float testDistance = 1.0f)
        {
            var distance = MyHero.Distance2D(Target) - Target.HullRadius - MyHero.HullRadius;
            if (distance <= testDistance)
            {
                return true;
            }

            var blink = MyHero.Inventory.Items.FirstOrDefault(x => x.ClassID == ClassID.CDOTA_Item_BlinkDagger);
            if (blink != null && blink.CanBeCasted())
            {
                var blinkRange = blink.AbilitySpecialData.First(x => x.Name == "blink_range").Value;
                if (distance - testDistance <= blinkRange)
                {
                    return true;
                }
            }
            return false;
        }

        protected async Task<bool> MoveOrBlinkToEnemy(CancellationToken tk = default(CancellationToken),
            float minimumRange = 0.0f, float maximumRange = 0.0f)
        {
            var distance = MyHero.Distance2D(Target) - Target.HullRadius - MyHero.HullRadius;

            var testRange = maximumRange == 0.0f ? MyHero.GetAttackRange() : maximumRange;
            if (distance <= testRange)
            {
                return true;
            }

            var blink = MyHero.Inventory.Items.FirstOrDefault(x => x.ClassID == ClassID.CDOTA_Item_BlinkDagger);
            if (blink != null && blink.CanBeCasted())
            {
                var blinkRange = blink.AbilitySpecialData.First(x => x.Name == "blink_range").Value;
                if (distance <= blinkRange)
                {
                    var pos = (Target.NetworkPosition - MyHero.NetworkPosition).Normalized();
                    pos *= minimumRange;
                    pos = Target.NetworkPosition - pos;
                    blink.UseAbility(pos);
                    await Await.Delay((int) (MyHero.GetTurnTime(pos) * 1000), tk);
                    return false;
                }
            }
            var phaseBoots = MyHero.Inventory.Items.FirstOrDefault(x => x.Name == "item_phase_boots");
            if (phaseBoots != null && phaseBoots.CanBeCasted())
            {
                phaseBoots.UseAbility();
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
            return false;
        }

        protected void Orbwalk()
        {
            switch (ZaioMenu.OrbwalkerMode)
            {
                case OrbwalkerMode.Mouse:
                    Orbwalker.OrbwalkOn(Target);
                    break;
                case OrbwalkerMode.Target:
                    var distance = MyHero.IsRanged ? MyHero.GetAttackRange() / 2 : 0;
                    var currentDistance = Target.Distance2D(MyHero);
                    if (currentDistance <= distance)
                    {
                        Orbwalker.Attack(Target, true);
                    }
                    else
                    {
                        var pos = (Target.NetworkPosition - MyHero.NetworkPosition).Normalized();
                        pos *= distance;
                        Orbwalker.OrbwalkOn(Target, Target.NetworkPosition - pos);
                    }
                    break;
                case OrbwalkerMode.Attack:
                    Orbwalker.Attack(Target, true);
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        protected async Task<DisabledState> DisableEnemy(CancellationToken tk = default(CancellationToken),
            float minimumTime = 0)
        {
            // make him disabled
            float duration = 0;
            if ((Target.IsHexed(out duration) || Target.IsStunned(out duration) || Target.IsSilenced() ||
                 Target.IsDisarmed()) && duration >= minimumTime)
            {
                return DisabledState.AlreadyDisabled;
            }

            foreach (var itemName in DisableItemList)
            {
                var item = MyHero.GetItemById(itemName);
                if (item != null && item.CanBeCasted(Target) && item.CanHit(Target))
                {
                    Log.Debug($"using disable item {item.Name}");
                    item.UseAbility(Target);
                    await Await.Delay(100, tk);
                    return DisabledState.UsedAbilityToDisable;
                }
            }
            return DisabledState.NotDisabled;
        }

        protected float GetSpellAmp()
        {
            // spell amp
            var spellAmp = (100.0f + MyHero.TotalIntelligence / 16.0f) / 100.0f;

            var aether = MyHero.GetItemById(ItemId.item_aether_lens);
            if (aether != null)
            {
                spellAmp += aether.AbilitySpecialData.First(x => x.Name == "spell_amp").Value / 100.0f;
            }

            var talent =
                MyHero.Spellbook.Spells.FirstOrDefault(
                    x => x.Level > 0 && x.Name.StartsWith("special_bonus_spell_amplify_"));
            if (talent != null)
            {
                spellAmp += talent.AbilitySpecialData.First(x => x.Name == "value").Value / 100.0f;
            }

            return spellAmp;
        }

        protected virtual async Task UseItems(CancellationToken tk = default(CancellationToken))
        {
            foreach (var itemId in ItemList)
            {
                var item = MyHero.GetItemById(itemId);
                if (item != null && item.CanBeCasted(Target) && item.CanHit(Target))
                {
                    Log.Debug($"using item {item.Name}");
                    if (item.AbilityBehavior.HasFlag(AbilityBehavior.UnitTarget))
                    {
                        item.UseAbility(Target);

                        // wait for eth hit to get bonus damage with following spells
                        if (item.ID == (uint) ItemId.item_ethereal_blade)
                        {
                            var speed = item.GetAbilityData("projectile_speed");
                            if (speed != 0.0f)
                            {
                                var time = Target.Distance2D(MyHero) / speed;
                                await Await.Delay((int) (time * 1000.0f + Game.Ping) + 100, tk);
                            }
                        }
                    }
                    else if (item.AbilityBehavior.HasFlag(AbilityBehavior.Point))
                    {
                        item.UseAbility(Target.NetworkPosition);
                        await Await.Delay(100, tk);
                    }
                    else if (item.AbilityBehavior.HasFlag(AbilityBehavior.NoTarget))
                    {
                        item.UseAbility();
                        await Await.Delay(100, tk);
                    }
                }
            }
        }

        protected virtual async Task<bool> Killsteal()
        {
            if (MyHero.UnitState.HasFlag(UnitState.Muted) || MyHero.IsStunned() || MyHero.IsHexed())
            {
                return true;
            }
            // spell amp
            var spellAmp = GetSpellAmp();

            // killsteal items
            var eth = MyHero.GetItemById(ItemId.item_ethereal_blade);
            if (eth != null && eth.CanBeCasted())
            {
                var damage = eth.GetAbilityData("blast_damage_base");
                if (MyHero.PrimaryAttribute == Attribute.Agility)
                {
                    damage += MyHero.TotalAgility * 2;
                }
                else if (MyHero.PrimaryAttribute == Attribute.Intelligence)
                {
                    damage += MyHero.TotalIntelligence * 2;
                }
                else
                {
                    damage += MyHero.TotalStrength * 2;
                }

                damage *= spellAmp + eth.GetAbilityData("ethereal_damage_bonus") / -100.0f;
                var enemy =
                    ObjectManager.GetEntitiesParallel<Hero>()
                                 .FirstOrDefault(
                                     x =>
                                         x.IsAlive && x.Team != MyHero.Team && eth.CanBeCasted(x) && eth.CanHit(x) &&
                                         !x.IsMagicImmune() &&
                                         x.Health < damage * (1 - x.MagicResistance()) && !x.CantBeAttacked() &&
                                         !x.CantBeKilled());
                if (enemy != null)
                {
                    eth.UseAbility(enemy);
                    var speed = eth.GetAbilityData("projectile_speed");
                    var time = enemy.Distance2D(MyHero) / speed;
                    Log.Debug($"killsteal for eth {time} with damage {damage} ({damage * (1 - enemy.MagicResistance())}");
                    eth.UseAbility(enemy);
                    await Await.Delay((int) (time * 1000.0f + Game.Ping) + 100);
                    return true;
                }
            }

            var dagon = MyHero.Inventory.Items.FirstOrDefault(x => x.Name.StartsWith("item_dagon"));
            if (dagon != null && dagon.CanBeCasted())
            {
                var index = dagon.Name.Length == 10 ? 0 : uint.Parse(dagon.Name.Substring(11)) - 1;

                var damage = dagon.AbilitySpecialData.First(x => x.Name == "damage").GetValue(index);
                damage *= spellAmp;
                var enemy =
                    ObjectManager.GetEntitiesParallel<Hero>()
                                 .FirstOrDefault(
                                     x =>
                                         x.IsAlive && x.Team != MyHero.Team && dagon.CanBeCasted(x) && dagon.CanHit(x) &&
                                         !x.IsMagicImmune() &&
                                         x.Health < damage * (1 - x.MagicResistance()) && !x.CantBeAttacked() &&
                                         !x.CantBeKilled());
                if (enemy != null)
                {
                    Log.Debug(
                        $"killsteal dagon {index} damage: {damage} ({damage * (1 - enemy.MagicResistance())}) - {dagon.CastRange}");
                    dagon.UseAbility(enemy);
                    await Await.Delay(125);
                    return true;
                }
            }
            return false;
        }

        protected virtual bool HasNoLinkens(Unit target)
        {
            if (!target.IsLinkensProtected())
            {
                return true;
            }

            foreach (var itemId in LinkensItemList)
            {
                var item = MyHero.GetItemById(itemId);
                if (item != null && item.CanBeCasted(target) && item.CanHit(target))
                {
                    item.UseAbility(target);
                    return true;
                }
            }

            return false;
        }
    }
}