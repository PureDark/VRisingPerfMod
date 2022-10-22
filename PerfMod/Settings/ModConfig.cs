using BepInEx.Configuration;
using BepInEx;
using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using PureDark.VRising.PerfMod.Upscale;

namespace PureDark.VRising.PerfMod.Settings
{
    public static class ModConfig
    {
        private const string CONFIG_FILE_NAME = "PureDark.VRising.PerfMod.cfg";

        private static readonly ConfigFile configFile = new ConfigFile(System.IO.Path.Combine(Paths.ConfigPath, CONFIG_FILE_NAME), true);
        internal static ConfigEntry<UpscaleMethod> UpscaleMethod { get; private set; }
        internal static ConfigEntry<UpscaleProfile> UpscaleProfile { get; private set; }
        internal static ConfigEntry<float> Sharpness { get; private set; }

        internal static void Init()
        {
            UpscaleMethod = configFile.Bind<UpscaleMethod>(
                "Upscale Settings",
                "Upscale Method",
                Upscale.UpscaleMethod.FSR2,
                "Which upscaling method you would like to use."
            );
            UpscaleProfile = configFile.Bind<UpscaleProfile>(
                "Upscale Settings",
                "Upscale Optimal Settings",
                Upscale.UpscaleProfile.Balanced,
                "Which setting you would like to use for Upscaling."
            );
            Sharpness = configFile.Bind<float>(
                "Upscale Settings",
                "Sharpness",
                1f,
                "The sharpness value to use when Upscaling is on, setting it = 0 to disable sharpening."
            );
        }

        internal static void Save()
        {
            configFile.Save();
        }
    }
}
