using StardewModdingAPI;
using StardewValley;

namespace ItsStardewTime.Patches
{
    internal class FixWarpToFestivalBugPatches
    {
#nullable disable
        private static IMonitor Monitor;
#nullable enable

        internal static void Initialize(IMonitor monitor)
        {
            Monitor = monitor;
        }

        internal static bool WarpFarmer_Prefix()
        {
            try
            {
                if (Game1.weatherIcon == 1)
                {
                    Game1.whereIsTodaysFest ??= Game1.temporaryContent.Load<Dictionary<string, string>>("Data\\Festivals\\" + Game1.currentSeason + Game1.dayOfMonth)["conditions"].Split('/')[0];
                }

                return true;
            }
            catch (Exception ex)
            {
                Monitor.Log($"Failed to run patch:\n{ex}", LogLevel.Error);
                return true;
            }
        }
    }
}
