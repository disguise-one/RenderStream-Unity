using UnityEngine;
using UnityEngine.Playables;

using System;

namespace Disguise.RenderStream
{
    [AddComponentMenu("Disguise RenderStream/Time Control")]
    [RequireComponent(typeof(PlayableDirector))]
    public class DisguiseTimeControl : MonoBehaviour
    {
        void Start()
        {
            playableDirector = GetComponent<PlayableDirector>();
            playableDirector.timeUpdateMode = DirectorUpdateMode.Manual;
        }

        void Update()
        {
            Time.captureDeltaTime = 0;
            var renderStream = DisguiseRenderStream.Instance;
            if (renderStream == null) return;
            
            if (renderStream.HasNewFrameData)
            {
                if (renderStream.LatestFrameData.localTime < playableDirector.initialTime || renderStream.LatestFrameData.localTimeDelta <= 0)
                    playableDirector.Pause();
                else
                    playableDirector.Resume();

                if (renderStream.LatestFrameData.localTimeDelta > 0)
                {
                    Time.captureDeltaTime = (float)renderStream.LatestFrameData.localTimeDelta;
                }

                playableDirector.time = Math.Max(0, renderStream.LatestFrameData.localTime - playableDirector.initialTime);

                switch (playableDirector.extrapolationMode)
                {
                    case DirectorWrapMode.Hold:
                        if (playableDirector.time > playableDirector.duration)
                            playableDirector.time = playableDirector.duration;
                        break;
                    case DirectorWrapMode.Loop:
                        playableDirector.time = (playableDirector.time % playableDirector.duration);
                        break;
                }

                playableDirector.Evaluate();
            }
        }

        private PlayableDirector playableDirector;
    }
}