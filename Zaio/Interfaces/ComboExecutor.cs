using System.Threading;
using System.Threading.Tasks;
using Ensage;

namespace Zaio.Interfaces
{
    internal interface IComboExecutor
    {
        Task ExecuteComboAsync(Unit target, CancellationToken tk = default(CancellationToken));
    }
}