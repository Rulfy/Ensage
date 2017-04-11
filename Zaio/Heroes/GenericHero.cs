using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Ensage;
using Ensage.Common.Threading;
using log4net;
using PlaySharp.Toolkit.Logging;
using Zaio.Interfaces;
using AbilityId = Ensage.AbilityId;

namespace Zaio.Heroes
{
    // Pseudo class used for all heroes which are not officially supported
    internal class GenericHero : ComboHero
    {
        private static readonly ILog Log = AssemblyLogs.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        public override async Task ExecuteComboAsync(Unit target, CancellationToken tk = new CancellationToken())
        {
            await UseItems(target, tk);

            // make him disabled
            await DisableEnemy(target, tk);

            if (ZaioMenu.ShouldUseOrbwalker)
            {
                Orbwalk();
                Log.Debug($"orbwalking");
            }
            else
            {
                MyHero.Attack(target);
                await Await.Delay(125, tk);
            }
        }
    }
}