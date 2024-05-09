using HarmonyLib;
using ItsStardewTime.Framework;
using StardewModdingAPI;
using StardewValley;

namespace ItsStardewTime.Patches
{
    internal class FixWarpToFestivalBugPatches
    {
        internal static void Initialize(Harmony harmony)
        {
            harmony.Patch
            (
                original: AccessTools.Method
                (
                    typeof(Game1),
                    nameof(Game1.warpFarmer),
                    new[] { typeof(LocationRequest), typeof(int), typeof(int), typeof(int) }
                ),
                prefix: new HarmonyMethod
                (
                    typeof(FixWarpToFestivalBugPatches),
                    nameof(WarpFarmer_Prefix)
                )
            );
        }

        internal static bool WarpFarmer_Prefix()
        {
            try
            {
                if (Game1.weatherIcon == 1)
                {
                    Game1.whereIsTodaysFest ??= Game1.temporaryContent.Load<Dictionary<string, string>>
                        (
                            "Data\\Festivals\\" + Game1.currentSeason + Game1.dayOfMonth
                        )["conditions"].Split('/')[0];
                }

                return true;
            }
            catch (Exception ex)
            {
                TimeController.Monitor.Log($"Failed to run patch:\n{ex}", LogLevel.Error);
                return true;
            }
        }
    }
}