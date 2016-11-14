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

namespace InvokerReborn.SequenceHelpers
{
    internal class AwaitMoveToTarget : ISequenceEntry
    {
        private static readonly ILog Log = AssemblyLogs.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private readonly Func<int> _engageRange;
        private readonly Hero _me;


        public AwaitMoveToTarget(Hero me, Func<int> engageRange)
        {
            _me = me;
            _engageRange = engageRange;
        }

        public int EngageRange => _engageRange();

        public async Task ExecuteAsync(Unit target, CancellationToken tk = default(CancellationToken))
        {
            await ExecuteAsync(target.NetworkPosition, _engageRange(), tk);
        }

        public async Task ExecuteAsync(Vector3 target, int engageRange,
            CancellationToken tk = default(CancellationToken))
        {
            if (_me.Distance2D(target) <= engageRange)
                return;

            Log.Debug($"Execute AwaitMoveToTarget {target} | {engageRange}");
            // try to reach the target in max 5 seconds
            var moveCt = CancellationTokenSource.CreateLinkedTokenSource(tk,
                new CancellationTokenSource(InvokerMenu.MoveTimeout).Token);
            var inRange = _me.MoveToTargetAsync(target, engageRange, moveCt.Token);

            // couldn't reach the target
            if (await inRange == false)
                throw new OperationCanceledException();
        }
    }
}