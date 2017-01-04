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

    internal class ToEMPMetBlastRefMetBlastEMP : InvokerCombo
    {
        private readonly ColdSnap _coldSnap;

        private readonly DeafeningBlast _deafeningBlast1;

        private readonly DeafeningBlast _deafeningBlast2;

        private readonly EMP _emp1;

        private readonly EMP _emp2;

        private readonly Meteor _meteor1;

        private readonly Meteor _meteor2;

        private readonly Refresher _refresher;

        private readonly Sunstrike _sunstrike;

        private readonly Tornado _tornado;

        private int _originalTornadoTravelTime;

        public ToEMPMetBlastRefMetBlastEMP(Hero me, Key key)
            : base(me, key)
        {
            this._tornado = new Tornado(me, () => 0);
            this._emp1 = new EMP(me, this.EMPDelay1);
            this._meteor1 = new Meteor(me, this.MeteorDelay1);
            this._deafeningBlast1 = new DeafeningBlast(me, this.BlastDelay1);

            this._refresher = new Refresher(me);
            this._meteor2 = new Meteor(me);
            this._deafeningBlast2 = new DeafeningBlast(me);
            this._emp2 = new EMP(me);
            this._sunstrike = new Sunstrike(me);
            this._coldSnap = new ColdSnap(me);

            // _sunstrike2.PositionChange = ...
            this.AbilitySequence = new List<ISequenceEntry>
                                       {
                                           new AwaitBlinkOrMove(me, () => this.EngageRange),
                                           this._tornado,
                                           new AwaitModifier("modifier_invoker_tornado", 3000),
                                           new AwaitBlinkOrMove(me, () => (int)this._meteor1.Ability.CastRange),
                                           this._emp1,
                                           this._meteor1,
                                           this._deafeningBlast1,
                                           this._refresher,
                                           this._meteor2,
                                           this._deafeningBlast2,
                                           this._emp2,
                                           this._coldSnap // sunstrike?
                                       };
        }

        protected override int EngageRange => Math.Min(this._tornado.Distance, 2050);

        private int BlastDelay1()
        {
            var travelSpeed =
                this._deafeningBlast1.Ability.AbilitySpecialData.First(x => x.Name == "travel_speed").Value;
            var blastDelayTime = (int)(this.Me.Distance2D(this.Target) / travelSpeed * 1000);

            return this._tornado.Duration + this._originalTornadoTravelTime - this.EMPDelay1() - this.MeteorDelay1();
        }

        private int EMPDelay1()
        {
            this._originalTornadoTravelTime = this.TornadaTraveltime();
            return this._tornado.Duration + this._originalTornadoTravelTime - this._emp1.Delay;
        }

        private int MeteorDelay1()
        {
            return this._tornado.Duration + this._originalTornadoTravelTime - this.EMPDelay1() - this._meteor1.Delay
                   + 500;
        }

        private int TornadaTraveltime()
        {
            var travelSpeed = this._tornado.Ability.AbilitySpecialData.First(x => x.Name == "travel_speed").Value;
            return (int)(this.Me.Distance2D(this.Target) / travelSpeed * 1000);
        }
    }
}