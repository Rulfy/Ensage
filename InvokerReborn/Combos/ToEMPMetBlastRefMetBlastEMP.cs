using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using Ensage;
using Ensage.Common.Extensions;
using InvokerReborn.Abilities;
using InvokerReborn.Interfaces;
using InvokerReborn.Items;
using InvokerReborn.SequenceHelpers;

namespace InvokerReborn.Combos
{
    internal class ToEMPMetBlastRefMetBlastEMP : InvokerCombo
    {
        private readonly DeafeningBlast _deafeningBlast1;
        private readonly DeafeningBlast _deafeningBlast2;
        private readonly EMP _emp1;
        private readonly EMP _emp2;
        private readonly Meteor _meteor1;
        private readonly Meteor _meteor2;
        private readonly Refresher _refresher;
        private readonly Tornado _tornado;
        private readonly Sunstrike _sunstrike;
        private readonly ColdSnap _coldSnap;

        public ToEMPMetBlastRefMetBlastEMP(Hero me, Key key) : base(me, key)
        {
            _tornado = new Tornado(me, () => 0);
            _emp1 = new EMP(me, EMPDelay1);
            _meteor1 = new Meteor(me, MeteorDelay1);
            _deafeningBlast1 = new DeafeningBlast(me, BlastDelay1);

            _refresher = new Refresher(me);
            _meteor2 = new Meteor(me);
            _deafeningBlast2 = new DeafeningBlast(me);
            _emp2 = new EMP(me);
            _sunstrike = new Sunstrike(me);
            _coldSnap = new ColdSnap(me);
            //  _sunstrike2.PositionChange = ...

            AbilitySequence = new List<ISequenceEntry>
            {
                new AwaitBlinkOrMove(me, () => EngageRange),
                _tornado,
                new AwaitModifier("modifier_invoker_tornado", 3000),
                new AwaitBlinkOrMove(me, () => (int)_meteor1.Ability.CastRange),
                _emp1,
                _meteor1,
                _deafeningBlast1,
                new OptionalItemAborter(me, "item_refresher"),
                _refresher,
                _meteor2,
                _deafeningBlast2,
                _emp2,
                _coldSnap // sunstrike?
            };
        }


        protected override int EngageRange => Math.Min(_tornado.Distance, 2050);

        private int TornadaTraveltime()
        {
            var travelSpeed = _tornado.Ability.AbilitySpecialData.First(x => x.Name == "travel_speed").Value;
            return (int)(Me.Distance2D(Target) / travelSpeed * 1000);
        }

        private int _originalTornadoTravelTime;
        private int EMPDelay1()
        {
            _originalTornadoTravelTime = TornadaTraveltime();
            return _tornado.Duration + _originalTornadoTravelTime - _emp1.Delay;
        }
        private int MeteorDelay1()
        {
            return _tornado.Duration + _originalTornadoTravelTime - EMPDelay1() - _meteor1.Delay;
        }

        private int BlastDelay1()
        {
            var travelSpeed = _deafeningBlast1.Ability.AbilitySpecialData.First(x => x.Name == "travel_speed").Value;
            var blastDelayTime = (int)(Me.Distance2D(Target) / travelSpeed * 1000);

            return _tornado.Duration + _originalTornadoTravelTime - EMPDelay1() - MeteorDelay1();
        }
      
    }
}