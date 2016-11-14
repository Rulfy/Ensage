using System;
using System.Threading;
using System.Threading.Tasks;
using Ensage;
using Ensage.Common.Threading;
using InvokerReborn.Interfaces;

namespace InvokerReborn.SequenceHelpers
{
    public class AwaitModifier : ISequenceEntry
    {
        private readonly string _modifierName;
        private readonly int _timeout;

        public AwaitModifier(string modifierName, int timeout)
        {
            _modifierName = modifierName;
            _timeout = timeout;
        }

        public async Task ExecuteAsync(Unit target, CancellationToken tk = default(CancellationToken))
        {
            var timeoutTk = CancellationTokenSource.CreateLinkedTokenSource(tk,
                new CancellationTokenSource(_timeout).Token);
            var hasModifier = await target.WaitModifierAsync(_modifierName, timeoutTk.Token);
            if (!hasModifier)
                throw new OperationCanceledException();
        }
    }
}