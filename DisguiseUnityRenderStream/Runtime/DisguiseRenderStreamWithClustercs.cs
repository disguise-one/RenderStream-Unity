#if ENABLE_CLUSTER_DISPLAY
using System;
using System.IO;
using System.Threading;
using Microsoft.Win32;
using UnityEngine;
using UnityEngine.SceneManagement;
using Unity.ClusterDisplay;
using Unity.ClusterDisplay.Utils;

namespace Disguise.RenderStream
{
    /// <summary>
    /// A specialized version of <see cref="DisguiseRenderStream"/> that inserts synchronization logic from
    /// the Cluster Display package.
    /// </summary>
    /// <remarks>
    /// The Cluster Display synchronization functionality keeps render nodes in lockstep, which is necessary for
    /// physics, visual effects, and other computed/simulated behaviors.
    /// </remarks>
    class DisguiseRenderStreamWithCluster : DisguiseRenderStream, IDisposable
    {
        EventBus<FrameData> m_FrameDataBus;
        IDisposable m_FollowerFrameDataSubscription;
        
        /// <summary>
        /// An instance of <see cref="ClusterSync"/> that we own. If null, it means cluster rendering is disabled or
        /// there is already another instance of <see cref="ClusterSync"/>.
        /// </summary>
        ClusterSync m_ClusterSync;

        internal DisguiseRenderStreamWithCluster(ManagedSchema schema)
            : base(schema) { }

        void InitializeClusterSyncInstance()
        {
            if (!ServiceLocator.TryGet<IClusterSyncState>(out _))
            {
                var settings = DetectSettings();
                if (settings.ClusterLogicSpecified)
                {
                    m_ClusterSync = new ClusterSync("RenderStreamCluster");
                    m_ClusterSync.EnableClusterDisplay(settings);

                    ServiceLocator.Provide<IClusterSyncState>(m_ClusterSync);
                }
            }
            else
            {
                ClusterDebug.LogWarning("ClusterSync was previously initialized");
            }
        }

