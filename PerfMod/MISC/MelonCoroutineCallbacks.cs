using System;
using UnityEngine;


namespace PureDark.VRising.PerfMod.MISC
{
    class MelonCoroutineCallbacks : MonoBehaviour
    {

        public MelonCoroutineCallbacks(IntPtr value) : base(value) { }


        void Start()
        {
            DontDestroyOnLoad(this.gameObject);
        }


        void Update()
        {
            MelonCoroutines.Process();

        }

        static int lastFrame = 0;

        void OnGUI()
        {
            int currFrame = Time.frameCount;
            if (lastFrame != currFrame)
            {
                MelonCoroutines.ProcessWaitForEndOfFrame();
                lastFrame = currFrame;
            }
        }

        void FixedUpdate()
        {
            MelonCoroutines.ProcessWaitForFixedUpdate();
        }
    }
}
