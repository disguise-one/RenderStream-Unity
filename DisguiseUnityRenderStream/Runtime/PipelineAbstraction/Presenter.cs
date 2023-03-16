using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;

namespace Disguise.RenderStream
{
    /// <summary>
    /// <para>
    /// Blits a texture to the local screen.
    /// A number of <see cref="BlitStrategy.Strategy">strategies</see> are available to handle
    /// the size and aspect ratio differences between the two surfaces.
    /// </para>
    /// 
    /// <para>
    /// <see cref="UITKInputForPresenter"/> and <see cref="UGUIInputForPresenter"/> are responsible for
    /// adjusting the <see cref="UnityEngine.EventSystems.EventSystem"/> mouse coordinates to account for the blit.
    /// </para>
    ///
    /// <remarks>
    /// Assumes that the local screen is the <see cref="Display.main">main display</see>.
    /// Modify this class for local multi-monitor specifics.
    /// Doesn't support HDR display output.
    /// </remarks>
    /// </summary>
    [ExecuteAlways]
    class Presenter : MonoBehaviour
    {
        /// <summary>
        /// The color space of the <see cref="source"/>'s texture.
        /// </summary>
        public enum SourceColorSpace
        {
            /// <summary>
            /// Blit directly without any color space conversions.
            /// </summary>
            Unspecified = -2,
            
            /// <summary>
            /// Detect the color space based on the <see cref="source"/>'s texture's <see cref="GraphicsFormat"/>.
            /// sRGB formats are assumed to contain sRGB data, while other formats are assumed to contain linear data.
            /// </summary>
            Auto = -1,
            
            /// <summary>
            /// sRGB color primaries + linear transfer function
            /// </summary>
            Linear = CameraCaptureDescription.ColorSpace.Linear,
            
            /// <summary>
            /// sRGB color primaries + sRGB transfer function
            /// </summary>
            sRGB = CameraCaptureDescription.ColorSpace.sRGB,
        }
        
        const string k_profilerTag = "Disguise Presenter";
        const string k_profilerClearTag = "Disguise Presenter Clear";

        [SerializeField]
        RenderTexture m_source;
        
        [SerializeField]
        SourceColorSpace m_sourceColorSpace = SourceColorSpace.Auto;
        
        [SerializeField]
        BlitStrategy.Strategy m_strategy = BlitStrategy.Strategy.Fill;

        [SerializeField]
        bool m_autoFlipY = true;
        
        [SerializeField]
        bool m_clearScreen;
        
        Backend m_backEnd;

        /// <summary>
        /// Describes how to handle the size and aspect ratio differences between the <see cref="source"/> and the screen.
        /// </summary>
        public BlitStrategy.Strategy strategy
        {
            get => m_strategy;
            set => m_strategy = value;
        }
        
        /// <summary>
        /// On platforms such as DX12 the texture needs to be flipped before being presented to the screen.
        /// </summary>
        public bool autoFlipY
        {
            get => m_autoFlipY;
            set => m_autoFlipY = value;
        }

        /// <summary>
        /// The texture to present. Can be any 2D texture.
        /// </summary>
        public RenderTexture source
        {
            get => m_source;
            protected set => m_source = value;
        }
        
        /// <summary>
        /// The color space of the <see cref="source"/> texture.
        /// <see cref="SourceColorSpace.Auto"/> should manage most cases.
        /// </summary>
        public SourceColorSpace sourceColorSpace
        {
            get => m_sourceColorSpace;
            set => m_sourceColorSpace = value;
        }

        /// <summary>
        /// When Unity has no onscreen cameras the screen might never be cleared.
        /// </summary>
        public bool clearScreen
        {
            get => m_clearScreen;
            set => m_clearScreen = value;
        }

        public bool IsValid => m_source != null;

        public Vector2 sourceSize => new Vector2(m_source.width, m_source.height);
        
        public Vector2 targetSize => new Vector2(Screen.width, Screen.height);

        /// <summary>
        /// Can override to setup <see cref="m_source"/>.
        /// </summary>
        protected virtual void OnEnable()
        {
#if DISGUISE_UNITY_USE_HDR_DISPLAY
            Debug.LogWarning($"{nameof(Presenter)} only supports SDR output, but HDR Display Output is allowed in the Project Settings.");
#endif
            
#if UNITY_EDITOR
            m_backEnd = new EditorBackend(this);
#else
            m_backEnd = new PlayerBackend(this);
#endif
            
            m_backEnd.Enable();
        }
        
        protected virtual void OnDisable()
        {
            m_backEnd.Disable();
        }

        protected virtual void Update()
        {
            if (m_clearScreen)
                ClearScreen();
        }
        
