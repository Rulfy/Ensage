namespace InvokerReborn.SequenceHelpers
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;

    using Ensage;
    using Ensage.Common.Threading;

    using InvokerReborn.Interfaces;

    public class AwaitModifier : ISequenceEntry
    {
        private readonly string modifierName;

        private readonly int timeout;

        public AwaitModifier(string modifierName, int timeout)
        {
            this.modifierName = modifierName;
            this.timeout = timeout;
        }

        public async Task ExecuteAsync(Unit target, CancellationToken tk = default(CancellationToken))
        {
            var timeoutTk = CancellationTokenSource.CreateLinkedTokenSource(
                tk,
                new CancellationTokenSource(this.timeout).Token);
            var hasModifier = await target.WaitModifierAsync(this.modifierName, timeoutTk.Token);
            if (!hasModifier)
            {
                throw new OperationCanceledException();
            }
        }
    }
}