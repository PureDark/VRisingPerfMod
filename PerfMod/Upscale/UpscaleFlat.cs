using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;
using System;
using System.Runtime.InteropServices;
using UnhollowerRuntimeLib;
using PureDark.VRising.PerfMod.Settings;
using UnhollowerBaseLib.Attributes;
using static UnityEngine.Rendering.DebugUI;
using static UnityEngine.Rendering.HighDefinition.RenderPipelineSettings;
using PureDark.VRising.PerfMod.Patches;
using UnityEngine.Experimental.Rendering;
using System.Collections;
using PureDark.VRising.PerfMod.MISC;
using static PureDark.VRising.PerfMod.PerformancePlugin;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;

namespace PureDark.VRising.PerfMod.Upscale
{
    //[Il2CppImplements(typeof(IPostProcessComponent))]
    public sealed class UpscaleFlat /*: CustomPostProcessVolumeComponent, IPostProcessComponent*/
    {
        public static bool IsRendering = false;

        public static UpscaleMethod upscaleMethod = UpscaleMethod.FSR2;

        private float m_sharpness = 1f;

        public float Sharpness
        {
            get { return m_sharpness; }
            set
            {
                m_sharpness = (float)Math.Round(value, 2);
            }
        }

        public bool DLSSEnabled = false;

        public bool IsActive() => DLSSEnabled;

        //public override CustomPostProcessInjectionPoint injectionPoint => CustomPostProcessInjectionPoint.AfterPostProcess;

        public Camera targetCamera;
        public RenderTexture colorTex;
        public RenderTexture destTex;
        public Texture depthTex;
        public Texture motionVectorTex;
        public Texture2D OutColor;

        public int renderWidth, renderHeight;
        public int displayWidth, displayHeight;

        public int maxJitterCount = 64;

        public int id = 1;

        private float renderScale;

        private Vector4 screenSize;

        private UpscaleParams upscaleParams;

        public UpscaleProfile currentProfile = UpscaleProfile.MaxPerformance;

        public struct UpscaleParams
        {
            public int id;
            public IntPtr color;
            public IntPtr motionVector;
            public IntPtr depth;
            public IntPtr destination;
            public float sharpness;
            public float jitterOffsetX;
            public float jitterOffsetY;
            public bool reset;
            public float nearPlane;
            public float farPlane;
            public float veticalFOV;
        }

        public void Setup()
        {
            upscaleMethod = ModConfig.UpscaleMethod.Value;
            currentProfile = ModConfig.UpscaleProfile.Value;
            Sharpness = ModConfig.Sharpness.Value;
            displayWidth = Screen.width;
            displayHeight = Screen.height;

            targetCamera = Camera.main;
            targetCamera.depthTextureMode = DepthTextureMode.Depth | DepthTextureMode.MotionVectors;

            ShowLog();

            //可能是因为IL2CPP的原因，插件的UnityPluginLoad不会被调用，需要通过传递一个底层纹理的方法获得ID3D11Device
            //所有游戏都选择使用自行获取device的方法，不依赖unity对插件的调用了
            var rt = new Texture2D(1, 1, TextureFormat.ARGB32, false, false);
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Direct3D11)
                SetupGraphicDevice(rt.GetNativeTexturePtr(), GraphicsAPI.D3D11);
            else
            {
                DLSSEnabled = false;
                return;
            }

            if (targetCamera == null)
                targetCamera = Camera.main;
            targetCamera.depthTextureMode = DepthTextureMode.Depth | DepthTextureMode.MotionVectors;
            var format = HDRenderPipeline.currentPipeline.currentPlatformRenderPipelineSettings.colorBufferFormat == ColorBufferFormat.R16G16B16A16
                ? DXGI_FORMAT.DXGI_FORMAT_R16G16B16A16_FLOAT
                : DXGI_FORMAT.DXGI_FORMAT_R11G11B10_FLOAT;
            var texPtr = InitUpscaleFeature(id, upscaleMethod, currentProfile, displayWidth, displayHeight, false, true, true, false, true, true, format);
            OutColor = Texture2D.CreateExternalTexture(displayWidth, displayHeight, TextureFormat.RGBA64, false, false, texPtr);

            renderWidth = GetRenderWidth(id);
            renderHeight = GetRenderHeight(id);
            if (Sharpness < -1f)
                Sharpness = GetOptimalSharpness(id);

            upscaleParams = new UpscaleParams { };

            Debug.Log("Deep Learning Upsacling Setup!!");
            RenderPipelineManager.endCameraRendering -= new Action<ScriptableRenderContext, Camera>(RenderPipelineManager_endCameraRendering);
            RenderPipelineManager.endCameraRendering += new Action<ScriptableRenderContext, Camera>(RenderPipelineManager_endCameraRendering);

            renderScale = (float)renderWidth / Screen.width;
            maxJitterCount = 8 * ((int)(1.0f / renderScale) ^ 2);
            if (maxJitterCount == 0)
                maxJitterCount = 64;