        /// <summary>
        /// Get the destination UV transformations to pass to the <see cref="Blitter"/> API.
        /// </summary>
        /// <param name="skipAutoFlip">
        /// When true, the return value isn't adjusted for the graphics API's UV representation.
        /// This is useful for UI which only needs a CPU representation of the bounds.
        /// </param>
        public ScaleBias GetScaleBias(bool skipAutoFlip)
        {
            var scaleBias = BlitStrategy.DoStrategy(m_strategy, sourceSize, targetSize);

            if (autoFlipY && !skipAutoFlip && SystemInfo.graphicsUVStartsAtTop)
                scaleBias = ScaleBias.FlipY(scaleBias);

            return scaleBias;
        }

        /// <summary>
        /// Resolves the color space conversion to apply based on
        /// the source texture and the main display's backbuffer.
        /// </summary>
        BlitExtended.ColorSpaceConversion GetColorSpaceConversion()
        {
            if (m_sourceColorSpace == SourceColorSpace.Unspecified)
                return BlitExtended.ColorSpaceConversion.None;
            
            var sourceDescriptor = m_sourceColorSpace switch
            {
                SourceColorSpace.Auto => SRGBConversions.GetAutoDescriptor(m_source),
                SourceColorSpace.Linear => new SRGBConversions.Descriptor(SRGBConversions.Space.Linear, SRGBConversions.GetTextureFormat(m_source)),
                SourceColorSpace.sRGB => new SRGBConversions.Descriptor(SRGBConversions.Space.sRGB, SRGBConversions.GetTextureFormat(m_source)),
                _ => throw new ArgumentOutOfRangeException()
            };

            var mainDisplayDescriptor = SRGBConversions.GetDisplayDescriptor(Display.main);
            var conversion = SRGBConversions.GetConversion(sourceDescriptor, mainDisplayDescriptor);
            return BlitExtended.GetSRGBConversion(conversion);
        }

        void IssueCommands(CommandBuffer cmd)
        {
            const RenderTexture mainDisplay = default;
            CoreUtils.SetRenderTarget(cmd, mainDisplay);
            
            var srcScaleBias = ScaleBias.Identity;
            var dstScaleBias = GetScaleBias(false);
            
            BlitExtended.Instance.BlitQuad(cmd, m_source, GetColorSpaceConversion(), srcScaleBias, dstScaleBias);
        }
        
        void ClearScreen()
        {
            CommandBuffer cmd = CommandBufferPool.Get(k_profilerClearTag);

            const RenderTexture mainDisplay = default;
            CoreUtils.SetRenderTarget(cmd, mainDisplay);
            cmd.ClearRenderTarget(true, true, Color.black);
            
            Graphics.ExecuteCommandBuffer(cmd);
            
            CommandBufferPool.Release(cmd);
        }
        
        abstract class Backend
        {
            protected readonly Presenter m_presenter;
            
            protected Backend(Presenter presenter)
            {
                m_presenter = presenter;
            }

            protected bool ShouldRun => m_presenter.IsValid;
        
            public abstract void Enable();
            public abstract void Disable();
        }
        
#if UNITY_EDITOR
        /// <summary>
        /// Drawing after WaitForEndOfFrame ensures that we target the editor's intermediate "GameView RT" instead of the screen.
        /// </summary>
        class EditorBackend : Backend
        {
            Coroutine m_FrameLoop;
            
            public EditorBackend(Presenter presenter):
                base(presenter)
            {
                
            }
            
            public override void Enable()
            {
                m_FrameLoop = m_presenter.StartCoroutine(FrameLoop());
            }
        
            public override void Disable()
            {
                m_presenter.StopCoroutine(m_FrameLoop);
                m_FrameLoop = null;
            }
            
            IEnumerator FrameLoop()
            {
                while (true)
                {
                    yield return new WaitForEndOfFrame();
                    
                    if (!ShouldRun)
                        continue;
                
                    CommandBuffer cmd = CommandBufferPool.Get(k_profilerTag);
                    m_presenter.IssueCommands(cmd);

                    Graphics.ExecuteCommandBuffer(cmd);
                    
                    CommandBufferPool.Release(cmd);
                }
            }
        }
#else
        /// <summary>
        /// In the player we can draw directly to the screen.
        /// </summary>
        class PlayerBackend : Backend
        {
            public PlayerBackend(Presenter presenter):
                base(presenter)
            {
                
            }
            
            public override void Enable()
            {
                RenderPipelineManager.endContextRendering += OnEndContextRendering;
            }
        
            public override void Disable()
            {
                RenderPipelineManager.endContextRendering -= OnEndContextRendering;
            }
        
            void OnEndContextRendering(ScriptableRenderContext context, List<Camera> _)
            {
                if (!ShouldRun)
                    return;
                
                // Check that we're in the correct context (ex not rendering probes)
                if (RenderTexture.active != null)
                    return;
                
                CommandBuffer cmd = CommandBufferPool.Get(k_profilerTag);
                m_presenter.IssueCommands(cmd);

                context.ExecuteCommandBuffer(cmd);
                context.Submit();
        
                CommandBufferPool.Release(cmd);
            }
        }
#endif
    }
}
