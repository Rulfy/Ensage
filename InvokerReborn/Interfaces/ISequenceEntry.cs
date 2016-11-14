using System.Threading;
using System.Threading.Tasks;
using Ensage;

namespace InvokerReborn.Interfaces
{
    public interface ISequenceEntry
    {
        Task ExecuteAsync(Unit target, CancellationToken tk = default(CancellationToken));
    }
}