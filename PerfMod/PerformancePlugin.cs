using BepInEx;
using HarmonyLib;
using BepInEx.Logging;
using UnityEngine;
using UnityEngine.SceneManagement;
using System;
using PureDark.VRising.PerfMod.Upscale;
using PureDark.VRising.PerfMod.Patches;
using PureDark.VRising.PerfMod.Settings;
using PureDark.VRising.PerfMod.MISC;
using System.Runtime.InteropServices;
using System.IO;
using System.Reflection;
using System.Security;
using BepInEx.Unity.IL2CPP;
using Il2CppInterop.Runtime.Injection;

namespace PureDark.VRising.PerfMod
{
    [BepInPlugin(GUID, NAME, VERSION)]
    public class PerformancePlugin : BasePlugin
    {
        public const string GUID = "PureDark.VRising.PerfMod";
        public const string NAME = "VRisingPerformanceMod";
        public const string AUTHOR = "PureDark/暗暗十分/突破天际的金闪闪";
        public const string VERSION = "1.1.0";

        public static PerformancePlugin Instance { get; private set; }

        internal static Harmony Harmony { get; } = new Harmony(GUID);

        public override void Load()
        {
            Instance = this;
            Log.Setup(base.Log);

            Init();
        }

        private void Init()
        {
            SetupIL2CPPClassInjections();
            HarmonyPatches.PatchAll();
            ModConfig.Init();
            SceneManager.sceneLoaded += new Action<Scene, LoadSceneMode>(OnSceneLoaded);
            LoadAllDlls();
        }


        public void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (!UpscaleManager.instance)
            {
                var DLSSRoot = new GameObject("DLSSGlobals");
                DLSSRoot.AddComponent<UpscaleManager>();
                DLSSRoot.AddComponent<MelonCoroutineCallbacks>();
            }
        }

        private void SetupIL2CPPClassInjections()
        {
            ClassInjector.RegisterTypeInIl2Cpp<UpscaleManager>();
            ClassInjector.RegisterTypeInIl2Cpp<MelonCoroutineCallbacks>();
        }

        public static void LoadAllDlls()
        {
            SetUnmanagedDllDirectory();
            var result = LoadLibrary("nvngx_dlss.dll");
            Log.Debug("Load nvngx_dlss.dll result: " + result);
            result = LoadLibrary("ffx_fsr2_api_x64.dll");
            Log.Debug("Load ffx_fsr2_api_x64.dll result: " + result);
            result = LoadLibrary("ffx_fsr2_api_dx12_x64.dll");
            Log.Debug("Load ffx_fsr2_api_dx12_x64.dll result: " + result);
            result = LoadLibrary("dxil.dll");
            Log.Debug("Load dxil.dll result: " + result);
            result = LoadLibrary("dxcompiler.dll");
            Log.Debug("Load dxcompiler.dll result: " + result);
            result = LoadLibrary("XeFX.dll");
            Log.Debug("Load XeFX.dll result: " + result);
            result = LoadLibrary("XeFX_Loader.dll");
            Log.Debug("Load XeFX_Loader.dll result: " + result);
            result = LoadLibrary("libxess.dll");
            Log.Debug("Load libxess.dll result: " + result);
            result = LoadLibrary("PDPerfPlugin.dll");
            Log.Debug("Load PDPerfPlugin.dll result: " + result);
            if ((int)result == 0)
                Log.Error("Win32 ErrorInfo: " + Marshal.GetLastWin32Error());
        }

        public static void SetUnmanagedDllDirectory()
        {
            string path = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            Log.Info("SetUnmanagedDllDirectory: " + path);
            if (!SetDllDirectory(path)) throw new System.ComponentModel.Win32Exception();
        }

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern bool SetDllDirectory(string path);

        [SuppressUnmanagedCodeSecurity]
        [DllImport("Kernel32.dll", EntryPoint = "LoadLibrary", CallingConvention = CallingConvention.Winapi)]
        public static extern IntPtr LoadLibrary(string lpFileName);


        public static new class Log
        {
            static ManualLogSource log;

            public static void Setup(ManualLogSource log)
            {
                Log.log = log;
            }

            public static void Warning(string msg)
            {
                log.LogWarning(msg);
            }

            public static void Error(string msg)
            {
                log.LogError(msg);
            }

            public static void Info(string msg)
            {
                log.LogInfo(msg);
            }

            public static void Debug(string msg)
            {
                log.LogDebug(msg);
            }
        }
    }
}