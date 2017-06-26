// <copyright file="Juggernaut.cs" company="Ensage">
//    Copyright (c) 2017 Ensage.
// </copyright>

namespace Vaper.Heroes
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel;
    using System.Linq;
    using System.Reflection;
    using System.Threading;
    using System.Threading.Tasks;

    using Ensage;
    using Ensage.SDK.Abilities;
    using Ensage.SDK.Abilities.Items;
    using Ensage.SDK.Abilities.npc_dota_hero_juggernaut;
    using Ensage.SDK.Extensions;
    using Ensage.SDK.Geometry;
    using Ensage.SDK.Handlers;
    using Ensage.SDK.Helpers;
    using Ensage.SDK.Inventory.Metadata;
    using Ensage.SDK.Menu;
    using Ensage.SDK.Utils;

    using log4net;

    using PlaySharp.Toolkit.Helper.Annotations;
    using PlaySharp.Toolkit.Logging;

    using SharpDX;

    using Vaper.OrbwalkingModes;

    using Color = System.Drawing.Color;

    [ExportHero(HeroId.npc_dota_hero_juggernaut)]
    public class Juggernaut : BaseHero
    {
        private static readonly ILog Log = AssemblyLogs.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private Unit healingWardUnit;

        private float lastAttackTime;

        [PublicAPI]
        [ItemBinding]
        public item_abyssal_blade AbyssalBlade { get; private set; }

        public juggernaut_blade_fury BladeFury { get; private set; }

        public MenuItem<bool> BladeFuryMoveOnly { get; private set; }

        [PublicAPI]
        [ItemBinding]
        public item_blink Blink { get; private set; }

        public MenuItem<bool> ControlWard { get; set; }

        public juggernaut_blade_dance Crit { get; private set; }

        public MenuItem<bool> CritIndicator { get; private set; }

        public float CurrentCritChance { get; private set; }

        public DiffusalBlade Diffusal
        {
            get
            {
                if (this.Diffu2 != null)
                {
                    return this.Diffu2;
                }

                return this.Diffu1;
            }
        }

        public juggernaut_healing_ward HealingWard { get; private set; }

        [PublicAPI]
        [ItemBinding]
        public item_manta Manta { get; private set; }

        [PublicAPI]
        [ItemBinding]
        public item_mjollnir Mjollnir { get; private set; }

        public MenuItem<bool> OmniBlink { get; private set; }

        public juggernaut_omni_slash OmniSlash { get; private set; }

        [PublicAPI]
        [ItemBinding]
        protected item_diffusal_blade Diffu1 { get; private set; }

        [PublicAPI]
        [ItemBinding]
        protected item_diffusal_blade_2 Diffu2 { get; private set; }

        protected TaskHandler HealingWardControlHandler { get; private set; }

        protected TaskHandler OmniBlinkHandler { get; private set; }

        protected override VaperOrbwalkingMode GetOrbwalkingMode()
        {
            return new JuggernautOrbwalker(this);
        }

        protected override void OnActivate()
        {
            base.OnActivate();
            this.Ensage.Inventory.Attach(this);

            this.BladeFury = this.Ensage.AbilityFactory.GetAbility<juggernaut_blade_fury>();
            this.HealingWard = this.Ensage.AbilityFactory.GetAbility<juggernaut_healing_ward>();
            this.Crit = this.Ensage.AbilityFactory.GetAbility<juggernaut_blade_dance>();
            this.OmniSlash = this.Ensage.AbilityFactory.GetAbility<juggernaut_omni_slash>();

            this.OmniBlinkHandler = UpdateManager.Run(this.OnOmniBlink, false, false);
            this.HealingWardControlHandler = UpdateManager.Run(this.OnHealingWardControl, false, false);

            var factory = this.Menu.Hero.Factory;
            this.CritIndicator = factory.Item("Show Crit Indicator", true);
            this.CritIndicator.PropertyChanged += this.CritIndicatorPropertyChanged;
            this.OmniBlink = factory.Item("Blink while using Omnislash", true);
            this.OmniBlink.PropertyChanged += this.OmniBlinkOnPropertyChanged;
            this.ControlWard = factory.Item("Control Healing Ward", true);
            this.ControlWard.PropertyChanged += this.ControlWardPropertyChanged;
            this.BladeFuryMoveOnly = factory.Item("Bladefury move only", true);

            Entity.OnInt32PropertyChange += this.OnNetworkActivity;
            if (this.CritIndicator)
            {
                this.Ensage.Renderer.Draw += this.OnDraw;
            }

            if (this.OmniBlink)
            {
                Unit.OnModifierAdded += this.OnOmniUsage;
            }

            if (this.ControlWard)
            {
                ObjectManager.OnAddEntity += this.OnHealingWardAdded;
            }
        }

        protected override void OnDeactivate()
        {
            this.ControlWard.PropertyChanged -= this.ControlWardPropertyChanged;
            this.OmniBlink.PropertyChanged -= this.OmniBlinkOnPropertyChanged;
            this.CritIndicator.PropertyChanged -= this.CritIndicatorPropertyChanged;

            ObjectManager.OnAddEntity -= this.OnHealingWardAdded;
            Unit.OnModifierAdded -= this.OnOmniUsage;
            Entity.OnInt32PropertyChange -= this.OnNetworkActivity;
            this.Ensage.Renderer.Draw -= this.OnDraw;

            this.OmniBlinkHandler.Cancel();

            this.Ensage.Inventory.Detach(this);
            base.OnDeactivate();
        }

        private void ControlWardPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (this.ControlWard)
            {
                ObjectManager.OnAddEntity += this.OnHealingWardAdded;
            }
            else
            {
                ObjectManager.OnAddEntity -= this.OnHealingWardAdded;
                this.HealingWardControlHandler.Cancel();
            }
        }

        private void CritIndicatorPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (this.CritIndicator)
            {
                this.Ensage.Renderer.Draw += this.OnDraw;
            }
            else
            {
                this.Ensage.Renderer.Draw -= this.OnDraw;
            }
        }

        private void OmniBlinkOnPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (this.OmniBlink)
            {
                Unit.OnModifierAdded += this.OnOmniUsage;
            }
            else
            {
                Unit.OnModifierAdded -= this.OnOmniUsage;
                this.OmniBlinkHandler.Cancel();
            }
        }

        private void OnDraw(object sender, EventArgs e)
        {
            if (this.Crit.Ability.Level <= 0)
            {
                return;
            }

            Vector2 screenPos;
            var barPos = this.Owner.Position + new Vector3(0, 0, this.Owner.HealthBarOffset);
            if (Drawing.WorldToScreen(barPos, out screenPos))
            {
                this.Ensage.Renderer.DrawRectangle(new RectangleF(screenPos.X - 40, screenPos.Y - 12, 80, 7), Color.Red);

                var critWidth = 80.0f * this.CurrentCritChance;
                this.Ensage.Renderer.DrawLine(new Vector2(screenPos.X - 40, screenPos.Y - 8), new Vector2((screenPos.X - 40) + critWidth, screenPos.Y - 8), Color.Red, 7);
            }
        }

        private void OnHealingWardAdded(EntityEventArgs args)
        {
            var unit = args.Entity as Unit;
            if ((unit != null) && (unit.Team == this.Owner.Team) && (args.Entity.Name == "npc_dota_juggernaut_healing_ward"))
            {
                this.healingWardUnit = unit;
                this.HealingWardControlHandler.RunAsync();
            }
        }

        private async Task OnHealingWardControl(CancellationToken token)
        {
            if (this.healingWardUnit == null)
            {
                return;
            }

            var team = this.healingWardUnit.Team;
            var healingRadius = this.HealingWard.Radius;
            while ((this.healingWardUnit != null) && this.healingWardUnit.IsValid && this.healingWardUnit.IsAlive)
            {
                if (Game.IsPaused)
                {
                    await Task.Delay(125, token);
                    continue;
                }

                var enemyHeroes = EntityManager<Hero>.Entities.Where(x => x.IsAlive && x.IsVisible && (x.Team != team) && (x.Distance2D(this.healingWardUnit) < 1000)).ToList();
                var alliedHeroes = EntityManager<Hero>
                    .Entities.Where(x => x.IsAlive && x.IsVisible && (x.Team == team) && (x.HealthPercent() <= 0.9f) && (x.Distance2D(this.healingWardUnit) < 800))
                    .OrderBy(x => x.HealthPercent())
                    .ToList();
                if (!alliedHeroes.Any())
                {
                    if (!this.Owner.IsAlive)
                    {
                        // nobody is there to heal ... *feelsbadman*
                        await Task.Delay(125, token);
                        continue;
                    }

                    alliedHeroes.Add(this.Owner);
                }

                // get areas to avoid with the healing ward
                var avoidCircles = new List<Polygon.Circle>(enemyHeroes.Count);
                foreach (var enemyHero in enemyHeroes)
                {
                    var dangerRange = enemyHero.AttackRange(this.healingWardUnit);
                    dangerRange = enemyHero.IsMelee ? dangerRange * 2f : dangerRange * 1.2f;

                    var circle = new Polygon.Circle(enemyHero.Position, dangerRange);

                    // Log.Debug($"Adding avoid circle {circle.Center} ({circle.Radius})");
                    avoidCircles.Add(circle);
                }

                // find allies which need healing and are not close to enemies
                var healCircles = new List<Polygon.Circle>(alliedHeroes.Count);
                foreach (var alliedHero in alliedHeroes)
                {
                    var circle = new Polygon.Circle(alliedHero.Position, healingRadius);
                    if (avoidCircles.Exists(x => x.Center.Distance(circle.Center) <= Math.Abs(x.Radius - circle.Radius)))
                    {
                        // Log.Debug($"Skipping heal circle {circle.Center} ({circle.Radius})");
                        continue;
                    }

                    // Log.Debug($"Adding heal circle {circle.Center} ({circle.Radius})");
                    healCircles.Add(circle);
                }

                var hasMoved = false;
                if (healCircles.Any())
                {
                    while (healCircles.Count > 1)
                    {
                        var mecResult = MEC.GetMec(healCircles.Select((target) => target.Center).ToList());
                        if ((mecResult.Radius != 0f) && (mecResult.Radius < healingRadius))
                        {
                            var movePos = new Vector3(
                                healCircles.Count <= 2 ? (healCircles[0].Center + healCircles[1].Center) / 2 : mecResult.Center,
                                this.healingWardUnit.Position.Z);

                            if (avoidCircles.TrueForAll(x => !x.IsInside(movePos)))
                            {
                                this.healingWardUnit.Move(movePos);
                                hasMoved = true;
                                break;
                            }
                        }

                        Log.Debug($"removing target since radius {mecResult.Radius} or movePos to dangerous");
                        var itemToRemove = healCircles.Where(x => x.Center != this.Owner.Position.ToVector2())
                                                      .MaxOrDefault((target) => healCircles[0].Center.DistanceSquared(target.Center));
                        healCircles.Remove(itemToRemove);
                    }
                }

                // no safe area available... so just move to the target who needs it most and hope for the best
                if (!healCircles.Any() || !hasMoved)
                {
                    var isOwnerLow = this.Owner.HealthPercent() <= 0.5f;
                    var heroPos = isOwnerLow ? this.Owner.Position : alliedHeroes.First().Position;
                    if (!avoidCircles.Any())
                    {
                        this.healingWardUnit.Move(heroPos);
                        Log.Debug($"No heal + avoid circles, moving to first allied");
                    }
                    else
                    {
                        var z = this.healingWardUnit.Position.Z;

                        var clusterPos = Vector3.Zero;
                        foreach (var avoidCircle in avoidCircles)
                        {
                            clusterPos += avoidCircle.Center.ToVector3(z);
                        }

                        clusterPos /= avoidCircles.Count;

                        var movePos = (clusterPos - heroPos).Normalized();
                        movePos = heroPos + (movePos * healingRadius * -1f);
                        this.healingWardUnit.Move(movePos);
                        Log.Debug($"No heal, moving to first allied and away from enemies");
                    }
                }

                await Task.Delay(125, token);
            }

            this.healingWardUnit = null;
        }

        private void OnNetworkActivity(Entity sender, Int32PropertyChangeEventArgs args)
        {
            if (sender != this.Owner)
            {
                return;
            }

            if (args.PropertyName != "m_NetworkActivity")
            {
                return;
            }

            var newNetworkActivity = (NetworkActivity)args.NewValue;
            switch (newNetworkActivity)
            {
                case NetworkActivity.Attack:
                case NetworkActivity.Attack2:
                    this.lastAttackTime = Game.GameTime;
                    if (this.Crit.Ability.Level <= 0)
                    {
                        return;
                    }

                    // TODO: check for allies, buildings and wards target
                    this.CurrentCritChance = Math.Min(1.0f, this.CurrentCritChance + Utils.GetPseudoChance(this.Crit.ProcChance));
                    break;

                case NetworkActivity.Crit:
                    this.lastAttackTime = Game.GameTime;
                    if (this.Crit.Ability.Level <= 0)
                    {
                        return;
                    }

                    // Pseudo-random_distribution
                    this.CurrentCritChance = Utils.GetPseudoChance(this.Crit.ProcChance);
                    break;
            }
        }

        private async Task OnOmniBlink(CancellationToken token)
        {
            var startTime = Game.GameTime;
            var duration = this.OmniSlash.Duration;
            var radius = this.OmniSlash.Radius;
            var myTeam = this.Owner.Team;

            Unit lastTarget = null;

            await Task.Delay((int)(this.OmniSlash.TickRate * 1000), token);
            while (((Game.GameTime - startTime) <= duration) && this.Owner.HasModifier(this.OmniSlash.ModifierName))
            {
                var closestTarget = EntityManager<Unit>
                    .Entities.Where(x => x.IsVisible && x.IsAlive && (x.Team != myTeam) && !(x is Building) && x.IsRealUnit() && (x.Distance2D(this.Owner) < radius))
                    .OrderBy(x => x.Distance2D(this.Owner))
                    .FirstOrDefault();

                var blinkToNextTarget = closestTarget == null;
                if (blinkToNextTarget)
                {
                    Log.Debug("already null");
                }

                Log.Debug($"closest target: {closestTarget?.Name}: {closestTarget?.Distance2D(this.Owner)} | {closestTarget?.Distance2D(this.Owner) < radius}");
                if (!blinkToNextTarget)
                {
                    if (closestTarget.Health < this.OmniSlash.GetTickDamage(closestTarget))
                    {
                        var timeFromLastAttack = Game.GameTime - this.lastAttackTime;
                        var timeTillNextAttack = this.OmniSlash.TickRate - timeFromLastAttack;
                        if (timeTillNextAttack > 0.01)
                        {
                            var waitTime = (int)((timeTillNextAttack + (this.OmniSlash.TickRate / 4)) * 1000.0f);

                            Log.Debug($"wait: {waitTime} | {timeTillNextAttack} + {this.OmniSlash.TickRate / 4} + {Game.Ping}");
                            await Task.Delay(waitTime, token);
                            blinkToNextTarget = true;
                        }
                    }
                }

                if (blinkToNextTarget)
                {
                    if (this.Blink.CanBeCasted)
                    {
                        var nextTarget = EntityManager<Hero>
                            .Entities.Where(
                                x => x.IsVisible
                                     && x.IsAlive
                                     && (x.Team != myTeam)
                                     && (x != closestTarget)
                                     && (x != lastTarget)
                                     && !x.IsIllusion
                                     && (x.Distance2D(this.Owner) < (this.Blink.CastRange + (radius / 2))))
                            .OrderBy(x => x.Health)
                            .FirstOrDefault();

                        if (nextTarget != null)
                        {
                            Log.Debug($"blink to {nextTarget.Name}");

                            var blinkPos = (nextTarget.Position - this.Owner.Position).Normalized();
                            blinkPos = this.Owner.Position + (blinkPos * Math.Min(this.Blink.CastRange, nextTarget.Distance2D(this.Owner)));
                            this.Blink.UseAbility(blinkPos);
                        }
                    }

                    return;
                }

                lastTarget = closestTarget;
                await Task.Delay(50, token);
            }
        }

        private void OnOmniUsage(Unit sender, ModifierChangedEventArgs args)
        {
            if (sender != this.Owner)
            {
                return;
            }

            if (args.Modifier.Name != this.OmniSlash.ModifierName)
            {
                return;
            }

            if (this.OmniBlink && (this.Blink != null))
            {
                this.OmniBlinkHandler.RunAsync();
            }
        }
    }
}