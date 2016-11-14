using System;
using System.Threading;
using System.Threading.Tasks;
using Ensage;
using Ensage.Common.Extensions;
using InvokerReborn.Interfaces;

namespace InvokerReborn.SequenceHelpers
{
    class OptionalItemAborter : ISequenceEntry
    {
        private readonly string _itemName;
        private Hero _me;

        public OptionalItemAborter(Hero me, string itemName)
        {
            _itemName = itemName;
            _me = me;
        }

#pragma warning disable CS1998 // Async method lacks 'await' operators and will run synchronously
        public async Task ExecuteAsync(Unit target, CancellationToken tk = default(CancellationToken))
#pragma warning restore CS1998 // Async method lacks 'await' operators and will run synchronously
        {
            if( _me.FindItem(_itemName) == null )
                throw new OperationCanceledException();
        }
    }
}
