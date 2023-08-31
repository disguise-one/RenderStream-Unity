using UnityEngine;

namespace Disguise.RenderStream
{
    public static class DisguiseFramerateManager
    {
        const int k_FrameRateUnlimited = -1;
        
        public static bool FrameRateIsLimited => Application.targetFrameRate >= 0;

        public static bool VSyncIsEnabled => QualitySettings.vSyncCount > 0;

        static bool s_WarnedVSync;
        static bool s_WarnedFrameRate;

        [RuntimeInitializeOnLoadMethod]
        public static void Initialize()
        {
            RemoveFrameLimit();
        }

        public static void Update()
        {
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
        }

        static void RemoveFrameLimit()
        {
            Application.targetFrameRate = k_FrameRateUnlimited;
        }
    }
}
