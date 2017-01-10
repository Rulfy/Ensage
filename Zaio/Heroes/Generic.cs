using System.Threading;
using System.Threading.Tasks;
using Ensage;
using Zaio.Interfaces;

namespace Zaio.Heroes
{
    // Pseudo class used for all heroes which are not officially supported
    internal class Generic : ComboHero
    {
        public override async Task ExecuteComboAsync(Unit target, CancellationToken tk = new CancellationToken())
        {
            await UseItems(tk);

            // make him disabled
            DisableEnemy(tk);
        }
    }
}