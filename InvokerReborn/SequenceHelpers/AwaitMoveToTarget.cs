namespace InvokerReborn.SequenceHelpers
{
    using System;
    using System.Reflection;
    using System.Threading;
    using System.Threading.Tasks;

    using Ensage;
    using Ensage.Common.Extensions;
    using Ensage.Common.Threading;

    using InvokerReborn.Interfaces;

    using log4net;

    using PlaySharp.Toolkit.Logging;

    using SharpDX;

    internal class AwaitMoveToTarget : ISequenceEntry
    {
        private static readonly ILog Log = AssemblyLogs.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private readonly Func<int> engageRange;

        private readonly Hero me;

        public AwaitMoveToTarget(Hero me, Func<int> engageRange)
        {
            this.me = me;
            this.engageRange = engageRange;
        }

        public int EngageRange => this.engageRange();

        public async Task ExecuteAsync(Unit target, CancellationToken tk = default(CancellationToken))
        {
            await this.ExecuteAsync(target.NetworkPosition, this.engageRange(), tk);
        }

        public async Task ExecuteAsync(
            Vector3 target,
            int engageRange,
            CancellationToken tk = default(CancellationToken))
        {
            if (this.me.Distance2D(target) <= engageRange)
            {
                return;
            }

            Log.Debug($"Execute AwaitMoveToTarget {target} | {engageRange}");

            // try to reach the target in max 5 seconds
            var moveCt = CancellationTokenSource.CreateLinkedTokenSource(
                tk,
                new CancellationTokenSource(InvokerMenu.MoveTimeout).Token);
            var inRange = this.me.MoveToTargetAsync(target, engageRange, moveCt.Token);

            // couldn't reach the target
            if (await inRange == false)
            {
                throw new OperationCanceledException();
            }
        }
    }
}