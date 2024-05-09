using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using ItsStardewTime.Framework;
using StardewModdingAPI;
using StardewValley.Locations;

namespace ItsStardewTime.Patches
{
    internal class SkullCavernJumpPatches
    {
        internal static void Initialize(Harmony harmony)
        {
            harmony.Patch
            (
                original: AccessTools.Method(typeof(MineShaft), nameof(MineShaft.enterMineShaft)),
                transpiler: new HarmonyMethod(typeof(SkullCavernJumpPatches), nameof(EnterMineShaft_Transpile))
            );
        }

        internal static IEnumerable<CodeInstruction> EnterMineShaft_Transpile(IEnumerable<CodeInstruction> instructions)
        {
            IEnumerable<CodeInstruction> enter_mine_shaft_transpile = instructions.ToList();
            try
            {
                var inst_list = enter_mine_shaft_transpile.ToList();
                bool patched = false;
                MethodInfo math_max_method = AccessTools.Method
                (
                    typeof(Math),
                    nameof(Math.Max),
                    new[] { typeof(int), typeof(int) }
                );
                MethodInfo patched_math_max_method = AccessTools.Method
                (
                    typeof(SkullCavernJumpPatches),
                    nameof(Max)
                );
                for (int i = 6; i < inst_list.Count - 6; ++i)
                {
                    if (inst_list[i].Calls(math_max_method))
                    {
                        inst_list[i] = new CodeInstruction(OpCodes.Call, patched_math_max_method);

                        patched = true;
                        break;
                    }
                }

                if (!patched)
                {
                    TimeController.Monitor.Log($"Failed to patch MineShaft.", LogLevel.Debug);
                }

                return inst_list;
            }
            catch (Exception ex)
            {
                TimeController.Monitor.Log($"Failed to apply patch:\n{ex}", LogLevel.Error);
                return enter_mine_shaft_transpile;
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