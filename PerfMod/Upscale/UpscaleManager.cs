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
            if (Input.GetKey(KeyCode.LeftControl) && Input.GetKey(KeyCode.LeftAlt) && Input.GetKeyUp(KeyCode.D))
            {
                UpscaleFlat.ToggleUpscaleFeature();
                textDisplayTime = 30f;
            }
            if (IsUpscaleEnabled())
            {
                if (Input.GetKey(KeyCode.LeftControl) && Input.GetKey(KeyCode.LeftAlt) && Input.GetKeyUp(KeyCode.F))
                {
                    UpscaleFlat.SwitchUpscaleMethod();
                    textDisplayTime = 30f;
                }
                if (Input.GetKey(KeyCode.LeftControl) && Input.GetKeyUp(KeyCode.UpArrow))
                {
                    UpscaleFlat.SwitchProfile(true);
                    textDisplayTime = 30f;
                }
                if (Input.GetKey(KeyCode.LeftControl) && Input.GetKeyUp(KeyCode.DownArrow))
                {
                    UpscaleFlat.SwitchProfile(false);
                    textDisplayTime = 30f;
                }
                if (Input.GetKey(KeyCode.LeftControl) && Input.GetKeyUp(KeyCode.LeftArrow))
                {
                    UpscaleFlat.SetSharpness(UpscaleFlat.Sharpness - 0.05f);
                    textDisplayTime = 30f;
                }
                if (Input.GetKey(KeyCode.LeftControl) && Input.GetKeyUp(KeyCode.RightArrow))
                {
                    UpscaleFlat.SetSharpness(UpscaleFlat.Sharpness + 0.05f);
                    textDisplayTime = 30f;
                }
                if (Input.GetKey(KeyCode.LeftControl) && Input.GetKeyUp(KeyCode.Keypad0))
                {
                    UpscaleFlat.ResetSharpness();
                    textDisplayTime = 30f;
                }
                if (Input.GetKey(KeyCode.LeftControl) && Input.GetKeyUp(KeyCode.Keypad3))
                {
                    UpscaleFlat.RefreshMipmapBias();
                }
            }
            if(IsUpscaleEnabled() && (Time.realtimeSinceStartup - UpscaleFlat.lastTimeSetMipBias > 600))
            {
                UpscaleFlat.RefreshMipmapBias();
            }
            if (IsUpscaleEnabled() && Input.GetKeyUp(KeyCode.Keypad2))
            {
            }
            if (IsUpscaleEnabled() && Input.GetKeyUp(KeyCode.Keypad3))
            {
            }
        }

        void OnGUI()
        {
            var SSName = (UpscaleFlat.upscaleMethod == UpscaleMethod.FSR2) ? "FSRv2.1" : "DLSSv2.4";
            var SSName2 = (UpscaleFlat.upscaleMethod == UpscaleMethod.FSR2) ? "DLSSv2.4" : "FSRv2.1";
            var GAPI = (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Direct3D11) ? "DX11" : "DX12";
            if (textDisplayTime > 0)
            {
                if (IsUpscaleEnabled())
                {
                    GUI.Label(new Rect(textFont.fontSize, Screen.height - textFont.fontSize * 4, textFont.fontSize * 10, textFont.fontSize * 3), SSName + " " + GAPI + " " + UpscaleFlat.currentProfile.ToString()
                        + "(" + UpscaleFlat.renderWidth + "," + UpscaleFlat.renderHeight + " -> " + Screen.width + "," + Screen.height + ")\r\n"
                        + "CTRL+ALT+D To Turn Off " + SSName + "    ||    CTRL+ALT+F To Switch To " + SSName2 + "\r\n"
                        + "CTRL+↑/↓ To Change Profile  ||  CTRL+←/→ To Addjust Sharpness(" + UpscaleFlat.Sharpness + ")\r\n", textFont);
                }
                else
                    GUI.Label(new Rect(textFont.fontSize, Screen.height - textFont.fontSize * 4, textFont.fontSize * 10, textFont.fontSize * 3), SSName + " Off\r\n"
                        + "CTRL+ALT+D To Turn On " + SSName, textFont);
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