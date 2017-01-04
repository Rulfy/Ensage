namespace InvokerReborn.Combos
{
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

        public EulsSSMeteorBlast(Hero me, Key key)
            : base(me, key)
        {
            this._euls = new Euls(me, () => 0);
            this._sunstrike = new Sunstrike(me, this.SunstrikeDelay);
            this._meteor = new Meteor(me, this.MeteorDelay);
            this._deafeningBlast = new DeafeningBlast(me, this.BlastDelay);
            this._coldSnap = new ColdSnap(me);
            this._forgeSpirit = new ForgeSpirit(me);

            this.AbilitySequence = new List<ISequenceEntry>
                                       {
                                           new AwaitBlinkOrMove(me, () => this.EngageRange),
                                           this._euls,
                                           new AwaitModifier("modifier_eul_cyclone", 250),
                                           this._sunstrike,
                                           this._meteor,
                                           this._deafeningBlast,
                                           this._coldSnap,
                                           this._forgeSpirit
                                       };
        }

        protected override int EngageRange
        {
            get
            {
                if (this._euls.Ability == null)
                {
                    return 0;
                }

                return (int)this._euls.Ability.CastRange;
            }
        }

        private int BlastDelay()
        {
            var travelSpeed = this._deafeningBlast.Ability.AbilitySpecialData.First(x => x.Name == "travel_speed").Value;
            var blastDelayTime = (int)(this.Me.Distance2D(this.Target) / travelSpeed * 1000);

            return this._euls.Duration - this.SunstrikeDelay() - this.MeteorDelay() - blastDelayTime;
        }

        private int MeteorDelay()
        {
            return this._euls.Duration - this.SunstrikeDelay() - this._meteor.Delay; // 2.5 - 1.7 - 1.3 = 0.4
        }

        private int SunstrikeDelay()
        {
            return this._euls.Duration - this._sunstrike.Delay; // 2.5 - 1.7 = 0.8
        }
    }
}