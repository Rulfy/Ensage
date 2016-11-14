using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Windows.Input;
using Ensage;
using Ensage.Common.Extensions;
using InvokerReborn.Abilities;
using InvokerReborn.Interfaces;
using InvokerReborn.Items;
using InvokerReborn.SequenceHelpers;
using log4net;
using PlaySharp.Toolkit.Logging;

namespace InvokerReborn.Combos
{
    // ReSharper disable once InconsistentNaming
    internal sealed class EulsSSMeteorBlast : InvokerCombo
    {
        private static readonly ILog Log = AssemblyLogs.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private readonly ColdSnap _coldSnap;
        private readonly DeafeningBlast _deafeningBlast;
        private readonly Euls _euls;
        private readonly ForgeSpirit _forgeSpirit;
        private readonly Meteor _meteor;
        private readonly Sunstrike _sunstrike;

        public EulsSSMeteorBlast(Hero me, Key key) : base(me, key)
        {
            _euls = new Euls(me, () => 0);
            _sunstrike = new Sunstrike(me, SunstrikeDelay);
            _meteor = new Meteor(me, MeteorDelay);
            _deafeningBlast = new DeafeningBlast(me, BlastDelay);
            _coldSnap = new ColdSnap(me);
            _forgeSpirit = new ForgeSpirit(me);

            AbilitySequence = new List<ISequenceEntry>
            {
                new AwaitBlinkOrMove(me, () => EngageRange),
                _euls,
                new AwaitModifier("modifier_eul_cyclone", 250),
                _sunstrike,
                _meteor,
                _deafeningBlast,
                _coldSnap,
                _forgeSpirit
            };
        }

        protected override int EngageRange
        {
            get
            {
                if (_euls.Ability == null)
                    return 0;
                return (int) _euls.Ability.CastRange;
            }
        }

        private int SunstrikeDelay()
        {
            return _euls.Duration - _sunstrike.Delay; // 2.5 - 1.7 = 0.8
        }

        private int MeteorDelay()
        {
            return _euls.Duration - SunstrikeDelay() - _meteor.Delay; // 2.5 - 1.7 - 1.3 = 0.4
        }

        private int BlastDelay()
        {
            var travelSpeed = _deafeningBlast.Ability.AbilitySpecialData.First(x => x.Name == "travel_speed").Value;
            var blastDelayTime = (int) (Me.Distance2D(Target)/travelSpeed*1000);

            return _euls.Duration - SunstrikeDelay() - MeteorDelay() - blastDelayTime;
        }
    }
}