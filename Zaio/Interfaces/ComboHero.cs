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
using Ensage.Common.Menu;
using Ensage.Common.Objects.UtilityObjects;
using Ensage.Common.Threading;
using log4net;
using PlaySharp.Toolkit.Logging;
using SharpDX;
using SpacebarToFarm;
using Zaio.Helpers;
using MyAsyncHelpers = Zaio.Helpers.MyAsyncHelpers;
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

        public EventHandler<EntityEventArgs> NewTargetAcquired;

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
        private ParticleEffect _comboTargetEffect;
        private bool _executed;
        private float _lastAttackRange;
        private Unit _target;
        protected Hero MyHero;
        protected Orbwalker Orbwalker;

        protected ComboHero() : base(ZaioMenu.ComboKey)
        {
            _repeatCombo = true;
        }

        protected ComboHero(bool repeatCombo) : base(ZaioMenu.ComboKey)
        {
            _repeatCombo = repeatCombo;
        }


        public Hero Hero => MyHero;
        public Unit ComboTarget => Target;

        protected static int ItemDelay => 50;

        protected Unit Target
        {
            get { return _target; }
            set
            {
                _target = value;
                if (_comboTargetEffect != null)
                {
                    _comboTargetEffect.Dispose();
                    _comboTargetEffect = null;
                }
                if (_target != null)
                {
                    //target inditcator
                    _comboTargetEffect =
                        _target.AddParticleEffect(@"particles\ui_mouseactions\range_finder_tower_aoe.vpcf");
                    _comboTargetEffect.SetControlPointEntity(2, MyHero);
                    _comboTargetEffect.SetControlPoint(2, MyHero.NetworkPosition); //start point XYZ
                    _comboTargetEffect.SetControlPoint(6, new Vector3(1, 0, 0)); // 1 means the particle is visible
                    _comboTargetEffect.SetControlPoint(7, _target.NetworkPosition); //end point XYZ  
                }
            }
        }

        protected float TotalAttackRange => MyHero.GetAttackRange() + MyHero.HullRadius;

        public abstract Task ExecuteComboAsync(Unit target, CancellationToken tk = new CancellationToken());

        protected int GetAbilityDelay(Unit target, Ability ability)
        {
            return (int) ((ability.FindCastPoint() + MyHero.GetTurnTime(target)) * 1000.0 + Game.Ping);
        }

        protected int GetAbilityDelay(Ability ability)
        {
            return (int)(ability.FindCastPoint() * 1000.0 + Game.Ping);
        }

        protected int GetAbilityDelay(Vector3 targetPosition, Ability ability)
        {
            return (int) ((ability.FindCastPoint() + MyHero.GetTurnTime(targetPosition)) * 1000.0 + Game.Ping);
        }

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

        protected virtual void OnLoadMenuItems(MenuItem supportedStuff = null, MenuItem killstealStuff = null)
        {
            if (supportedStuff != null)
            {
                MyAbilityExtension.AbilityStatus = supportedStuff.GetValue<AbilityToggler>().Dictionary;
                supportedStuff.ValueChanged += SupportedStuff_ValueChanged;
            }
            if (killstealStuff != null)
            {
                MyAbilityExtension.AbilityKillStealStatus = killstealStuff.GetValue<AbilityToggler>().Dictionary;
                killstealStuff.ValueChanged += KillstealSupportedStuff_ValueChanged;
            }
        }

        private void SupportedStuff_ValueChanged(object sender, OnValueChangeEventArgs e)
        {
            MyAbilityExtension.AbilityStatus = e.GetNewValue<AbilityToggler>().Dictionary;
        }

        private void KillstealSupportedStuff_ValueChanged(object sender, OnValueChangeEventArgs e)
        {
            MyAbilityExtension.AbilityKillStealStatus = e.GetNewValue<AbilityToggler>().Dictionary;
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
                Await.Block("zaio.killstealerSleep", MyAsyncHelpers.AsyncSleep);
            }

            if (TotalAttackRange != _lastAttackRange)
            {
                if (ZaioMenu.ShouldDisplayAttackRange)
                {
                    DestroyAttackEffect();
                    CreateAttackEffect();
                }
            }

            if (_comboTargetEffect != null && _target != null)
            {
                _comboTargetEffect.SetControlPoint(2, MyHero.NetworkPosition); //start point XYZ
                _comboTargetEffect.SetControlPoint(7, _target.NetworkPosition); //end point XYZ  
            }

            var prioritizeEvade = ZaioMenu.ShouldRespectEvader;

            if (prioritizeEvade && !Utils.SleepCheck("Evader.Avoiding"))
            {
                Log.Debug($"abort because evade1");
                Cancel();
            }
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
            var prioritizeEvade = ZaioMenu.ShouldRespectEvader;

            if (!ZaioMenu.ShouldLockTarget || Target == null || !Target.IsAlive)
            {
                //Log.Debug($"Find new target");
                // todo: more select0rs
                var oldTarget = Target;
                switch (ZaioMenu.TargetSelectorMode)
                {
                    case TargetSelectorMode.NearestToMouse:
                        Target = TargetSelector.ClosestToMouse(MyHero);
                        break;
                    case TargetSelectorMode.BestAutoAttackTarget:
                        Target = TargetSelector.BestAutoAttackTarget(MyHero);
                        break;
                    case TargetSelectorMode.HighestHealth:
                        Target = TargetSelector.HighestHealthPointsTarget(MyHero, 1000);
                        break;
                }
               

                if (prioritizeEvade && !Utils.SleepCheck("Evader.Avoiding"))
                {
                    Log.Debug($"abort because evade2");
                    return;
                }

                if (oldTarget != Target)
                {
                    NewTargetAcquired?.Invoke(this, new EntityEventArgs(Target));
                }

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

            if (prioritizeEvade && !Utils.SleepCheck("Evader.Avoiding"))
            {
                Log.Debug($"abort because evade3");
                return;
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

            if (ZaioMenu.ShouldUseBlinkDagger && !MyHero.IsMuted())
            {
                var blink = MyHero.Inventory.Items.FirstOrDefault(x => x.ClassID == ClassID.CDOTA_Item_BlinkDagger);
                if (blink != null && blink.CanBeCasted())
                {
                    var blinkRange = blink.AbilitySpecialData.First(x => x.Name == "blink_range").Value;
                    if (distance - testDistance <= blinkRange)
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        protected async Task<bool> MoveOrBlinkToEnemy(Unit target, CancellationToken tk = default(CancellationToken), float minimumRange = 0.0f, float maximumRange = 0.0f, bool usePrediction = false)
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
                            if (target.IsMoving && usePrediction)
                            {
                               var moves = Ensage.Common.Prediction.InFront(target, 75);
                               blink.UseAbility(moves);
                               await Await.Delay((int) (MyHero.GetTurnTime(moves) * 1000) + ItemDelay, tk);
                               return false;
                            }
                            else
                            {
                            blink.UseAbility(pos);
                            await Await.Delay((int) (MyHero.GetTurnTime(pos) * 1000) + ItemDelay, tk);
                            return false;
                            }
                            
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

        protected async Task<bool> MoveToEnemy(Unit target, CancellationToken tk = default(CancellationToken), float minimumRange = 0.0f, float maximumRange = 0.0f)
        {
            var distance = MyHero.Distance2D(target) - target.HullRadius - MyHero.HullRadius;

            var testRange = maximumRange == 0.0f ? MyHero.GetAttackRange() : maximumRange;
            if (distance <= testRange)
            {
                return true;
            }

            if (!MyHero.IsMuted() && !MyHero.IsInvisible())
            {
                var phaseBoots = MyHero.Inventory.Items.FirstOrDefault(x => x.Name == "item_phase_boots");
                if (phaseBoots != null && phaseBoots.CanBeCasted())
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

        protected async Task<DisabledState> DisableEnemy(Unit target, CancellationToken tk = default(CancellationToken), float minimumTime = 0)
        {
            // make him disabled
            float duration = 0;
            if ((target.IsHexed(out duration) || target.IsStunned(out duration) || target.IsSilenced() ||
                 Target.IsDisarmed()) && duration >= minimumTime)
            {
                return DisabledState.AlreadyDisabled;
            }

            if (!MyHero.IsMuted())
            {
                foreach (var itemName in DisableItemList)
                {
                    var item = MyHero.GetItemById(itemName);
                    if (item != null && item.CanBeCasted(target) && item.CanHit(target))
                    {
                        Log.Debug($"using disable item {item.Name}");
                        item.UseAbility(target);
                        await Await.Delay(ItemDelay, tk);
                        return DisabledState.UsedAbilityToDisable;
                    }
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

        protected virtual async Task UseItems(Unit target, CancellationToken tk = default(CancellationToken))
        {
            if (MyHero.IsMuted())
            {
                return;
            }

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
                            var time = Target.Distance2D(MyHero) / speed;
                            await Await.Delay((int) (time * 1000.0f + Game.Ping) + ItemDelay, tk);
                        }
                    }
                    else if (item.AbilityBehavior.HasFlag(AbilityBehavior.Point))
                    {
                        item.UseAbility(Target.NetworkPosition);
                        await Await.Delay(ItemDelay, tk);
                    }
                    else if (item.AbilityBehavior.HasFlag(AbilityBehavior.NoTarget))
                    {
                        item.UseAbility();
                        await Await.Delay(ItemDelay, tk);
                    }
                }
            }
        }

        protected virtual async Task<bool> Killsteal()
        {
            if (MyHero.IsMuted() || MyHero.IsStunned() || MyHero.IsHexed())
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
                                         x.IsAlive && x.Team != MyHero.Team && !x.IsIllusion && eth.CanBeCasted(x) &&
                                         eth.CanHit(x) &&
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
                    await Await.Delay((int) (time * 1000.0f + Game.Ping) + ItemDelay);
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
                                         x.IsAlive && x.Team != MyHero.Team && !x.IsIllusion && dagon.CanBeCasted(x) &&
                                         dagon.CanHit(x) &&
                                         !x.IsMagicImmune() &&
                                         x.Health < damage * (1 - x.MagicResistance()) && !x.CantBeAttacked() &&
                                         !x.CantBeKilled());
                if (enemy != null)
                {
                    Log.Debug(
                        $"killsteal dagon {index} damage: {damage} ({damage * (1 - enemy.MagicResistance())}) - {dagon.CastRange}");
                    dagon.UseAbility(enemy);
                    await Await.Delay(ItemDelay);
                    return true;
                }
            }
            return false;
        }

        protected virtual async Task<bool> HasNoLinkens(Unit target, CancellationToken tk = default(CancellationToken))
        {
            if (!target.IsLinkensProtected())
            {
                return true;
            }
            if (!MyHero.IsMuted())
            {
                foreach (var itemId in LinkensItemList)
                {
                    var item = MyHero.GetItemById(itemId);
                    if (item != null && item.CanBeCasted(target) && item.CanHit(target))
                    {
                        item.UseAbility(target);
                        await Await.Delay(ItemDelay, tk);
                        return true;
                    }
                }
            }

            return false;
        }
    }
}
