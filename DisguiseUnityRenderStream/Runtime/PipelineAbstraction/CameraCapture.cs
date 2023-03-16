using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace Disguise.RenderStream
{
    /// <summary>
    /// Configures the <see cref="GameObject"/>'s camera for offscreen rendering and provides access to its
    /// color and optionally its depth buffer. Also handles color space conversions and Y flip.
    ///
    /// <remarks>
    /// The camera will no longer render to the local screen.
    /// Use the <see cref="CameraCapturePresenter"/> component to display a captured texture on the local screen. 
    /// It will handle size and aspect ratio differences between the screen and the texture.
    /// </remarks>
    /// </summary>
    [ExecuteAlways]
    [RequireComponent(typeof(Camera))]
    public class CameraCapture : MonoBehaviour
    {
        public readonly struct Capture
        {
            internal Capture(RenderTexture cameraTexture, RenderTexture depthTexture)
            {
                this.cameraTexture = cameraTexture;
                this.depthTexture = depthTexture;
            }
            
            /// <summary>
            /// Refers to <see cref="CameraCapture.cameraTexture"/>.
            /// </summary>
            public RenderTexture cameraTexture { get; }

            /// <summary>
            /// Refers to <see cref="CameraCapture.depthTexture"/>.
            /// </summary>
            public RenderTexture depthTexture { get; }
        }

        /// <summary>
        /// Called once textures have been captured and are ready to be consumed.
        /// </summary>
        public event Action<ScriptableRenderContext, Capture> onCapture = delegate {};
        
        /// <summary>
        /// <para>
        /// The captured color texture. Its format is defined by <see cref="description"/>.
        /// </para>
        ///
        /// <para>
        /// Note: while the texture may be configured for MSAA, Unity will always use an implicitly resolved version
        /// of the texture for APIs such <see cref="CommandBuffer.CopyTexture(RenderTargetIdentifier, RenderTargetIdentifier)"/>
        /// and see <see cref="Texture.GetNativeTexturePtr"/>.
        /// </para>
        ///
        /// <remarks>
        /// To avoid unnecessary texture copies, this can refer directly to <see cref="Camera.targetTexture"/> (depending on
        /// <see cref="CameraCaptureDescription.m_autoFlipY"/> and <see cref="CameraCaptureDescription.m_colorSpace"/>).
        /// </remarks>
        /// </summary>
        public RenderTexture cameraTexture => m_description.NeedsBlit ? m_cameraBlitTexture : m_cameraTexture;
        
        /// <summary>
        /// The captured depth texture. Its format is defined by <see cref="description"/>.
        /// <remarks><see langword="null"/> if <see cref="description"/> is not configured for depth.</remarks>
        /// </summary>
        public RenderTexture depthTexture => m_depthTexture;

        /// <summary>
        /// Defines the color and depth textures. The textures are automatically disposed and created on change.
        /// </summary>
        public CameraCaptureDescription description
        {
            get => m_description;
            set
            {
                if (m_description != value)
                {
                    m_description = value;
                    Refresh();
                }
            }
        }

        [SerializeField]
        CameraCaptureDescription m_description = CameraCaptureDescription.Default;

        Camera m_camera;
        RenderTexture m_cameraTexture;
        RenderTexture m_cameraBlitTexture;
        RenderTexture m_depthTexture;
        CameraCaptureDescription m_lastDescription;
        
#if DEBUG
        bool m_HasSetSecondNames;        
#endif
        
        const string k_blitProfilerTag = "Camera Capture Blit";

        void Awake()
        {
            m_camera = GetComponent<Camera>();
            
            CreateResources(m_description);
        }

        void OnEnable()
        {
#if !(UNITY_PIPELINE_HDRP && HDRP_VERSION_SUPPORTED) && !(UNITY_PIPELINE_URP && URP_VERSION_SUPPORTED)
            Debug.LogError($"No supported render pipeline was found for {nameof(CameraCapture)}.");
#endif
            
            RenderPipelineManager.endCameraRendering += OnEndCameraRendering;
        }

        void OnDisable()
        {
            RenderPipelineManager.endCameraRendering -= OnEndCameraRendering;
        }

        void OnDestroy()
        {
            DisposeResources();
        }
        
        // Check for updates from the inspector UI
#if UNITY_EDITOR
        void Update()
        {
            if (m_lastDescription != m_description)
                Refresh();
        }
#endif

        void Refresh()
        {
            DisposeResources();
            CreateResources(m_description);
        }

        void CreateResources(CameraCaptureDescription desc)
        {
            m_lastDescription = desc;
            
            if (!desc.IsValid(out var message))
            {
                Debug.LogWarning($"{nameof(CameraCapture)} is disabled because of an invalid configuration: {message}");
                return;
            }

            var cameraDesc = desc.GetCameraDescriptor();
            m_cameraTexture = new RenderTexture(cameraDesc);
            
            m_camera.targetTexture = m_cameraTexture;

            if (desc.NeedsBlit)
            {
                var cameraBlitDesc = desc.GetCameraBlitDescriptor();
                m_cameraBlitTexture = new RenderTexture(cameraBlitDesc);
            }

            if (desc.m_copyDepth)
            {
                var depthCopyDescriptor = desc.GetDepthCopyDescriptor();
                m_depthTexture = new RenderTexture(depthCopyDescriptor);
            }
            
#if DEBUG
            m_cameraTexture.name = $"{nameof(CameraCapture)} Camera Texture Initial {m_cameraTexture.width}x{m_cameraTexture.height}";
            if (m_cameraBlitTexture != null)
                m_cameraBlitTexture.name = $"{nameof(CameraCapture)} Camera Blit Texture Initial {m_cameraBlitTexture.width}x{m_cameraBlitTexture.height}";
            if (m_depthTexture != null)
                m_depthTexture.name = $"{nameof(CameraCapture)} Depth Copy Texture Initial {m_depthTexture.width}x{m_depthTexture.height}";
            m_HasSetSecondNames = false;
#endif
        }

        void DisposeResources()
        {
            if (m_cameraTexture != null)
                m_cameraTexture.Release();
            if (m_cameraBlitTexture != null)
                m_cameraBlitTexture.Release();
            if (m_depthTexture != null)
                m_depthTexture.Release();

            m_cameraTexture = null;
            m_cameraBlitTexture = null;
            m_depthTexture = null;
        }

        void OnEndCameraRendering(ScriptableRenderContext ctx, Camera camera)
        {
            if (camera != m_camera)
                return;
            
            // Disabled because of invalid configuration?
            if (m_cameraTexture == null)
                return;

            var needsBlit = m_description.NeedsBlit;
            
            if (needsBlit)
            {
                var cmd = CommandBufferPool.Get(k_blitProfilerTag);
                
                BlitExtended.Instance.BlitTexture(cmd,
                    m_cameraTexture,
                    m_cameraBlitTexture,
                    BlitExtended.GetSRGBConversion(m_description.SRGBConversion),
                    m_description.NeedsFlipY ? ScaleBias.FlippedY : ScaleBias.Identity);
                
                ctx.ExecuteCommandBuffer(cmd);
                ctx.Submit();
                
                CommandBufferPool.Release(cmd);
            }
            
            if (m_description.m_copyDepth)
            {
                DepthCopy.instance.Execute(ctx, m_depthTexture, m_description.m_depthCopyMode);
            }

            onCapture.Invoke(ctx, new Capture(needsBlit ? m_cameraBlitTexture : m_cameraTexture, m_depthTexture));

#if DEBUG
            // A RenderTexture holds MSAA and resolved versions of its textures.
            // This second naming step will name the resolved textures which Unity creates in a deferred manner.
            if (!m_HasSetSecondNames)
            {
                m_cameraTexture.name = $"{nameof(CameraCapture)} Camera Texture {m_cameraTexture.width}x{m_cameraTexture.height}";
                if (m_cameraBlitTexture != null)
                    m_cameraBlitTexture.name = $"{nameof(CameraCapture)} Camera Blit Texture {m_cameraBlitTexture.width}x{m_cameraBlitTexture.height}";
                if (m_depthTexture != null)
                    m_depthTexture.name = $"{nameof(CameraCapture)} Depth Copy Texture {m_depthTexture.width}x{m_depthTexture.height}";
                m_HasSetSecondNames = true;
            }
#endif
        }
    }
}