            screenSize = new Vector4(displayWidth, displayHeight, 1.0f / displayWidth, 1.0f / displayHeight);

            var scaler = DelegateSupport.ConvertDelegate<PerformDynamicRes>((Func<float>)getScale);
            DynamicResolutionHandler.SetDynamicResScaler(scaler, DynamicResScalePolicyType.ReturnsPercentage);
        }

        private float getScale()
        {
            return (IsActive()) ? renderScale * 100f : 100f;
        }

        public void Render(CommandBuffer cmd, HDCamera camera, RTHandle source, RTHandle destination)
        {
            if (!IsActive())
                return;
            targetCamera = Camera.main;
            if (camera.camera != targetCamera || lastFrame == Time.frameCount)
                return;
            IsRendering = true;
            colorTex = InjectCustomVignette.isActive ? destination.rt : source.rt;
            destTex = destination.rt;
            depthTex = Shader.GetGlobalTexture("_CameraDepthTexture");
            motionVectorTex = Shader.GetGlobalTexture("_CameraMotionVectorsTexture");
            if (motionVectorTex != null)
            {
                upscaleParams.id = id;
                upscaleParams.color = colorTex.GetNativeTexturePtr();
                upscaleParams.motionVector = motionVectorTex.GetNativeTexturePtr();
                upscaleParams.depth = depthTex.GetNativeTexturePtr();
                upscaleParams.destination = destination.rt.GetNativeTexturePtr();
                upscaleParams.sharpness = Sharpness;
                upscaleParams.jitterOffsetX = -camera.taaJitter.x;
                upscaleParams.jitterOffsetY = -camera.taaJitter.y;
                upscaleParams.reset = camera.isFirstFrame;
                upscaleParams.nearPlane = camera.camera.nearClipPlane;
                upscaleParams.farPlane = camera.camera.farClipPlane;
                upscaleParams.veticalFOV = camera.camera.fieldOfView * Mathf.Deg2Rad;
                SetupUpscaleParams(upscaleParams.id, upscaleParams.color, upscaleParams.motionVector, upscaleParams.depth, upscaleParams.destination, upscaleParams.sharpness,
                    upscaleParams.jitterOffsetX, upscaleParams.jitterOffsetY, upscaleParams.reset, upscaleParams.nearPlane, upscaleParams.farPlane, upscaleParams.veticalFOV);
                cmd.IssuePluginEvent(GetUpscaleFunc(), 0);
                cmd.Blit(OutColor, destination);
                var hdrp = RenderPipelineManager.currentPipeline.Cast<HDRenderPipeline>();
                camera.screenSize = screenSize;
                hdrp.UpdateShaderVariablesGlobalCB(camera, cmd);
            }
        }

        public void Cleanup()
        {
            if (!IsActive())
                return;
        }

        private int lastFrame;

        private void RenderPipelineManager_endCameraRendering(ScriptableRenderContext context, Camera camera)
        {
            IsRendering = false;
            lastFrame = Time.frameCount;
        }

        public int GetJitterIndex()
        {
            return lastFrame % maxJitterCount;
        }

        public void ToggleUpscaleFeature()
        {
            ToggleUpscaleFeature(!DLSSEnabled);
        }

