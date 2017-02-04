using System.Threading.Tasks;

namespace Zaio.Helpers
{
    public static class AsyncHelpers
    {
        public static async Task AsyncSleep()
        {
            await Task.Delay(500);
        }
    }
}