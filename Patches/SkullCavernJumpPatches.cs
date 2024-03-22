using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using StardewModdingAPI;

namespace ItsStardewTime.Patches
{
    internal class SkullCavernJumpPatches
    {
#nullable disable
        private static IMonitor Monitor;
#nullable enable

        internal static void Initialize(IMonitor monitor)
        {
            Monitor = monitor;
        }

        internal static IEnumerable<CodeInstruction> EnterMineShaft_Transpile(IEnumerable<CodeInstruction> instructions)
        {
            try
            {
                var instList = instructions.ToList();
                bool patched = false;
                MethodInfo mathMaxMethod = AccessTools.Method(typeof(Math), nameof(Math.Max), new[] { typeof(int), typeof(int) });
                MethodInfo patchedMathMaxMethod = AccessTools.Method(typeof(SkullCavernJumpPatches), nameof(SkullCavernJumpPatches.Max));
                for (int i = 6; i < instList.Count - 6; ++i)
                {
                    if (instList[i].Calls(mathMaxMethod))
                    {
                        instList[i] = new CodeInstruction(OpCodes.Call, patchedMathMaxMethod);

                        patched = true;
                        break;
                    }
                }

                if (!patched)
                {
                    Monitor.Log($"Failed to patch MineShaft.", LogLevel.Debug);
                }

                return instList;
            }
            catch (Exception ex)
            {
                Monitor.Log($"Failed to apply patch:\n{ex}", LogLevel.Error);
                return instructions;
            }
        }

        private static int Max(int a, int b)
        {
            int result = Math.Max(a, b);
            TimeMaster.TimeController.UpdateHealthLock(result);

            return result;
        }
    }
}
