using System.Threading.Tasks;

namespace Zaio.Helpers
{
    public static class MyAsyncHelpers
    {
        public static async Task AsyncSleep()
        {
            await Task.Delay(500);
        }
    }
}