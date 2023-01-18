using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Disguise.RenderStream
{
    /// <summary>
    /// Strategy calculations for the <see cref="Blitter"/> API.
    /// </summary>
    static class PresenterStrategy
    {
        /// <summary>
        /// A strategy to handle the size and aspect ratio differences between two surfaces.
        /// </summary>
        public enum Strategy
        {
            /// <summary>
            /// Stretches the source to have the same size as the destination.
            /// The aspect ratio may be lost.
            /// </summary>
            Stretch,
            
            /// <summary>
            /// The source isn't scaled at all but it's centered within the destination.
            /// </summary>
            NoResize,
            
            /// <summary>
            /// The source is scaled while conserving the aspect ratio so that the width matches the destination.
            /// </summary>
            FitWidth,
            
            /// <summary>
            /// The source is scaled while conserving the aspect ratio so that the height matches the destination.
            /// </summary>
            FitHeight,
            
            /// <summary>
            /// The source is scaled while conserving the aspect ratio to fill the destination.
            /// It can't overflow but can leave black bars on the sides.
            /// </summary>
            Letterbox,
            
            /// <summary>
            /// The source is scaled while conserving the aspect ratio to fill the destination.
            /// It can overflow but won't leave black bars on the sides.
            /// </summary>
            Fill
        }

        /// <summary>
        /// Computes a strategy for the <see cref="Blitter"/> API.
        /// </summary>
        /// <returns>A scale + bias vector</returns>
        public static Vector4 DoStrategy(Strategy strategy, Vector2 srcSize, Vector2 dstSize)
        {
            switch (strategy)
            {
                case Strategy.Stretch:
                    return Stretch(srcSize, dstSize);
                case Strategy.NoResize:
                    return NoResize(srcSize, dstSize);
                case Strategy.FitWidth:
                    return FitWidth(srcSize, dstSize);
                case Strategy.FitHeight:
                    return FitHeight(srcSize, dstSize);
                case Strategy.Letterbox:
                    return Letterbox(srcSize, dstSize);
                case Strategy.Fill:
                    return Fill(srcSize, dstSize);
                default:
                    throw new NotImplementedException();
            }
        }
        
        static Vector4 Stretch(Vector2 srcSize, Vector2 dstSize)
        {
            return new Vector4(1f, 1f, 0f, 0f);
        }
        
        static Vector4 NoResize(Vector2 srcSize, Vector2 dstSize)
        {
            var scale = srcSize / dstSize;
            var offset = CenterUVOffset(scale);
            
            return new Vector4(scale.x, scale.y, offset.x, offset.y);
        }
        
        static Vector4 FitWidth(Vector2 srcSize, Vector2 dstSize)
        {
            var yScale = InverseAspectRatio(srcSize) * AspectRatio(dstSize);
            var yOffset = CenterUVOffset(yScale);
            
            return new Vector4(1f, yScale, 0f, yOffset);
        }
        
        static Vector4 FitHeight(Vector2 srcSize, Vector2 dstSize)
        {
            var xScale = AspectRatio(srcSize) * InverseAspectRatio(dstSize);
            var xOffset = CenterUVOffset(xScale);
            
            return new Vector4(xScale, 1f, xOffset, 0f);
        }
        
        static Vector4 Letterbox(Vector2 srcSize, Vector2 dstSize)
        {
            var scrAspect = AspectRatio(srcSize);
            var dstAspect = AspectRatio(dstSize);

            if (scrAspect > dstAspect)
                return FitWidth(srcSize, dstSize);
            else
                return FitHeight(srcSize, dstSize);
        }
        
        static Vector4 Fill(Vector2 srcSize, Vector2 dstSize)
        {
            var scrAspect = AspectRatio(srcSize);
            var dstAspect = AspectRatio(dstSize);

            if (scrAspect < dstAspect)
                return FitWidth(srcSize, dstSize);
            else
                return FitHeight(srcSize, dstSize);
        }

        static float AspectRatio(Vector2 size)
        {
            return size.x / size.y;
        }
        
        static float InverseAspectRatio(Vector2 size)
        {
            return size.y / size.x;
        }

        static float CenterUVOffset(float scale)
        {
            return (1f - scale) / 2f;
        }
        
        static Vector2 CenterUVOffset(Vector2 scale)
        {
            return (Vector2.one - scale) / 2f;
        }
    }
    
    /// <summary>
    /// <para>
    /// Blits a texture to the local screen.
    /// A number of strategies are available to handle the size and aspect ratio differences between the two surfaces.
    /// </para>
    /// 
    /// <para>
    /// <see cref="PresenterInput"/> is responsible for adjusting the <see cref="UnityEngine.EventSystems.EventSystem"/>
    /// mouse coordinates to account for the blit.
    /// </para>
    ///
    /// <remarks>Assumes that the local screen is the primary display. Modify this class for local multi-monitor specifics.</remarks>
    /// </summary>
    [ExecuteAlways]
    class Presenter : MonoBehaviour
    {
        const string k_profilerTag = "Disguise Presenter";
        const string k_profilerClearTag = "Disguise Presenter Clear";

        [SerializeField]
        RenderTexture m_source;
        
        [SerializeField]
        PresenterStrategy.Strategy m_strategy = PresenterStrategy.Strategy.Fill;

        [SerializeField]
        bool m_clearScreen;
        
        Backend m_backEnd;

        public PresenterStrategy.Strategy strategy
        {
            get => m_strategy;
            set => m_strategy = value;
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
        /// Get the coordinates that would be passed to the <see cref="Blitter"/> API.
        /// </summary>
        /// <param name="flipY"></param>
        /// <returns>A scale + bias vector</returns>
        public Vector4 GetScaleBias(bool flipY)
        {
            var scaleBias = PresenterStrategy.DoStrategy(m_strategy, sourceSize, targetSize);

            if (flipY)
            {
                scaleBias.y = -scaleBias.y;
                scaleBias.w = 1f - scaleBias.w;
            }

            return scaleBias;
        }

        void IssueCommands(CommandBuffer cmd)
        {
            RenderTexture screen = default;
            CoreUtils.SetRenderTarget(cmd, screen);
            
            var srcScaleBias = new Vector4(1f, 1f, 0f, 0f);
            var dstScaleBias = GetScaleBias(true); // Flip Y for screen
            
            Blitter.BlitQuad(cmd, m_source, srcScaleBias, dstScaleBias, 0, true);
        }
        
        void ClearScreen()
        {
            CommandBuffer cmd = CommandBufferPool.Get(k_profilerClearTag);

            RenderTexture screen = default;
            CoreUtils.SetRenderTarget(cmd, screen);
            cmd.ClearRenderTarget(true, true, Color.black);
            
            Graphics.ExecuteCommandBuffer(cmd);
            
            CommandBufferPool.Release(cmd);
        }
        
        abstract class Backend
        {
            protected Presenter m_presenter;
            
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