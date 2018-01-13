// <copyright file="FailSwitch.cs" company="Ensage">
//    Copyright (c) 2018 Ensage.
// </copyright>

namespace FailSwitch
{
    using System;
    using System.ComponentModel.Composition;
    using System.Linq;
    using System.Threading.Tasks;

    using Ensage;
    using Ensage.SDK.Abilities;
    using Ensage.SDK.Abilities.Items;
    using Ensage.SDK.Extensions;
    using Ensage.SDK.Geometry;
    using Ensage.SDK.Helpers;
    using Ensage.SDK.Inventory.Metadata;
    using Ensage.SDK.Service;
    using Ensage.SDK.Service.Metadata;

    using NLog;

    using SharpDX;

    using Attribute = Ensage.Attribute;

    [ExportPlugin("FailSwitch")]
    public class FailSwitch : Plugin
    {
        private static readonly Logger Log = LogManager.GetCurrentClassLogger();

        private readonly AbilityId[] pingExcludedAbilities = { AbilityId.item_invis_sword, AbilityId.item_silver_edge, AbilityId.spirit_breaker_charge_of_darkness };

        private readonly IServiceContext context;

        private FailSwitchConfig config;

        private Attribute powerTreadsAttribute;

        private int powerTreadsWaiting;

        private volatile bool toggling;

        [ImportingConstructor]
        public FailSwitch([Import] IServiceContext context)
        {
            this.context = context;
        }

        [ItemBinding]
        public item_power_treads PowerTreads { get; set; }

        protected override void OnActivate()
        {
            this.context.Inventory.Attach(this);

            this.config = new FailSwitchConfig(this.context.Renderer);
            this.context.MenuManager.RegisterMenu(this.config);

            Player.OnExecuteOrder += this.PowerTreadsWatcher;
            Player.OnExecuteOrder += this.Player_OnExecuteOrder;
        }

        protected override void OnDeactivate()
        {
            Player.OnExecuteOrder -= this.Player_OnExecuteOrder;
            Player.OnExecuteOrder -= this.PowerTreadsWatcher;

            this.context.MenuManager.DeregisterMenu(this.config);

            this.context.Inventory.Detach(this);
        }

        private async Task ExecuteOrder(ExecuteOrderEventArgs args)
        {
            Vector3 targetPosition;
            switch (args.OrderId)
            {
                case OrderId.Ability:
                    args.Ability.UseAbility(args.IsQueued);
                    targetPosition = Vector3.Zero;
                    break;

                case OrderId.AbilityLocation:
                    args.Ability.UseAbility(args.TargetPosition, args.IsQueued);
                    targetPosition = args.TargetPosition;
                    break;

                case OrderId.AbilityTarget:
                    if (!args.Target.IsValid || !args.Target.IsVisible || !args.Target.IsAlive)
                    {
                        return;
                    }

                    args.Ability.UseAbility((Unit)args.Target, args.IsQueued);
                    targetPosition = args.Target.Position;
                    break;

                case OrderId.AbilityTargetRune:
                    args.Ability.UseAbility((Rune)args.Target, args.IsQueued);
                    targetPosition = args.Target.Position;
                    break;

                default:
                    return;
            }

            var castPoint = args.Ability is Item ? 1 : args.Ability.GetCastPoint(args.Ability.Level - 1);
            var waitTime = (int)((castPoint * 1000f) + Game.Ping);
            if (targetPosition != Vector3.Zero && args.Entities.First() is Hero hero)
            {
                waitTime += (int)(hero.TurnTime(targetPosition) * 1000f);
            }

            await Task.Delay(waitTime);
        }

