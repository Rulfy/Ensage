using System;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Ensage;
using Ensage.Common;
using Ensage.Common.Combo;
using Ensage.Common.Extensions;
using Ensage.Common.Extensions.SharpDX;
using Ensage.Common.Objects.UtilityObjects;
using Ensage.Common.Threading;
using log4net;
using PlaySharp.Toolkit.Logging;
using SharpDX;
using Attribute = Ensage.Attribute;

namespace Zaio.Interfaces
{
    internal abstract class ComboHero : ComboBase, IComboExecutor
    {
        private static readonly ILog Log = AssemblyLogs.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private readonly bool _repeatCombo;
        private ParticleEffect _attackRangeEffect;
        private bool _executed;
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

        public abstract Task ExecuteComboAsync(Unit target, CancellationToken tk = new CancellationToken());

        public virtual void OnLoad()
        {
            MyHero = ObjectManager.LocalHero;
            Orbwalker = new Orbwalker(MyHero);

            if (ZaioMenu.ShouldDisplayAttackRange)
            {
                _attackRangeEffect = MyHero.AddParticleEffect(@"particles\ui_mouseactions\drag_selected_ring.vpcf");
                _attackRangeEffect.SetControlPoint(1, new Vector3(255, 0, 222));
                _attackRangeEffect.SetControlPoint(2, new Vector3(MyHero.GetAttackRange() + MyHero.HullRadius, 255, 0));
                _attackRangeEffect.SetControlPoint(3, new Vector3(5, 0, 0));
            }

            GameDispatcher.OnIngameUpdate += GameDispatcher_OnIngameUpdate;
        }

        private void GameDispatcher_OnIngameUpdate(EventArgs args)
        {
            if (ZaioMenu.ShouldKillSteal)
            {
                Await.Block("zaio_killstealer", Killsteal);
            }
            else
            {
                Await.Block("zaio.killstealerSleep", Sleep);
            }
        }

        private async Task Sleep()
        {
            await Task.Delay(500);
        }

        public virtual void OnClose()
        {
            GameDispatcher.OnIngameUpdate -= GameDispatcher_OnIngameUpdate;

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
                Log.Debug($"Find new target");
                // todo: more select0rs
                Target = TargetSelector.ClosestToMouse(MyHero);
                if (Target == null)
                {
                    return;
                }
            }
            await ExecuteComboAsync(Target, token);
            await Await.Delay(250, token);
            _executed = true;
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

            var testRange = Math.Max(MyHero.GetAttackRange() * 1.1f, maximumRange);
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
                    blink.UseAbility(Target.NetworkPosition - pos);
                    await Await.Delay(125, tk);
                    return true;
                }
            }
            var phaseBoots = MyHero.Inventory.Items.FirstOrDefault(x => x.Name == "item_phase_boots");
            if (phaseBoots != null && phaseBoots.CanBeCasted())
            {
                phaseBoots.UseAbility();
                await Await.Delay(1, tk);
            }
            if (ZaioMenu.ShouldUseOrbwalker)
            {
                Orbwalker.Attack(Target, false);
            }
            else
            {
                MyHero.Attack(Target);
            }
            await Await.Delay(125, tk);
            return false;
        }

        protected async Task<bool> DisableEnemy(CancellationToken tk = default(CancellationToken), float minimumTime = 0)
        {
            // make him disabled
            float duration = 0;
            if ((Target.IsHexed(out duration) || Target.IsStunned(out duration) || Target.IsSilenced() ||
                 Target.IsDisarmed()) && duration >= minimumTime)
            {
                return true;
            }
            var itemList = new[]
            {
                "item_sheepstick",
                "item_abyssal_blade",
                "item_bloodthorn",
                "item_orchid",
                "item_heavens_halberd"
            };
            foreach (var itemName in itemList)
            {
                var item = MyHero.FindItem(itemName);
                if (item != null && item.CanBeCasted(Target))
                {
                    Log.Debug($"using disable item {item.Name}");
                    item.UseAbility(Target);
                    await Await.Delay(1, tk);
                    return true;
                }
            }
            return false;
        }

        protected virtual async Task Killsteal()
        {
            // spell amp
            var spellAmp = (100.0f + MyHero.TotalIntelligence / 16.0f) / 100.0f;

            var aether = MyHero.Inventory.Items.FirstOrDefault(x => x.ClassID == ClassID.CDOTA_Item_Aether_Lens);
            if (aether != null)
            {
                spellAmp += aether.AbilitySpecialData.First(x => x.Name == "spell_amp").Value / 100.0f;
            }

            var talent = MyHero.Spellbook.Spells.FirstOrDefault(x => x.Name.StartsWith("special_bonus_spell_amplify_"));
            if (talent != null && talent.Level > 0)
            {
                spellAmp += talent.AbilitySpecialData.First(x => x.Name == "value").Value / 100.0f;
            }


            // killsteal items
            var eth = MyHero.FindItem("item_ethereal_blade");
            if (eth != null && eth.CanBeCasted())
            {
                var damage = eth.AbilitySpecialData.First(x => x.Name == "blast_damage_base").Value;
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

                damage *= spellAmp + 0.4f;
                var enemy =
                    ObjectManager.GetEntitiesParallel<Hero>()
                                 .FirstOrDefault(
                                     x =>
                                         x.IsAlive && x.Team != MyHero.Team && !x.IsMagicImmune() &&
                                         x.Distance2D(MyHero) < eth.CastRange &&
                                         x.Health < damage * (1 - x.MagicDamageResist));
                if (enemy != null)
                {
                    eth.UseAbility(enemy);
                    var speed = eth.AbilitySpecialData.First(x => x.Name == "projectile_speed").Value;
                    var time = enemy.Distance2D(MyHero) / speed;
                    Log.Debug($"killsteal for eth {time} with damage {damage} ({damage * (1 - enemy.MagicDamageResist)}");
                    eth.UseAbility(enemy);
                    await Await.Delay((int) (time * 1000.0f + Game.Ping));
                    return;
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
                                         x.IsAlive && x.Team != MyHero.Team && !x.IsMagicImmune() &&
                                         x.Distance2D(MyHero) < dagon.CastRange &&
                                         x.Health < damage * (1 - x.MagicDamageResist));
                if (enemy != null)
                {
                    Log.Debug(
                        $"killsteal dagon {index} damage: {damage} ({damage * (1 - enemy.MagicDamageResist)}) - {dagon.CastRange}");
                    dagon.UseAbility(enemy);
                    await Await.Delay(125);
                }
            }
        }
    }
}