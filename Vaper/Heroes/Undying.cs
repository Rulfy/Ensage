// <copyright file="Undying.cs" company="Ensage">
//    Copyright (c) 2017 Ensage.
// </copyright>

namespace Vaper.Heroes
{
    using System;
    using System.ComponentModel;
    using System.Linq;
    using System.Reflection;
    using System.Threading;
    using System.Threading.Tasks;

    using Ensage;
    using Ensage.SDK.Abilities.npc_dota_hero_undying;
    using Ensage.SDK.Extensions;
    using Ensage.SDK.Helpers;
    using Ensage.SDK.Menu;

    using log4net;

    using PlaySharp.Toolkit.Logging;

    using SharpDX;

    using Vaper.OrbwalkingModes;

    using Color = System.Drawing.Color;

    [ExportHero(HeroId.npc_dota_hero_undying)]
    public class Undying : BaseHero
    {
        private static readonly ILog Log = AssemblyLogs.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        public undying_decay Decay { get; private set; }

        public undying_flesh_golem Golem { get; private set; }

        public undying_soul_rip SoulRip { get; private set; }

        public MenuItem<bool> SoulRipIndicator { get; private set; }

        public undying_tombstone Tombstone { get; private set; }

        protected override VaperOrbwalkingMode GetOrbwalkingMode()
        {
            return new UndyingOrbwalker(this);
        }

        protected override void OnActivate()
        {
            base.OnActivate();

            this.Decay = this.Ensage.AbilityFactory.GetAbility<undying_decay>();
            this.SoulRip = this.Ensage.AbilityFactory.GetAbility<undying_soul_rip>();
            this.Tombstone = this.Ensage.AbilityFactory.GetAbility<undying_tombstone>();
            this.Golem = this.Ensage.AbilityFactory.GetAbility<undying_flesh_golem>();

            var factory = this.Menu.Hero.Factory;
            this.SoulRipIndicator = factory.Item("Affected Unit Count by SoulRip", true);
            this.SoulRipIndicator.PropertyChanged += this.SoulRipIndicatorPropertyChanged;

            if (this.SoulRipIndicator)
            {
                this.Ensage.Renderer.Draw += this.OnDraw;
            }
        }

        protected override void OnDeactivate()
        {
            this.Ensage.Renderer.Draw -= this.OnDraw;

            base.OnDeactivate();
        }

        protected override async Task OnKillsteal(CancellationToken token)
        {
            if (Game.IsPaused || !this.Owner.IsAlive)
            {
                await Task.Delay(125, token);
                return;
            }

            if (this.SoulRip.CanBeCasted)
            {
                var killstealTarget = EntityManager<Hero>.Entities.FirstOrDefault(
                    x => x.IsAlive
                         && (x.Team != this.Owner.Team)
                         && !x.IsIllusion
                         && this.SoulRip.CanHit(x)
                         && !x.IsLinkensProtected()
                         && (this.SoulRip.GetDamage(x) > x.Health));

                if (killstealTarget != null)
                {
                    // Log.Debug($"damage {this.SoulRip.GetDamage(killstealTarget)}");
                    this.SoulRip.UseAbility(killstealTarget);
                    var castDelay = this.SoulRip.GetCastDelay(killstealTarget);
                    await this.AwaitKillstealDelay(castDelay, token);

                    await Task.Delay(125, token);
                    return;
                }
            }

            if (this.Decay.CanBeCasted)
            {
                var killstealTarget = EntityManager<Hero>.Entities.FirstOrDefault(
                    x => x.IsAlive
                         && (x.Team != this.Owner.Team)
                         && !x.IsIllusion
                         && this.Decay.CanHit(x)
                         && (this.Decay.GetHealthLeft(x) <= 0));

                if (killstealTarget != null)
                {
                    if (this.Decay.UseAbility(killstealTarget))
                    {
                        var castDelay = this.Decay.GetCastDelay(killstealTarget);
                        await this.AwaitKillstealDelay(castDelay, token);
                    }
                }
            }

            await Task.Delay(125, token);
        }

        private void OnDraw(object sender, EventArgs e)
        {
            if (!this.SoulRip.CanBeCasted)
            {
                return;
            }

            Vector2 screenPos;
            var barPos = this.Owner.Position + new Vector3(0, 0, this.Owner.HealthBarOffset);
            if (Drawing.WorldToScreen(barPos, out screenPos))
            {
                this.Ensage.Renderer.DrawRectangle(new RectangleF(screenPos.X - 40, screenPos.Y - 12, 80, 7), Color.Red);

                var affected = this.SoulRip.GetAffectedUnitCount(null);
                var maxAffected = this.SoulRip.MaxUnits;

                var percentage = (float)affected / maxAffected;

                var width = 80.0f * percentage;
                this.Ensage.Renderer.DrawLine(new Vector2(screenPos.X - 40, screenPos.Y - 8), new Vector2((screenPos.X - 40) + width, screenPos.Y - 8), Color.Red, 7);
            }
        }

        private void SoulRipIndicatorPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (this.SoulRipIndicator)
            {
                this.Ensage.Renderer.Draw += this.OnDraw;
            }
            else
            {
                this.Ensage.Renderer.Draw -= this.OnDraw;
            }
        }
    }
}