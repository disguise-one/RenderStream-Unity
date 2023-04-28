#if ENABLE_CLUSTER_DISPLAY
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Disguise.RenderStream.Utils;
using UnityEngine;
using UnityEngine.SceneManagement;
using Unity.ClusterDisplay;
using Unity.ClusterDisplay.Utils;
using UnityEngine.Scripting;
using Debug = UnityEngine.Debug;

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
    [ClusterParamProcessor]
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

        protected override void Initialize()
        {
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

            InitializeGfxResources();
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

        [ClusterParamProcessorMethod, Preserve]
        public static ClusterParams ProcessClusterParams(ClusterParams clusterParams)
        {
            var commandLineArgs = Environment.GetCommandLineArgs();
            var repeaterCountIdx = Array.IndexOf(commandLineArgs, "-followers");
            int? maybeRepeaterCount =
                repeaterCountIdx > 0 && commandLineArgs.Length > repeaterCountIdx + 1 &&
                int.TryParse(commandLineArgs[repeaterCountIdx + 1], out var repeaterCountArg)
                    ? repeaterCountArg
                    : null;

            if (maybeRepeaterCount.HasValue)
            {
                clusterParams.RepeaterCount = maybeRepeaterCount.Value;
            }

            ClusterDebug.Log($"Trying to assign ids for {clusterParams.RepeaterCount} repeaters");
            if (clusterParams.RepeaterCount < 1)
            {
                ClusterDebug.LogWarning("There are no repeater nodes specified.");
                // Leave the parameters alone, skip the rest of the parameter processing.
                // Other processors may still want to work with the parameters.
                return clusterParams;
            }

            try
            {
                var adapterInfo = MulticastExtensions.SelectNetworkInterface(clusterParams.AdapterName);
                clusterParams.NodeID = NegotiateNodeID(clusterParams, adapterInfo.address);
                ClusterDebug.Log($"Auto-assigning node ID {clusterParams.NodeID} (repeaters: {clusterParams.RepeaterCount})");

                clusterParams.AdapterName = adapterInfo.name;
                clusterParams.Fence = FrameSyncFence.External;
                clusterParams.ClusterLogicSpecified = true;

                // Arbitrarily assign Node 0 as the emitter
                clusterParams.Role = clusterParams.NodeID == 0 ? NodeRole.Emitter : NodeRole.Repeater;
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                clusterParams.ClusterLogicSpecified = false;
            }

            return clusterParams;
        }

        /// <summary>
        /// Negotiate a distinct ID amongst other running render nodes. Uses the same multi-cast end point
        /// as the core Cluster Display logic.
        /// </summary>
        /// <param name="clusterParams">Parameters containing the networking information.</param>
        /// <param name="adapterAddress">The (unicast) IP address of the network interface we're negotiating on.</param>
        /// <returns></returns>
        /// <exception cref="TimeoutException">The operation timed out.</exception>
        /// <exception cref="OperationCanceledException">Multiple errors were encountered. See the log for details.</exception>
        static byte NegotiateNodeID(ClusterParams clusterParams, IPAddress adapterAddress)
        {
            var address = IPAddress.Parse(clusterParams.MulticastAddress);
            var sendEndPoint = new IPEndPoint(address, clusterParams.Port);
            var timeoutMilliseconds = (int)clusterParams.HandshakeTimeout.TotalMilliseconds;
            
            using var udpClient = new UdpClient();
            udpClient.EnableMulticast(address,
                clusterParams.Port,
                adapterAddress,
                timeoutMilliseconds);

            int nodeIdResult;
            var expectedNodeCount = clusterParams.RepeaterCount + 1;

            const string announcePrefix = "8e310677-85cf-4c0c-8209-15890342c4e4";
            const string readyPrefix = "772c27a7-e38d-42a9-835c-158f05dc50e0";
            
            // Message that indicates that we're available to join a group.
            // Each node must have a unique announcement message.
            var announceMessage = $"{announcePrefix}:{adapterAddress}-{Process.GetCurrentProcess().Id}";
            
            // Message that indicates that we're discovered a valid group.
            var readyMessage = $"{readyPrefix}:{adapterAddress}-{Process.GetCurrentProcess().Id}";
            var foundGroup = false;
            
            // Set up our listening and announcing tasks.
            var cancellation = new CancellationTokenSource();
            var token = cancellation.Token;
            
            var listen = Task.Run(async () =>
            {
                // Listen for messages, including from self
                SortedSet<string> announcements = new();
                HashSet<string> readyReports = new();
                while (!token.IsCancellationRequested)
                {
                    var result = await udpClient.ReceiveAsync();
                    var str = Encoding.UTF8.GetString(result.Buffer);
                    Debug.Log($"Received negotiation message {str}");
                    if (str.StartsWith(announcePrefix))
                    {
                        if (announcements.Add(str))
                        {
                            // We've received announcements from the expected nodes. We have a group.
                            foundGroup = announcements.Count >= expectedNodeCount;
                        }
                    }
                    else if (str.StartsWith(readyPrefix))
                    {
                        // We're going to keep going until each node is reporting that they have a group.
                        if (readyReports.Add(str) && readyReports.Count >= expectedNodeCount)
                        {
                            Debug.Log($"All {expectedNodeCount} nodes reported ready. Stop listening for messages.");
                            break;
                        }
                    }
                }

                // Done!
                // At this point, we've found a group, and received "ready" announcements from all other nodes.
                // Return all the announcements that we received from the completed group.
                return announcements;
            });

            // Add a timeout to the listen task.
            var listenWithTimeout = Task.WhenAny(listen,
                Task.Run(async () =>
                {
                    await Task.Delay(clusterParams.HandshakeTimeout, token);
                    return new List<string>();
                }));

            var announce = Task.Run(async () =>
            {
                var announceBytes = Encoding.UTF8.GetBytes(announceMessage);
                var readyBytes = Encoding.UTF8.GetBytes(readyMessage);
                while (!token.IsCancellationRequested)
                {
                    // Announce our presence to the network.
                    // Even if we've already found a group, we want to continue announcing until this task
                    // is cancelled (to give other nodes a chance to discover us).
                    await udpClient.SendAsync(announceBytes, announceBytes.Length, sendEndPoint);
                    if (foundGroup)
                    {
                        // Announce that we have a group
                        Debug.Log("Reporting ready");
                        await udpClient.SendAsync(readyBytes, readyBytes.Length, sendEndPoint);
                    }
                    await Task.Delay(100, token);
                }
            });

            try
            {
                if (listenWithTimeout.Result == listen)
                {
                    // Now that we have a completed group, we can assign a node ID.
                    var announcements = listen.Result;
                    
                    // Order the announcements in a list. The node ID will the index
                    // of this process's message.
                    nodeIdResult = announcements.ToList().IndexOf(announceMessage);
                }
                else
                {
                    throw new TimeoutException("Timed out waiting for announcements");
                }
            }
            catch (AggregateException e)
            {
                foreach (var exception in e.InnerExceptions)
                {
                    Debug.LogException(exception);
                }

                throw new OperationCanceledException("");
            }
            finally
            {
                // Cancel outstanding tasks.
                cancellation.Cancel();
                try
                {
                    Task.WaitAll(new[] { listen, announce }, clusterParams.HandshakeTimeout);
                }
                catch (AggregateException e)
                {
                    // Nothing to do here.
                    // This exception is should be coming from the "announce" task being cancelled forceably.
                    // Errors from the "listen" task should have been caught already.
                }
            }

            return (byte)nodeIdResult;
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
