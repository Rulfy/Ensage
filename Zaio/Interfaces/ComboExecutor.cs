using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Ensage;

namespace Zaio.Interfaces
{
    interface IComboExecutor
    {
        Task ExecuteComboAsync(Unit target, CancellationToken tk = default(CancellationToken));
    }
}
