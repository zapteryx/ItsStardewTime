using HarmonyLib;
using ItsStardewTime.Framework;
using StardewModdingAPI;
using StardewValley;

namespace ItsStardewTime.Patches.TimeDisplayPatches;

internal class TimeOfDayStringPatches
{
        internal static void Initialize
        (
            in Harmony harmony
        )
        {
            harmony.Patch
            (
                original: AccessTools.Method(typeof(Game1), nameof(Game1.getTimeOfDayString)),
                postfix: new HarmonyMethod(typeof(TimeOfDayStringPatches), nameof(GetTimeOfDayString_Postfix))
            );
        }

        /// <summary>
        /// Postfix for the Game1.getTimeOfDayString method.
        /// This method handles creating the display time string based on the current game time. We modify the time
        /// passed into this method with the prefix, and then we modify the string that is returned here.
        /// The original method, supplied with our updated time, correctly handles localization and formatting,
        /// but needs modification based on the configuration settings to update with minutes and 24 hour time.
        /// </summary>
        /// <param name="time"></param>
        /// <param name="__result"></param>
        internal static void GetTimeOfDayString_Postfix(int time, ref string __result)
        {
            try
            {
                // Impossible to correctly account for a modded in language time string.
                if (LocalizedContentManager.CurrentLanguageCode == LocalizedContentManager.LanguageCode.mod)
                {
                    return;
                }

                ClockHelper.Minutes.Handle(time, ref __result);
                ClockHelper.Hours.Handle(time, ref __result);
            }
            catch (Exception ex)
            {
                TimeController.Monitor.Log($"Failed to run patch:\n{ex}", LogLevel.Error);
            }
        }
    }
