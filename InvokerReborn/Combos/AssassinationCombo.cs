namespace InvokerReborn.Combos
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Windows.Input;

    using Ensage;
    using Ensage.Common.Extensions;

    using InvokerReborn.Abilities;
    using InvokerReborn.Interfaces;
    using InvokerReborn.Items;
    using InvokerReborn.SequenceHelpers;

    internal sealed class AssassinationCombo : InvokerCombo
    {
        private readonly DeafeningBlast _deafeningBlast;

        private readonly Meteor _meteor1;

        private readonly Meteor _meteor2;

        private readonly Refresher _refresher;

        private readonly Sunstrike _sunstrike1;

        private readonly Sunstrike _sunstrike2;

        private readonly Tornado _tornado;

        private int _originalTornadoTravelTime;

        public AssassinationCombo(Hero me, Key key)
            : base(me, key)
        {
            this._tornado = new Tornado(me, () => 0);
            this._sunstrike1 = new Sunstrike(me, this.SunstrikeDelay1);
            this._meteor1 = new Meteor(me, this.MeteorDelay1);
            this._deafeningBlast = new DeafeningBlast(me, this.BlastDelay);
            this._refresher = new Refresher(me);
            this._meteor2 = new Meteor(me);
            this._sunstrike2 = new Sunstrike(me);

            // _sunstrike2.PositionChange = ...
            this.AbilitySequence = new List<ISequenceEntry>
                                       {
                                           new AwaitBlinkOrMove(me, () => this.EngageRange),
                                           this._tornado,
                                           new AwaitModifier("modifier_invoker_tornado", 3000),
                                           new AwaitBlinkOrMove(me, () => (int)this._meteor1.Ability.CastRange),
                                           this._sunstrike1,
                                           this._meteor1,
                                           this._deafeningBlast,
                                           this._refresher,
                                           this._meteor2,
                                           this._sunstrike2
                                       };
        }

        protected override int EngageRange => Math.Min(this._tornado.Distance, 2400);

        private int BlastDelay()
        {
            var travelSpeed = this._deafeningBlast.Ability.AbilitySpecialData.First(x => x.Name == "travel_speed").Value;
            var blastDelayTime = (int)(this.Me.Distance2D(this.Target) / travelSpeed * 1000);

            return this._tornado.Duration + this._originalTornadoTravelTime - this.SunstrikeDelay1()
                   - this.MeteorDelay1() - blastDelayTime;
        }

        private int MeteorDelay1()
        {
            return this._tornado.Duration + this._originalTornadoTravelTime - this.SunstrikeDelay1()
                   - this._meteor1.Delay;
        }

        private int SunstrikeDelay1()
        {
            this._originalTornadoTravelTime = this.TornadaTraveltime();
            return this._tornado.Duration + this._originalTornadoTravelTime - this._sunstrike1.Delay;
        }

        private int TornadaTraveltime()
        {
            var travelSpeed = this._tornado.Ability.AbilitySpecialData.First(x => x.Name == "travel_speed").Value;
            return (int)(this.Me.Distance2D(this.Target) / travelSpeed * 1000);
        }
    }
}