        protected override void Initialize()
        {
            InitializeClusterSyncInstance();

            ServiceLocator.TryGet(out IClusterSyncState clusterSyncState);
            if (clusterSyncState is not { IsClusterLogicEnabled: true })
            {
                // We're not actually running in a cluster.
                // Fall back to base implementation.
                base.Initialize();
                return;
            }
            
            if (m_FrameDataBus != null)
            {
                ClusterDebug.LogWarning("RenderStream is already registered with Cluster Display");
                return;
            }
         
            m_FrameDataBus = new EventBus<FrameData>(clusterSyncState);
            switch (clusterSyncState.NodeRole)
            {
                // If we're the emitter (the "controller" in disguise-land), wait for the frame request (rs_awaitFrame)
                // before the Cluster Display sync point.
                case NodeRole.Emitter when !clusterSyncState.RepeatersDelayedOneFrame:
                {
                    ClusterDebug.Log("Setting the node as Disguise RenderStream controller (no delay)");
                    // If we're only syncing the basic engine state (no custom data being computed during
                    // the frame), then we can cheat a bit to reduce latency, by publishing disguise FrameData
                    // *before* the sync point, so it will get transmitted on the current sync point (custom data is
                    // typically transmitted during the *next* sync point)
                    ClusterSyncLooper.onInstanceDoPreFrame += AwaitFrame;
                    ClusterSyncLooper.onInstanceDoPreFrame += PublishEmitterEvents;
                    break;
                }
                case NodeRole.Emitter when clusterSyncState.RepeatersDelayedOneFrame:
                {
                    ClusterDebug.Log("Setting the node as Disguise RenderStream controller (one frame delay)");
                    // Custom data is computed during the frame and transmitted at the next sync point, which requires
                    // repeaters to be operating 1 frame behind.
                    ClusterSyncLooper.onInstanceDoPreFrame += AwaitFrame;
                    ClusterSyncLooper.onInstanceDoLateFrame += PublishEmitterEvents;
                    break;
                }
                case NodeRole.Repeater:
                {
                    // If we're a repeater (a "follower"), we don't need to wait for disguise, and instead just wait on the
                    // normal Cluster Display sync point. The disguise FrameData structure is transmitted as custom
                    // data on the EventBus.
                    m_FollowerFrameDataSubscription = m_FrameDataBus.Subscribe(BeginFollowerFrameOnRepeaterNode);
                    ClusterDebug.Log("Setting the node as Disguise RenderStream follower");
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

        void PublishEmitterEvents() => m_FrameDataBus.Publish(LatestFrameData);

    
        void BeginFollowerFrameOnRepeaterNode(FrameData emitterFrameData)
        {
            DisguiseRenderStreamSettings settings = DisguiseRenderStreamSettings.GetOrCreateSettings();
            RS_ERROR error = PluginEntry.instance.beginFollowerFrame(emitterFrameData.tTracked);
            
            Debug.Assert(error != RS_ERROR.RS_NOT_INITIALISED);
            LatestFrameData = emitterFrameData;
            
            if (error == RS_ERROR.RS_ERROR_QUIT)
                Application.Quit();
            if (error == RS_ERROR.RS_ERROR_STREAMS_CHANGED)
            {
                CreateStreams();
            }

            switch (settings.sceneControl)
            {
                case DisguiseRenderStreamSettings.SceneControl.Selection:
                    if (SceneManager.GetActiveScene().buildIndex != LatestFrameData.scene)
                    {
                        HasNewFrameData = false;
                        SceneManager.LoadScene((int)LatestFrameData.scene);
                    }

                    break;
            }

            HasNewFrameData = (error == RS_ERROR.RS_ERROR_SUCCESS);
        
            if (HasNewFrameData)
            {
                ProcessFrameData(LatestFrameData);
            }
        }
        
        static ClusterParams DetectSettings()
        {
            var commandLineArgs = Environment.GetCommandLineArgs();
            var repeaterCountIdx = Array.IndexOf(commandLineArgs, "-nodes");
            int? repeaterCount =
                repeaterCountIdx > 0 && commandLineArgs.Length > repeaterCountIdx + 1 &&
                int.TryParse(commandLineArgs[repeaterCountIdx + 1], out var repeaterCountArg)
                    ? repeaterCountArg
                    : null;

            ClusterDebug.Log($"Trying to assign ids for {repeaterCount} repeaters");

            var clusterParams = new ClusterParams
            {
                Port = 25690,
                MulticastAddress = "224.0.1.0",
                ClusterLogicSpecified = true,
                CommunicationTimeout = TimeSpan.FromSeconds(5),
                HandshakeTimeout = TimeSpan.FromSeconds(10),
                NodeID = 255    // placeholder
            };

#if NET_4_6
            // Get the node info from the Win32 registry. Use the Set-NodeProperty.ps1 script (look for it in the
            // Scripts directory in the repo root) to set these values.
            using var clusterKey = Registry.LocalMachine.OpenSubKey("Software\\Unity Technologies\\ClusterDisplay");
            if (clusterKey != null)
            {
                clusterParams.NodeID = (byte)(int)clusterKey.GetValue("NodeID");
                clusterParams.RepeaterCount = repeaterCount ?? (int)clusterKey.GetValue("RepeaterCount");
                clusterParams.MulticastAddress = (string)clusterKey.GetValue("MulticastAddress");
                clusterParams.Port = (int)clusterKey.GetValue("MulticastPort");
                clusterParams.AdapterName = (string)clusterKey.GetValue("AdapterName");
            }
            else if (repeaterCount.HasValue)
            {
#endif
                clusterParams.RepeaterCount = repeaterCount.Value;
                // If we're running several nodes on the same machine, use a single-access file to keep track
                // of node ids in use.
                var retries = 0;
                var nodeIdFilePath = Path.Combine(Path.GetTempPath(), Application.productName + ".node");
                while (clusterParams.NodeID == 255 && retries < 10)
                {
                    try
                    {
                        using var fileStream = new FileStream(nodeIdFilePath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
                        using var reader = new StreamReader(fileStream);
                        if (!byte.TryParse(reader.ReadLine(), out clusterParams.NodeID))
                        {
                            clusterParams.NodeID = 0;
                        }

                        fileStream.Position = 0;
                        using var writer = new StreamWriter(fileStream);
                        writer.Write(clusterParams.NodeID + 1);
                        fileStream.SetLength(fileStream.Position);
                    }
                    catch (Exception ex)
                    {
                        retries++;
                        ClusterDebug.Log($"Unable to access node ID file: {ex.Message}");
                        Thread.Sleep(500);
                    }
                }

                Application.quitting += () =>
                {
                    File.Delete(nodeIdFilePath);
                };

#if NET_4_6
            }
#endif
            ClusterDebug.Log($"Auto-assigning node ID {clusterParams.NodeID} (repeaters: {clusterParams.RepeaterCount})");
            // First one to start up gets to be the emitter - node 0
            clusterParams.EmitterSpecified = clusterParams.NodeID == 0;

            if (clusterParams.NodeID == 255 || clusterParams.RepeaterCount == 0)
            {
                ClusterDebug.LogWarning("Cannot obtain cluster settings. Cluster rendering will be disabled");
                clusterParams.ClusterLogicSpecified = false;
            }

            return clusterParams;
        }

        public void Dispose()
        {
            ClusterSyncLooper.onInstanceDoPreFrame -= AwaitFrame;
            ClusterSyncLooper.onInstanceDoPreFrame -= PublishEmitterEvents;
            ClusterSyncLooper.onInstanceDoLateFrame -= PublishEmitterEvents;
            m_FollowerFrameDataSubscription?.Dispose();
            m_FrameDataBus?.Dispose();
            m_FrameDataBus = null;
            if (m_ClusterSync != null)
            {
                m_ClusterSync.DisableClusterDisplay();
                ServiceLocator.Withdraw<IClusterSyncState>();
                m_ClusterSync = null;
            }
        }
    }
}
#endif