        private async void Player_OnExecuteOrder(Player sender, ExecuteOrderEventArgs args)
        {
            try
            {
                if (!(args.Entities.First() is Unit caster))
                {
                    return;
                }

                ActiveAbility ability;
                switch (args.OrderId)
                {
                    case OrderId.Ability:
                    case OrderId.AbilityLocation:
                    case OrderId.AbilityTarget:
                        try
                        {
                            ability = (ActiveAbility)this.context.AbilityFactory.GetAbility(args.Ability);
                        }
                        catch (AbilityNotImplementedException e)
                        {
                            Log.Error(e);
                            return;
                        }

                        break;

                    default:
                        return;
                }

                int delay;
                Vector3 center;
                switch (args.OrderId)
                {
                    case OrderId.Ability:
                        {
                            if (!this.config.NoTargetSpells.PictureStates.TryGetValue(ability.Ability.TextureName, out var state) || !state)
                            {
                                return;
                            }

                            delay = ability.GetCastDelay();
                            center = caster.Position;
                            break;
                        }

                    case OrderId.AbilityLocation:
                        {
                            if (!this.config.AreaSpells.PictureStates.TryGetValue(ability.Ability.TextureName, out var state) || !state)
                            {
                                return;
                            }

                            delay = ability.GetCastDelay(args.TargetPosition);
                            center = args.TargetPosition;
                            break;
                        }

                    case OrderId.AbilityTarget:
                        {
                            if (!this.config.TargetSpells.PictureStates.TryGetValue(ability.Ability.TextureName, out var state) || !state)
                            {
                                return;
                            }

                            if (args.Target is Unit target)
                            {
                                Unit betterTarget = null;

                                // check for real target if we targeted an illusion
                                if (target is Hero heroTarget && target.IsIllusion)
                                {
                                    var replicateFrom = heroTarget.ReplicateFrom;
                                    if (replicateFrom.IsValid
                                        && replicateFrom.IsAlive
                                        && replicateFrom.IsVisible
                                        && !replicateFrom.IsLinkensProtected()
                                        && !replicateFrom.IsReflectingAbilities()
                                        && replicateFrom.IsEnemy(caster)
                                        && ability.CanHit(replicateFrom))
                                    {
                                        betterTarget = replicateFrom;
                                    }
                                }

                                // check for the closest hero target if used on a creep
                                var targetBlocking = target.IsLinkensProtected() || target.IsReflectingAbilities();
                                if (betterTarget == null && (target.IsNeutral || target is Creep || targetBlocking))
                                {
                                    betterTarget = EntityManager<Hero>
                                                   .Entities.Where(
                                                       x => x.IsAlive && x.IsVisible && !x.IsIllusion && !x.IsLinkensProtected() && !x.IsReflectingAbilities() && x.IsEnemy(caster) && ability.CanHit(x))
                                                   .OrderBy(x => x.Distance2D(target))
                                                   .FirstOrDefault();
                                }

                                if (betterTarget != null)
                                {
                                    args.Process = false;
                                    if (this.config.FindRealTarget)
                                    {
                                        ability.UseAbility(betterTarget);
                                    }
                                }
                                else if (targetBlocking)
                                {
                                    args.Process = false;
                                }
                            }

                            return;
                        }

                    default:
                        return;
                }

                if (delay > 0)
                {
                    await Task.Delay((int)(delay - Game.Ping));
                }

                var radius = 0f;
                if (ability is IAreaOfEffectAbility aoeAbility)
                {
                    radius = aoeAbility.Radius;
                }
                else if (ability is CircleAbility circleAbility)
                {
                    radius = circleAbility.Radius;
                }

                if (radius > 0)
                {
                    var circle = new Polygon.Circle(center, radius);
                    if (!EntityManager<Hero>.Entities.Any(x => x.IsAlive && x.IsVisible && x.IsEnemy(this.context.Owner) && circle.IsInside(x.Position)))
                    {
                        caster.Stop();
                    }
                }
            }
            catch (Exception e)
            {
                Log.Error(e);
            }
        }

        private async void PowerTreadsWatcher(Player sender, ExecuteOrderEventArgs args)
        {
            try
            {
                if (!this.config.TogglePowerTreads || !args.IsPlayerInput || !(args.Entities.First() is Hero))
                {
                    return;
                }

                switch (args.OrderId)
                {
                    // only block abilities for toggling
                    case OrderId.Ability:
                    case OrderId.AbilityLocation:
                    case OrderId.AbilityTarget:
                    case OrderId.AbilityTargetRune:
                        break;

                    default:
                        return;
                }

                // if we don't have powertreads or it's already on int, leave it like this
                if (this.PowerTreads == null || this.PowerTreads.ActiveAttribute == Attribute.Intelligence || args.Ability.ManaCost == 0)
                {
                    return;
                }

                try
                {
                    args.Process = false;
                    if (this.toggling)
                    {
                        this.powerTreadsWaiting++;

                        // block order while toggling
                        while (this.toggling)
                        {
                            await Task.Delay(100);
                        }

                        await this.ExecuteOrder(args);
                        return;
                    }

                    // toggle boots
                    this.toggling = true;
                    this.powerTreadsWaiting++;
                    this.powerTreadsAttribute = this.PowerTreads.ActiveAttribute;

                    // while (this.PowerTreads.ActiveAttribute != Attribute.Intelligence)
                    {
                        this.PowerTreads.SwitchAttribute(Attribute.Intelligence);
                        await Task.Delay(100 + (int)Game.Ping);
                    }

                    await this.ExecuteOrder(args);
                }
                finally
                {
                    this.toggling = false;
                    this.powerTreadsWaiting--;
                    
                    // Don't toggle back when casting a channeling ability
                    if (this.powerTreadsWaiting == 0 && ((args.Ability.AbilityBehavior & AbilityBehavior.Channeled) == 0))
                    {
                        // ping protection for invis and charge
                        if (Game.Ping > this.config.PingGuard.Value && this.pingExcludedAbilities.Contains(args.Ability.Id))
                        {
                            Log.Info($"Ignoring toggling back after ability {args.Ability.Name}, because of too high ping {Game.Ping} > {this.config.PingGuard}"); 
                        }
                        else
                        {
                            // restore power treads attribute
                            // while (this.PowerTreads.ActiveAttribute != this.powerTreadsAttribute)
                            {
                                this.PowerTreads.SwitchAttribute(this.powerTreadsAttribute);
                                await Task.Delay(100 + (int)Game.Ping);
                            }
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Log.Error(e);
            }
        }
    }
}