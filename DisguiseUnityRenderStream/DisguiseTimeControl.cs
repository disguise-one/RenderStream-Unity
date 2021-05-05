using UnityEngine;
using UnityEngine.Playables;

using System;

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
        if (DisguiseRenderStream.newFrameData)
        {
            if (DisguiseRenderStream.frameData.localTime < playableDirector.initialTime || DisguiseRenderStream.frameData.localTimeDelta <= 0)
                playableDirector.Pause();
            else
                playableDirector.Resume();

            playableDirector.time = Math.Max(0, DisguiseRenderStream.frameData.localTime - playableDirector.initialTime);

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
