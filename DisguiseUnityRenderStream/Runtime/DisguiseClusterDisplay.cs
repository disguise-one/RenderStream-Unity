#if ENABLE_CLUSTER_DISPLAY
using System;
using UnityEngine;
using UnityEngine.SceneManagement;
using Unity.ClusterDisplay;
using Unity.ClusterDisplay.Utils;

namespace Disguise.RenderStream
{
    partial class DisguiseRenderStream
    {
        static EventBus<FrameData> s_FrameDataBus;
        static IDisposable s_FollowerFrameDataSubscription;
        
        public static void RegisterClusterDisplayHooks()
        {
            if (!ServiceLocator.TryGet(out IClusterSyncState clusterSyncState)) return;
        
            s_FrameDataBus = new EventBus<FrameData>(clusterSyncState);
            switch (clusterSyncState.NodeRole)
            {
                case NodeRole.Emitter when !clusterSyncState.RepeatersDelayedOneFrame:
                    // If we're only syncing the basic frame data (no custom data being computed during
                    // the frame), then we can cheat a bit to reduce latency, by publishing RenderStream FrameData
                    // *before* the sync point.
                    ClusterSyncLooper.onInstanceDoPreFrame += AwaitFrameOnEmitterNode;
                    ClusterSyncLooper.onInstanceDoPreFrame += PublishEmitterEvents;
                    break;
                case NodeRole.Emitter when clusterSyncState.RepeatersDelayedOneFrame:
                    ClusterSyncLooper.onInstanceDoPreFrame += AwaitFrameOnEmitterNode;
                    ClusterSyncLooper.onInstanceDoLateFrame += PublishEmitterEvents;
                    break;
                case NodeRole.Repeater:
                {
                    s_FollowerFrameDataSubscription = s_FrameDataBus.Subscribe(BeginFollowerFrameOnRepeaterNode);
                    ClusterDebug.Log("Setting as follower");
                    var error = PluginEntry.instance.setFollower(1);
                    if (error is not RS_ERROR.RS_ERROR_SUCCESS)
                    {
                        ClusterDebug.Log($"Could not set follower: {error.ToString()}");
                    }

                    break;
                }
                case NodeRole.Unassigned:
                    ClusterDebug.LogError("Attempting to use Cluster Display, but Cluster Display has not been initialized.");
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        public static void UnregisterClusterDisplayEvents()
        {
            ClusterSyncLooper.onInstanceDoPreFrame -= AwaitFrameOnEmitterNode;
            ClusterSyncLooper.onInstanceDoPreFrame -= PublishEmitterEvents;
            ClusterSyncLooper.onInstanceDoLateFrame -= PublishEmitterEvents;
            s_FollowerFrameDataSubscription?.Dispose();
            s_FrameDataBus?.Dispose();
        }

        static void PublishEmitterEvents()
        {
            s_FrameDataBus.Publish(frameData);
        }
    
        static void AwaitFrameOnEmitterNode()
        {
            DisguiseRenderStreamSettings settings = DisguiseRenderStreamSettings.GetOrCreateSettings();
        
            RS_ERROR error = PluginEntry.instance.awaitFrameData(500, out frameData);
            if (error == RS_ERROR.RS_ERROR_QUIT)
                Application.Quit();
            if (error == RS_ERROR.RS_ERROR_STREAMS_CHANGED)
                CreateStreams();
            switch (settings.sceneControl)
            {
                case DisguiseRenderStreamSettings.SceneControl.Selection:
                    if (SceneManager.GetActiveScene().buildIndex != frameData.scene)
                    {
                        newFrameData = false;
                        SceneManager.LoadScene((int)frameData.scene);
                        return;
                    }

                    break;
            }
            newFrameData = (error == RS_ERROR.RS_ERROR_SUCCESS);
            if (newFrameData)
            {
                ProcessFrameData(frameData);
            }
        }
    
        static void BeginFollowerFrameOnRepeaterNode(FrameData emitterFrameData)
        {
            ClusterDebug.Log($"Received disguise framedata from controller: {emitterFrameData.tTracked}");
            DisguiseRenderStreamSettings settings = DisguiseRenderStreamSettings.GetOrCreateSettings();
            RS_ERROR error = PluginEntry.instance.beginFollowerFrame(emitterFrameData.tTracked);
            Debug.Assert(error != RS_ERROR.RS_NOT_INITIALISED);
            frameData = emitterFrameData;
            if (error == RS_ERROR.RS_ERROR_QUIT)
                Application.Quit();
            if (error == RS_ERROR.RS_ERROR_STREAMS_CHANGED)
            {
                CreateStreams();
                // error = PluginEntry.instance.beginFollowerFrame(emitterFrameData.tTracked);
            }

            switch (settings.sceneControl)
            {
                case DisguiseRenderStreamSettings.SceneControl.Selection:
                    if (SceneManager.GetActiveScene().buildIndex != frameData.scene)
                    {
                        newFrameData = false;
                        SceneManager.LoadScene((int)frameData.scene);
                    }

                    break;
            }

            newFrameData = (error == RS_ERROR.RS_ERROR_SUCCESS);
        
            if (newFrameData)
            {
                ProcessFrameData(frameData);
            }
        }
    }
}
#endif