using PureDark.VRising.PerfMod.MISC;
using PureDark.VRising.PerfMod.Settings;
using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace PureDark.VRising.PerfMod.Upscale
{
    public class UpscaleManager : MonoBehaviour
    {
        public UpscaleManager(IntPtr value) : base(value) { }

        public static UpscaleManager instance;
        public UpscaleFlat UpscaleFlat;

        private float textDisplayTime = 0;
        private float scale;

        GUIStyle labelFont;
        GUIStyle textFont;

        void Start()
        {
            if (instance)
            {
                enabled = false;
                return;
            }
            instance = this;
            DontDestroyOnLoad(gameObject);

            //HDRenderPipeline.defaultAsset.afterPostProcessCustomPostProcesses.Add("DLSSMod.DLSS.DLSSFlat, DLSSMod, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null");

            UpscaleFlat = new UpscaleFlat();
            UpscaleFlat.upscaleMethod = ModConfig.UpscaleMethod.Value;
            textDisplayTime = 360f;
            scale = ((float)Screen.width) / 2560;
            labelFont = new GUIStyle();
            labelFont.normal.textColor = new Color(1f, 1f, 1f);
            labelFont.fontSize = (int)(40 * scale);
            textFont = new GUIStyle();
            textFont.normal.textColor = new Color(1f, 1f, 1f);
            textFont.fontSize = (int)(20 * scale);
        }

        public void Update()
        {
            bool keyCombination = (ModConfig.CombinationKey.Value != KeyCode.None) ? Input.GetKey(ModConfig.CombinationKey.Value) : true;
            if (keyCombination && Input.GetKeyUp(KeyCode.Keypad1))
            {
                if(IsUpscaleEnabled() && UpscaleFlat.upscaleMethod == UpscaleMethod.DLSS)
                    UpscaleFlat.ToggleUpscaleFeature(false);
                else
                    UpscaleFlat.SwitchUpscaleMethod(UpscaleMethod.DLSS);
                textDisplayTime = 30f;
            }
            if (keyCombination && Input.GetKeyUp(KeyCode.Keypad2))
            {
                if (IsUpscaleEnabled() && UpscaleFlat.upscaleMethod == UpscaleMethod.FSR2)
                    UpscaleFlat.ToggleUpscaleFeature(false);
                else
                    UpscaleFlat.SwitchUpscaleMethod(UpscaleMethod.FSR2);
                textDisplayTime = 30f;
            }
            if (keyCombination && Input.GetKeyUp(KeyCode.Keypad3))
            {
                if (IsUpscaleEnabled() && UpscaleFlat.upscaleMethod == UpscaleMethod.XESS)
                    UpscaleFlat.ToggleUpscaleFeature(false);
                else
                    UpscaleFlat.SwitchUpscaleMethod(UpscaleMethod.XESS);
                textDisplayTime = 30f;
            }
            if (keyCombination && Input.GetKeyUp(KeyCode.Keypad4))
            {
                if (IsUpscaleEnabled() && UpscaleFlat.upscaleMethod == UpscaleMethod.DLAA)
                    UpscaleFlat.ToggleUpscaleFeature(false);
                else
                    UpscaleFlat.SwitchUpscaleMethod(UpscaleMethod.DLAA);
                textDisplayTime = 30f;
            }
            if (IsUpscaleEnabled())
            {
                if (keyCombination && Input.GetKeyUp(KeyCode.UpArrow))
                {
                    UpscaleFlat.SwitchProfile(true);
                    textDisplayTime = 30f;
                }
                if (keyCombination && Input.GetKeyUp(KeyCode.DownArrow))
                {
                    UpscaleFlat.SwitchProfile(false);
                    textDisplayTime = 30f;
                }
                if (keyCombination && Input.GetKeyUp(KeyCode.LeftArrow))
                {
                    UpscaleFlat.SetSharpness(UpscaleFlat.Sharpness - 0.05f);
                    textDisplayTime = 30f;
                }
                if (keyCombination && Input.GetKeyUp(KeyCode.RightArrow))
                {
                    UpscaleFlat.SetSharpness(UpscaleFlat.Sharpness + 0.05f);
                    textDisplayTime = 30f;
                }
                if (keyCombination && Input.GetKeyUp(KeyCode.Keypad0))
                {
                    UpscaleFlat.ResetSharpness();
                    textDisplayTime = 30f;
                }
                if (keyCombination && Input.GetKeyUp(KeyCode.Keypad3))
                {
                    UpscaleFlat.RefreshMipmapBias();
                }
                if (Time.realtimeSinceStartup - UpscaleFlat.lastTimeSetMipBias > 600)
                {
                    UpscaleFlat.RefreshMipmapBias();
                }
            }
        }

        void OnGUI()
        {
            var SSName = "";
            switch (UpscaleFlat.upscaleMethod)
            {
                case UpscaleMethod.DLSS:
                    SSName = "DLSSv2.4";
                    break;
                case UpscaleMethod.FSR2:
                    SSName = "FSRv2.4";
                    break;
                case UpscaleMethod.XESS:
                    SSName = "XeSSv1.0.1";
                    break;
                case UpscaleMethod.DLAA:
                    SSName = "DLAA";
                    break;

            }
            var GAPI = (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Direct3D11) ? "DX11" : "DX12";
            if (textDisplayTime > 0)
            {
                if (IsUpscaleEnabled())
                {
                    GUI.Label(new Rect(textFont.fontSize, Screen.height - textFont.fontSize * 4, textFont.fontSize * 10, textFont.fontSize * 3), SSName + " " + GAPI + " " + UpscaleFlat.qualityLevel.ToString()
                        + "(" + UpscaleFlat.renderWidth + "," + UpscaleFlat.renderHeight + " -> " + Screen.width + "," + Screen.height + ")\r\n"
                        + "CTRL+NumPad1/2/3/4 To Turn Switch Between DLSS/FSR2/XeSS/DLAA || Press Again To Turn It Off.\r\n"
                        + "CTRL+↑/↓ To Change Profile  ||  CTRL+←/→ To Addjust Sharpness(" + UpscaleFlat.Sharpness + ")\r\n", textFont);
                }
                else
                    GUI.Label(new Rect(textFont.fontSize, Screen.height - textFont.fontSize * 4, textFont.fontSize * 10, textFont.fontSize * 3), SSName + " Off\r\n"
                        + "CTRL+NumPad1/2/3/4 To Switch Between DLSS/FSR2/XeSS/DLAA || Press Again To Turn It Off.", textFont);
                textDisplayTime -= Time.deltaTime;

                if (IsUpscaleEnabled())
                    GUI.Label(new Rect(textFont.fontSize, 60, 600, 60), SSName, labelFont);
                else
                    GUI.Label(new Rect(textFont.fontSize, 60, 600, 60), "Upscaling OFF", labelFont);
            }
        }

        public static int GetJitterIndex()
        {
            return instance.UpscaleFlat.GetJitterIndex();
        }

        public static bool IsUpscaleEnabled()
        {
            return instance != null && instance.UpscaleFlat != null && instance.UpscaleFlat.IsActive();
        }

    }
}