        public void ToggleUpscaleFeature(bool enable)
        {
            if (enable)
            {
                if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Direct3D11)
                {
                    if (upscaleMethod == UpscaleMethod.DLSS)
                    {
                        Setup();
                        DLSSEnabled = true;
                        RefreshMipmapBias();
                    }
                    else
                        MelonCoroutines.Start(DelayEnableUpscale(0.1f));
                }
                else if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Direct3D12)
                    MelonCoroutines.Start(DelayEnableUpscale(0.1f));
            }
            else
            {
                DLSSEnabled = false;
                RefreshMipmapBias();
            }
        }

        public void SwitchUpscaleMethod()
        {
            ToggleUpscaleMethod(upscaleMethod != UpscaleMethod.FSR2);
        }

        public void ToggleUpscaleMethod(bool enable)
        {

            if (enable)
            {
                ModConfig.UpscaleMethod.Value = UpscaleMethod.FSR2;
                ModConfig.Save();
                ToggleUpscaleFeature(false);
                upscaleMethod = UpscaleMethod.FSR2;
                MelonCoroutines.Start(DelayEnableUpscale(0.1f));
            }
            else
            {
                ModConfig.UpscaleMethod.Value = UpscaleMethod.DLSS;
                ModConfig.Save();
                ToggleUpscaleFeature(false);
                upscaleMethod = UpscaleMethod.DLSS;
                if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Direct3D11)
                    ToggleUpscaleFeature(true);
                else if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Direct3D12)
                    MelonCoroutines.Start(DelayEnableUpscale(0.1f));
            }
        }

        public void SwitchProfile(bool up)
        {
            ToggleUpscaleFeature(false);
            switch (currentProfile)
            {
                case UpscaleProfile.UltraPerformance:
                    currentProfile = (up) ? UpscaleProfile.MaxPerformance : UpscaleProfile.UltraPerformance;
                    break;
                case UpscaleProfile.MaxPerformance:
                    currentProfile = (up) ? UpscaleProfile.Balanced : UpscaleProfile.UltraPerformance;
                    break;
                case UpscaleProfile.Balanced:
                    currentProfile = (up) ? UpscaleProfile.MaxQuality : UpscaleProfile.MaxPerformance;
                    break;
                case UpscaleProfile.MaxQuality:
                    currentProfile = (up) ? UpscaleProfile.MaxQuality : UpscaleProfile.Balanced;
                    break;
            }
            ModConfig.UpscaleProfile.Value = currentProfile;
            ModConfig.Save();
            ToggleUpscaleFeature(true);
        }
        private IEnumerator DelayEnableUpscale(float seconds)
        {
            yield return new WaitForSeconds(seconds);
            Setup();
            yield return new WaitForSeconds(seconds);
            DLSSEnabled = true;
            RefreshMipmapBias();
        }

        public void ResetSharpness()
        {
            SetSharpness(0, true);
        }

        public void SetSharpness(float value, bool reset = false)
        {
            Sharpness = (reset) ? GetOptimalSharpness() : value;
            ModConfig.Sharpness.Value = Sharpness;
            ModConfig.Save();
        }

        public float lastTimeSetMipBias = 0;
        public void RefreshMipmapBias()
        {
            var mipbias = (DLSSEnabled)?GetOptimalMipmapBias(id) : 0;
            var list = Resources.FindObjectsOfTypeAll(UnhollowerRuntimeLib.Il2CppType.Of<Texture>());
            foreach (var item in list)
            {
                item.Cast<Texture>().mipMapBias = mipbias;
            }
            lastTimeSetMipBias = Time.realtimeSinceStartup;
        }

        [DllImport("PDPerfPlugin")]
        private static extern bool SetupGraphicDevice(IntPtr tex, GraphicsAPI api = GraphicsAPI.D3D11);
        [DllImport("PDPerfPlugin")]
        private static extern IntPtr InitUpscaleFeature(int id, UpscaleMethod upscaleType, UpscaleProfile mode, int displaySizeX, int displaySizeY, bool isContentHDR, bool depthInverted, bool YAxisInverted, bool motionVetorsJittered,
            bool enableSharpening, bool enableAutoExposure, DXGI_FORMAT format = DXGI_FORMAT.DXGI_FORMAT_R16G16B16A16_FLOAT);

        [DllImport("PDPerfPlugin")]
        private static extern void EvaluateUpscale(int id, IntPtr color, IntPtr depth, IntPtr motionVector, IntPtr destination, float sharpness, float jitterOffsetX, float jitterOffsetY, bool reset, float nearPlane, float farPlane, float verticalFOV);

        [DllImport("PDPerfPlugin")]
        private static extern int GetRenderWidth(int id = 1);
        [DllImport("PDPerfPlugin")]
        private static extern int GetRenderHeight(int id = 1);
        [DllImport("PDPerfPlugin")]
        private static extern float GetOptimalSharpness(int id = 1);
        [DllImport("PDPerfPlugin")]
        public static extern float GetOptimalMipmapBias(int id = 1);

        [DllImport("PDPerfPlugin")]
        private static extern IntPtr GetUpscaleWithDataFunc();

        [DllImport("PDPerfPlugin")]
        private static extern IntPtr GetUpscaleFunc();

        [DllImport("PDPerfPlugin")]
        private static extern void SetupUpscaleParams(int id, IntPtr color, IntPtr motionVector, IntPtr depth, IntPtr destination, float sharpness, float jitterOffsetX, float jitterOffsetY, bool reset, float nearPlane, float farPlane, float verticalFOV);

        [DllImport("PDPerfPlugin")]
        private static extern void ReleaseUpscaleFeature(int id);

        [DllImport("PDPerfPlugin")]
        private static extern void SetDebug(bool debug);

        [DllImport("PDPerfPlugin")]
        private static extern void SetMotionScaleX(int id, float motionScaleX);

        [DllImport("PDPerfPlugin")]
        private static extern void SetMotionScaleY(int id, float motionScaleY);



        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate void LogDelegate(IntPtr message, int iSize);

        [DllImport("PDPerfPlugin", CallingConvention = CallingConvention.Cdecl)]
        public static extern void InitCSharpDelegate(LogDelegate log);

        public static void LogMessageFromCpp(IntPtr message, int iSize)
        {
            Debug.Log(Marshal.PtrToStringAnsi(message, iSize));
        }
        public static void ShowLog()
        {
            InitCSharpDelegate(LogMessageFromCpp);
        }

    }
}
