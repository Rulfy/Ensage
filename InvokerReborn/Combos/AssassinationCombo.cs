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

namespace InvokerReborn.Combos
{
    internal sealed class AssassinationCombo : InvokerCombo
    {
        private readonly DeafeningBlast _deafeningBlast;
        private readonly Meteor _meteor1;
        private readonly Meteor _meteor2;
        private readonly Refresher _refresher;
        private readonly Sunstrike _sunstrike1;
        private readonly Sunstrike _sunstrike2;
        private readonly Tornado _tornado;

        public AssassinationCombo(Hero me, Key key) : base(me, key)
        {
            _tornado = new Tornado(me, () => 0);
            _sunstrike1 = new Sunstrike(me, SunstrikeDelay1);
            _meteor1 = new Meteor(me, MeteorDelay1);
            _deafeningBlast = new DeafeningBlast(me, BlastDelay);
            _refresher = new Refresher(me);
            _meteor2 = new Meteor(me);
            _sunstrike2 = new Sunstrike(me);
            //  _sunstrike2.PositionChange = ...

            AbilitySequence = new List<ISequenceEntry>
            {
                new AwaitBlinkOrMove(me, () => EngageRange),
                _tornado,
                new AwaitModifier("modifier_invoker_tornado", 3000),
                _sunstrike1,
                _meteor1,
                _deafeningBlast,
                _refresher,
                _meteor2,
                _sunstrike2
            };
        }

        protected override int EngageRange => Math.Min(_tornado.Distance, 2400);

        private int TornadaTraveltime()
        {
            var travelSpeed = _deafeningBlast.Ability.AbilitySpecialData.First(x => x.Name == "travel_speed").Value;
            return (int) (Me.Distance2D(Target)/travelSpeed*1000);
        }

        private int SunstrikeDelay1()
        {
            return _tornado.Duration + TornadaTraveltime() - _sunstrike1.Delay;
        }

        private int MeteorDelay1()
        {
            return _tornado.Duration + TornadaTraveltime() - SunstrikeDelay1() - _meteor1.Delay;
        }

        private int BlastDelay()
        {
            var travelSpeed = _deafeningBlast.Ability.AbilitySpecialData.First(x => x.Name == "travel_speed").Value;
            var blastDelayTime = (int) (Me.Distance2D(Target)/travelSpeed*1000);

            return _tornado.Duration + TornadaTraveltime() - SunstrikeDelay1() - MeteorDelay1() - blastDelayTime;
        }
    }
}