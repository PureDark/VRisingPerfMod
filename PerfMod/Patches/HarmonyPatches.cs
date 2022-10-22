using HarmonyLib;
using UnityEngine.SceneManagement;

namespace PureDark.VRising.PerfMod.Patches
{
    internal static class HarmonyPatches
    {
        public static Harmony Instance { get; set; }

        public static void PatchAll()
        {
            if(Instance == null)
                Instance = new Harmony("com.PureDark.VRising.PrefMod");
            Instance.PatchAll();
            Instance.Patch(
                typeof(UnhollowerBaseLib.LogSupport).GetMethod("Warning", AccessTools.all),
                new HarmonyMethod(typeof(HarmonyPatches).GetMethod(nameof(HarmonyPatches.UnhollowerWarningPrefix))));
        }

        public static bool UnhollowerWarningPrefix(string __0) => !__0.Contains("unsupported return type") && !__0.Contains("unsupported parameter");

    }
}
