using HarmonyLib;
using ItsStardewTime.Patches.TimeDisplayPatches;

namespace ItsStardewTime.Patches;

internal static class HarmonyInitializer
{
    internal static void Initialize
    (
        in Harmony harmony
    )
    {
        DayTimeMoneyBoxPatches.Initialize(harmony);
        TimeOfDayStringPatches.Initialize(harmony);
        FixWarpToFestivalBugPatches.Initialize(harmony);
        SkullCavernJumpPatches.Initialize(harmony);
    }
}