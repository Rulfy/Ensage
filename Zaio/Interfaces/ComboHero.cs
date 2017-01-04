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

        public static ClassID HeroClassId { get; }

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
        }

        public virtual void OnClose()
        {
        }

        public virtual void OnDraw()
        {
            if (_executed && Target != null && Target.IsAlive)
            {
                Drawing.DrawText($"Killing {Game.Localize(Target.Name)}", Game.MouseScreenPosition + new Vector2(28, 5),
                    new Vector2(24, 200), Color.Red, FontFlags.AntiAlias | FontFlags.DropShadow);
            }
        }

        protected override async Task Execute(CancellationToken token)
        {
            if (!ZaioMenu.ShouldLockTarget || (Target == null || !Target.IsAlive))
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
            float minimumRange = 0.0f)
        {
            var distance = MyHero.Distance2D(Target) - Target.HullRadius - MyHero.HullRadius;
            if (distance <= MyHero.GetAttackRange() * 1.1)
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
            if ((Target.IsHexed(out duration) || Target.IsStunned(out duration) || Target.IsSilenced() || Target.IsDisarmed()) && duration >= minimumTime)
            {
                return true;
            }
            var itemList = new[] {"item_sheepstick", "item_abyssal_blade", "item_bloodthorn", "item_orchid", "item_heavens_halberd" };
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
    }
}