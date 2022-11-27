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

        public bool UpscalerEnabled = false;

        public bool IsActive() => UpscalerEnabled;

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

        public UpscaleProfile qualityLevel = UpscaleProfile.MaxPerformance;

        public struct UpscaleParams
        {
            public int id;
            public IntPtr color;
            public IntPtr motionVector;
            public IntPtr depth;
            public IntPtr mask;
            public IntPtr destination;
            public float renderSizeX;
            public float renderSizeY;
            public float sharpness;
            public float jitterOffsetX;
            public float jitterOffsetY;
            public float motionScaleX;
            public float motionScaleY;
            public bool reset;
            public float nearPlane;
            public float farPlane;
            public float veticalFOV;
            public bool execute;
        }

        public void Setup()
        {
            upscaleMethod = ModConfig.UpscaleMethod.Value;
            qualityLevel = ModConfig.UpscaleProfile.Value;
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
                UpscalerEnabled = false;
                return;
            }

            if (targetCamera == null)
                targetCamera = Camera.main;
            targetCamera.depthTextureMode = DepthTextureMode.Depth | DepthTextureMode.MotionVectors;
            var format = HDRenderPipeline.currentPipeline.currentPlatformRenderPipelineSettings.colorBufferFormat == ColorBufferFormat.R16G16B16A16
                ? DXGI_FORMAT.DXGI_FORMAT_R16G16B16A16_FLOAT
                : DXGI_FORMAT.DXGI_FORMAT_R11G11B10_FLOAT;
            var um = (upscaleMethod == UpscaleMethod.DLAA) ? UpscaleMethod.DLSS : upscaleMethod;
            var texPtr = SimpleInit(id, um, qualityLevel, displayWidth, displayHeight, false, true, true, false, (Sharpness != 0), true, format);
            OutColor = Texture2D.CreateExternalTexture(displayWidth, displayHeight, TextureFormat.RGBA64, false, false, texPtr);
            if(upscaleMethod == UpscaleMethod.DLAA)
            {
                renderWidth = displayWidth;
                renderHeight = displayHeight;
                renderScale = 1.0f;
            }
            else
            {
                renderWidth = GetRenderWidth(id);
                renderHeight = GetRenderHeight(id);
                renderScale = (float)renderWidth / Screen.width;
            }
            if (Sharpness < -1f)
                Sharpness = GetOptimalSharpness(id);

            upscaleParams = new UpscaleParams { };

            Debug.Log("Deep Learning Upsacling Setup!!");
            RenderPipelineManager.endCameraRendering -= new Action<ScriptableRenderContext, Camera>(RenderPipelineManager_endCameraRendering);
            RenderPipelineManager.endCameraRendering += new Action<ScriptableRenderContext, Camera>(RenderPipelineManager_endCameraRendering);

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
                upscaleParams.color = source.rt.GetNativeTexturePtr();
                upscaleParams.motionVector = motionVectorTex.GetNativeTexturePtr();
                upscaleParams.depth = depthTex.GetNativeTexturePtr();
                upscaleParams.destination = destination.rt.GetNativeTexturePtr();
                upscaleParams.renderSizeX = renderWidth;
                upscaleParams.renderSizeY = renderHeight;
                upscaleParams.sharpness = Sharpness;
                upscaleParams.jitterOffsetX = -camera.taaJitter.x;
                upscaleParams.jitterOffsetY = -camera.taaJitter.y;
                upscaleParams.motionScaleX = -renderWidth;
                upscaleParams.motionScaleY = -renderHeight;
                upscaleParams.reset = camera.isFirstFrame;
                upscaleParams.nearPlane = camera.camera.nearClipPlane;
                upscaleParams.farPlane = camera.camera.farClipPlane;
                upscaleParams.veticalFOV = camera.camera.fieldOfView * Mathf.Deg2Rad;
                upscaleParams.execute = true;
                SetupUpscaleParams(upscaleParams.id, upscaleParams.color, upscaleParams.motionVector, upscaleParams.depth, IntPtr.Zero, upscaleParams.destination, upscaleParams.renderSizeX, upscaleParams.renderSizeY, upscaleParams.sharpness,
                    upscaleParams.jitterOffsetX, upscaleParams.jitterOffsetY, upscaleParams.motionScaleX, upscaleParams.motionScaleY, upscaleParams.reset, upscaleParams.nearPlane, upscaleParams.farPlane, upscaleParams.veticalFOV, upscaleParams.execute);
                cmd.IssuePluginEvent(GetEvaluateFunc(), upscaleParams.id);
                //cmd.Blit(OutColor, destination);
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
            ToggleUpscaleFeature(!UpscalerEnabled);
        }

        public void ToggleUpscaleFeature(bool enable)
        {
            if (enable)
            {
                MelonCoroutines.Start(DelayEnableUpscale(0.1f));
            }
            else
            {
                UpscalerEnabled = false;
                RefreshMipmapBias();
            }
        }

        public void SwitchUpscaleMethod(UpscaleMethod method)
        {
            ModConfig.UpscaleMethod.Value = method;
            ModConfig.Save();
            ToggleUpscaleFeature(false);
            upscaleMethod = method;
            MelonCoroutines.Start(DelayEnableUpscale(0.1f));
        }

        public void SwitchProfile(bool up)
        {
            ToggleUpscaleFeature(false);
            switch (qualityLevel)
            {
                case UpscaleProfile.UltraPerformance:
                    qualityLevel = (up) ? UpscaleProfile.MaxPerformance : UpscaleProfile.UltraPerformance;
                    break;
                case UpscaleProfile.MaxPerformance:
                    qualityLevel = (up) ? UpscaleProfile.Balanced : UpscaleProfile.UltraPerformance;
                    break;
                case UpscaleProfile.Balanced:
                    qualityLevel = (up) ? UpscaleProfile.MaxQuality : UpscaleProfile.MaxPerformance;
                    break;
                case UpscaleProfile.MaxQuality:
                    qualityLevel = (up) ? UpscaleProfile.MaxQuality : UpscaleProfile.Balanced;
                    break;
            }
            ModConfig.UpscaleProfile.Value = qualityLevel;
            ModConfig.Save();
            ToggleUpscaleFeature(true);
        }
        private IEnumerator DelayEnableUpscale(float seconds)
        {
            yield return new WaitForSeconds(seconds);
            Setup();
            yield return new WaitForSeconds(seconds);
            UpscalerEnabled = true;
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
            var mipbias = (UpscalerEnabled)?GetOptimalMipmapBias(id) : 0;
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
        private static extern IntPtr SimpleInit(int id, UpscaleMethod upscaleType, UpscaleProfile qualityLevel, int displaySizeX, int displaySizeY, bool isContentHDR, bool depthInverted, bool YAxisInverted,
            bool motionVetorsJittered, bool enableSharpening, bool enableAutoExposure, DXGI_FORMAT format = DXGI_FORMAT.DXGI_FORMAT_R16G16B16A16_FLOAT);

        [DllImport("PDPerfPlugin")]
        private static extern void SimpleEvaluate(int id, IntPtr color, IntPtr motionVector, IntPtr depth, IntPtr mask, IntPtr destination, int renderSizeX, int renderSizeY, float sharpness,
            float jitterOffsetX, float jitterOffsetY, int motionScaleX, int motionScaleY, bool reset, float nearPlane, float farPlane, float verticalFOV, bool execute = true);

        [DllImport("PDPerfPlugin")]
        private static extern int GetRenderWidth(int id = 1);
        [DllImport("PDPerfPlugin")]
        private static extern int GetRenderHeight(int id = 1);
        [DllImport("PDPerfPlugin")]
        private static extern float GetOptimalSharpness(int id = 1);
        [DllImport("PDPerfPlugin")]
        public static extern float GetOptimalMipmapBias(int id = 1);

        [DllImport("PDPerfPlugin")]
        private static extern IntPtr GetEvaluateWithDataFunc();

        [DllImport("PDPerfPlugin")]
        private static extern IntPtr GetEvaluateFunc();

        [DllImport("PDPerfPlugin")]
        private static extern void SetupUpscaleParams(int id, IntPtr color, IntPtr motionVector, IntPtr depth, IntPtr mask, IntPtr destination, float renderSizeX, float renderSizeY, float sharpness,
            float jitterOffsetX, float jitterOffsetY, float motionScaleX, float motionScaleY, bool reset, float nearPlane, float farPlane, float verticalFOV, bool execute = true);

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
        public static extern void InitLogDelegate(LogDelegate log);

        public static void LogMessageFromCpp(IntPtr message, int iSize)
        {
            Debug.Log(Marshal.PtrToStringAnsi(message, iSize));
        }
        public static void ShowLog()
        {
            InitLogDelegate(LogMessageFromCpp);
        }

    }
}
