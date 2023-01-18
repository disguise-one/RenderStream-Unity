using UnityEngine;

namespace Disguise.RenderStream
{
    public static class DisguiseFramerateManager
    {
#if ENABLE_CLUSTER_DISPLAY
        public static bool Enabled => false;
#else
        public static bool Enabled => true;
#endif
        
        const int k_FrameRateUnlimited = -1;
        
        public static bool FrameRateIsLimited => Application.targetFrameRate >= 0;

        public static bool VSyncIsEnabled => QualitySettings.vSyncCount > 0;

        static bool s_WarnedVSync;
        static bool s_WarnedFrameRate;

        [RuntimeInitializeOnLoadMethod]
        public static void Initialize()
        {
#if !ENABLE_CLUSTER_DISPLAY
            RemoveFrameLimit();
#endif
        }

        public static void Update()
        {
#if !ENABLE_CLUSTER_DISPLAY
            if (!s_WarnedVSync && VSyncIsEnabled)
            {
                Debug.LogWarning($"{nameof(DisguiseFramerateManager)}: {nameof(QualitySettings)}.{nameof(QualitySettings.vSyncCount)} is enabled and may affect performance.");
                s_WarnedVSync = true;
            }

            if (!s_WarnedFrameRate && FrameRateIsLimited)
            {
                Debug.LogWarning($"{nameof(DisguiseFramerateManager)}: {nameof(Application)}.{nameof(Application.targetFrameRate)} is limiting framerate and may affect performance.");
                s_WarnedFrameRate = true;
            }
#endif
        }

        static void RemoveFrameLimit()
        {
            Application.targetFrameRate = k_FrameRateUnlimited;
        }
    }
}
