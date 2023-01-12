using UnityEngine;

namespace Disguise.RenderStream
{
    public static class DisguiseFramerateManager
    {
#if ENABLE_CLUSTER_DISPLAY
        public const bool Enabled = false;
#else
        public const bool Enabled = true;
#endif
        
        static int k_FrameRateUnlimited = -1;
        
        public static bool FrameRateIsLimited => Application.targetFrameRate >= 0;

        public static bool VSyncIsEnabled => QualitySettings.vSyncCount > 0;

        public static void Initialize()
        {
#if !ENABLE_CLUSTER_DISPLAY
            RemoveFrameLimit();
#endif
        }

        public static void Update()
        {
#if !ENABLE_CLUSTER_DISPLAY
            if (VSyncIsEnabled)
            {
                Debug.LogWarning($"{nameof(DisguiseFramerateManager)}: {nameof(QualitySettings)}{nameof(QualitySettings.vSyncCount)} is enabled and may affect performance.");
            }

            if (FrameRateIsLimited)
            {
                Debug.LogWarning($"{nameof(DisguiseFramerateManager)}: {nameof(Application)}{nameof(Application.targetFrameRate)} is limiting framerate and may affect performance.");
            }
#endif
        }

        static void RemoveFrameLimit()
        {
            Application.targetFrameRate = k_FrameRateUnlimited;
        }
    }
}
