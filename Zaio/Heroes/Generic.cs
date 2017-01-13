using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Ensage;
using Ensage.Common.Threading;
using log4net;
using PlaySharp.Toolkit.Logging;
using Zaio.Interfaces;

namespace Zaio.Heroes
{
    // Pseudo class used for all heroes which are not officially supported
    internal class Generic : ComboHero
    {
        private static readonly ILog Log = AssemblyLogs.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        public override async Task ExecuteComboAsync(Unit target, CancellationToken tk = new CancellationToken())
        {
            await UseItems(tk);

            // make him disabled
            DisableEnemy(tk);

            if (ZaioMenu.ShouldUseOrbwalker)
            {
                Orbwalk(MyHero.IsRanged ? 450 : 0);
                Log.Debug($"orbwalking");
            }
            else
            {
                MyHero.Attack(Target);
                await Await.Delay(125, tk);
            }
        }
    }
}