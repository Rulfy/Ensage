namespace InvokerReborn.Interfaces
{
    using System.Threading;
    using System.Threading.Tasks;

    using Ensage;

    public interface ISequenceEntry
    {
        Task ExecuteAsync(Unit target, CancellationToken tk = default(CancellationToken));
    }
}