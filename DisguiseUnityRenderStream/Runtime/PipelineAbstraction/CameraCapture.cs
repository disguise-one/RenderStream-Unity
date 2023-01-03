using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;

namespace Disguise.RenderStream
{
    [RequireComponent(typeof(Camera))]
    class CameraCapture : MonoBehaviour
    {
        public enum CaptureMode
        {
            /// <summary>
            /// Triggered after a camera finished all its SRPs passes.
            /// This corresponds to <see cref="RenderPipelineManager.endCameraRendering"/>.
            /// </summary>
            CameraRenderingEnd,
            
            /// <summary>
            /// <para>
            /// Triggered after Unity finishes all of its rendering (including UI overlays).
            /// This corresponds to the "PlayerEndOfFrame" graphics profiler tag.
            /// </para>
            /// 
            /// <para>
            /// If the camera doesn't display any UI for example, it could be wasteful to use
            /// this mode instead of <see cref="CameraRenderingEnd"/> in a multi-camera setup
            /// (because it will wait for all cameras to render).
            /// </para>
            /// </summary>
            FrameEnd,
        }
        
        [Serializable]
        public struct CameraCaptureDescription : IEquatable<CameraCaptureDescription>
        {
            public static CameraCaptureDescription Default = new CameraCaptureDescription()
            {
                m_width = 0,
                m_height = 0,
                m_colorFormat = RenderTextureFormat.ARGB32,
                m_depthBufferBits = 24,
                m_copyDepth = false,
                m_depthCopyFormat = RenderTextureFormat.RFloat,
                m_depthCopyMode = DepthCopy.Mode.Linear01
            };
            
            public int m_width;
            public int m_height;
            public RenderTextureFormat m_colorFormat;
            public int m_depthBufferBits;
            public bool m_copyDepth;
            public RenderTextureFormat m_depthCopyFormat;
            public DepthCopy.Mode m_depthCopyMode;

            public bool IsValid => m_width > 0 && m_height > 0;

            public RenderTextureDescriptor GetCameraDescriptor()
            {

                return new RenderTextureDescriptor(m_width, m_height, m_colorFormat, m_depthBufferBits, 1);
            }

            public RenderTextureDescriptor GetDepthCopyDescriptor()
            {
                return new RenderTextureDescriptor(m_width, m_height, m_depthCopyFormat, 0, 1);
            }
            
            public override bool Equals(object obj)
            {
                return obj is CameraCaptureDescription other && Equals(other);
            }

            public override int GetHashCode()
            {
                return HashCode.Combine(
                    m_width,
                    m_height,
                    (int)m_colorFormat,
                    m_depthBufferBits,
                    m_copyDepth,
                    (int)m_depthCopyFormat,
                    (int)m_depthCopyMode
                );
            }

            public bool Equals(CameraCaptureDescription other)
            {
                return
                    m_width == other.m_width &&
                    m_height == other.m_height &&
                    m_colorFormat == other.m_colorFormat &&
                    m_depthBufferBits == other.m_depthBufferBits &&
                    m_copyDepth == other.m_copyDepth &&
                    m_depthCopyFormat == other.m_depthCopyFormat &&
                    m_depthCopyMode == other.m_depthCopyMode;
            }
            
            public static bool operator ==(CameraCaptureDescription lhs, CameraCaptureDescription rhs) => lhs.Equals(rhs);

            public static bool operator !=(CameraCaptureDescription lhs, CameraCaptureDescription rhs) => !(lhs == rhs);
        }

        /// <summary>
        /// Called once textures have been captured and are ready to be consumed.
        ///
        /// <remarks>
        /// <see cref="ScriptableRenderContext"/> is <see langword="null"/> when <see cref="captureMode"/>
        /// is set to <see cref="CaptureMode.FrameEnd"/>.
        /// </remarks>
        /// </summary>
        public event Action<ScriptableRenderContext, CameraCapture> onTexturesReady = delegate {};

        /// <summary>
        /// Determines when to capture the textures.
        /// 
        /// <remarks>
        /// The depth texture is always captured using the <see cref="CaptureMode.CameraRenderingEnd"/>
        /// mode because it's unaffected by later passes (ex UI overlays).
        /// </remarks>
        /// </summary>
        public CaptureMode captureMode
        {
            get => m_captureMode;
            set => m_captureMode = value;
        }
        
        [SerializeField]
        CaptureMode m_captureMode = CaptureMode.CameraRenderingEnd;
        
        public RenderTexture cameraTexture => m_cameraTexture;
        public RenderTexture depthTexture => m_depthTexture;

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

        DepthCopy m_depthCopy;
        Camera m_camera;
        RenderTexture m_cameraTexture;
        RenderTexture m_depthTexture;

#if UNITY_EDITOR
        CameraCaptureDescription m_lastDescription = CameraCaptureDescription.Default;
        bool m_RefreshFlag;
        
        void OnValidate()
        {
            if (m_lastDescription != m_description)
            {
                m_lastDescription = m_description;
                m_RefreshFlag = true;
            }
        }

        void Update()
        {
            if (m_RefreshFlag)
            {
                m_RefreshFlag = false;
                Refresh();
            }
        }
#endif

        void Awake()
        {
            m_depthCopy = new DepthCopy();
            
            m_camera = GetComponent<Camera>();
            
            if (m_description.IsValid)
                CreateResources(m_description);
        }

        IEnumerator Start()
        {
            yield return EndFrameLoop();
        }

        IEnumerator EndFrameLoop()
        {
            while (true)
            {
                yield return new WaitForEndOfFrame();

                if (isActiveAndEnabled && m_captureMode == CaptureMode.FrameEnd)
                    onTexturesReady.Invoke(default, this);
            }
        }

        void OnEnable()
        {
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

        void Refresh()
        {
            DisposeResources();
            CreateResources(m_description);
            m_camera.targetTexture = m_cameraTexture;
        }

        void CreateResources(CameraCaptureDescription desc)
        {
            if (!desc.IsValid)
            {
                Debug.LogWarning("Invalid description");
                return;
            }

            var cameraDesc = desc.GetCameraDescriptor();
            m_cameraTexture = new RenderTexture(cameraDesc);

            if (desc.m_copyDepth)
            {
                var depthCopyDescriptor = desc.GetDepthCopyDescriptor();
                m_depthTexture = new RenderTexture(depthCopyDescriptor);
            }
        }

        void DisposeResources()
        {
            if (m_cameraTexture != null)
                m_cameraTexture.Release();
            if (m_depthTexture != null)
                m_depthTexture.Release();
        }

        void OnEndCameraRendering(ScriptableRenderContext ctx, Camera camera)
        {
            if (camera != m_camera)
                return;

            if (m_description.m_copyDepth)
            {
                m_depthCopy.mode = m_description.m_depthCopyMode;
                m_depthCopy.Execute(ctx, new DepthCopy.FrameData() { m_depthOutput = m_depthTexture });
            }

            if (m_captureMode == CaptureMode.CameraRenderingEnd)
                onTexturesReady.Invoke(ctx, this);
        }
    }
}
