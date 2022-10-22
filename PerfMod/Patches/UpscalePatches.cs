using HarmonyLib;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;
using PureDark.VRising.PerfMod.Upscale;
using System;
using static UnityEngine.Rendering.HighDefinition.PostProcessSystem;

namespace PureDark.VRising.PerfMod.Patches
{
    [HarmonyPatch(typeof(CustomVignette))]
    internal class InjectCustomVignette
    {
        public static bool isActive;

        [HarmonyPostfix]
        [HarmonyPatch(nameof(CustomVignette.IsActive))]
        private static void IsActive(ref bool __result)
        {
            isActive = __result;
            __result |= UpscaleManager.IsUpscaleEnabled();
        }

        [HarmonyPrefix]
        [HarmonyPatch(nameof(CustomVignette.Render))]
        private static bool RenderPrefix()
        {
            return isActive;
        }

        [HarmonyPostfix]
        [HarmonyPatch(nameof(CustomVignette.Render))]
        private static void RenderPostFix(CommandBuffer cmd, HDCamera camera, RTHandle source, RTHandle destination)
        {
            if (UpscaleManager.IsUpscaleEnabled())
            {
                UpscaleManager.instance.UpscaleFlat.Render(cmd, camera, source, destination);
            }
        }

        [HarmonyPostfix]
        [HarmonyPatch(nameof(CustomVignette.Cleanup))]
        private static void Cleanup()
        {
            if (UpscaleManager.IsUpscaleEnabled())
            {
                UpscaleManager.instance.UpscaleFlat.Cleanup();
            }
        }
    }

    [HarmonyPatch(typeof(DynamicResolutionHandler), nameof(DynamicResolutionHandler.DynamicResolutionEnabled))]
    internal class InjectDynamicResolutionHandlerDynamicResolutionEnabled
    {
        private static bool Prefix(ref bool __result)
        {
            __result = false;
            return false;
        }
    }

    [HarmonyPatch(typeof(HDCamera))]
    internal class InjectHDCamera
    {
        [HarmonyPrefix]
        [HarmonyPatch(nameof(HDCamera.UpdateAllViewConstants), new Type[] { typeof(bool), typeof(bool)})]
        private static bool UpdateAllViewConstantsPrefix(HDCamera __instance, ref bool jitterProjectionMatrix)
        {
            if (UpscaleManager.IsUpscaleEnabled())
            {
                jitterProjectionMatrix = true;
                //用自定义的frameIndex代替内置的，以实现更高数量的jitter样本
                __instance.taaFrameIndex = UpscaleManager.GetJitterIndex();
            }
            return true;
        }

        [HarmonyPrefix]
        [HarmonyPatch(nameof(HDCamera.UpdateAntialiasing))]
        private static bool UpdateAntialiasing()
        {
            //DLSS开启时跳过，这个方法会每帧重置TAAJitter的frameIndex
            return !UpscaleManager.IsUpscaleEnabled();
        }
    }
    
    [HarmonyPatch(typeof(SkyManager), nameof(SkyManager.IsLightingSkyValid))]
    internal class UpdateHDRenderPipelineUpdateShaderVariablesGlobalLightLoop
    {
        private static bool Prefix(ref bool __result)
        {
            //不注入这里会崩溃
            if (UpscaleFlat.IsRendering)
            {
                __result = false;
                return false;
            }
            return true;
        }
    }
}
