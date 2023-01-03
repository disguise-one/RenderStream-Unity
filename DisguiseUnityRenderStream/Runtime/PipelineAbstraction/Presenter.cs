using System;
using System.Collections;
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
            /// The aspect ratio is lost.
            /// </summary>
            Fill,
            
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
            FillAspectRatio
        }

        /// <summary>
        /// Computes a strategy for the <see cref="Blitter"/> API.
        /// </summary>
        /// <returns>A scale + bias vector</returns>
        public static Vector4 DoStrategy(Strategy strategy, Vector2 srcSize, Vector2 dstSize)
        {
            switch (strategy)
            {
                case Strategy.Fill:
                    return Fill(srcSize, dstSize);
                case Strategy.NoResize:
                    return NoResize(srcSize, dstSize);
                case Strategy.FitWidth:
                    return FitWidth(srcSize, dstSize);
                case Strategy.FitHeight:
                    return FitHeight(srcSize, dstSize);
                case Strategy.Letterbox:
                    return Letterbox(srcSize, dstSize);
                case Strategy.FillAspectRatio:
                    return FillAspectRatio(srcSize, dstSize);
                default:
                    throw new NotImplementedException();
            }
        }
        
        static Vector4 Fill(Vector2 srcSize, Vector2 dstSize)
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
            var yScale = AspectRatio(srcSize) * AspectRatio(dstSize);
            var yOffset = CenterUVOffset(yScale);
            
            return new Vector4(1f, yScale, 0f, yOffset);
        }
        
        static Vector4 FitHeight(Vector2 srcSize, Vector2 dstSize)
        {
            var xScale = AspectRatio(srcSize) / AspectRatio(dstSize);
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
        
        static Vector4 FillAspectRatio(Vector2 srcSize, Vector2 dstSize)
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
    /// Blits a texture to the local screen.
    /// A number of strategies are available to handle the size and aspect ratio differences between the two surfaces.
    ///
    /// <remarks>Assumes that the local screen is the primary display. Modify this class for local multi-monitor specifics.</remarks>
    /// </summary>
    class Presenter : MonoBehaviour
    {
        const string k_profilerTag = "Disguise Presenter";

        public PresenterStrategy.Strategy m_strategy = PresenterStrategy.Strategy.FillAspectRatio;
        
        /// <summary>
        /// The texture to present. Can be any 2D texture.
        /// </summary>
        public RenderTexture m_source;

        bool IsValid => m_source != null;

        Vector2 sourceSize => new Vector2(m_source.width, m_source.height);
        
        Vector2 targetSize => new Vector2(Screen.width, Screen.height);

        /// <summary>
        /// Can override to setup <see cref="m_source"/>.
        /// </summary>
        protected virtual void OnEnable()
        {
            
        }
        
        IEnumerator Start()
        {
            yield return EndFrameLoop();
        }

        IEnumerator EndFrameLoop()
        {
            while (true)
            {
                // Corresponds to the "PlayerEndOfFrame" graphics profiler tag
                yield return new WaitForEndOfFrame();

                if (!isActiveAndEnabled || !IsValid)
                    continue;

                Present();
            }
        }

        void Present()
        {
            CommandBuffer cmd = CommandBufferPool.Get(k_profilerTag);
            cmd.Clear();

            IssueCommands(cmd);
            
            Graphics.ExecuteCommandBuffer(cmd);
            
            cmd.Clear();
            CommandBufferPool.Release(cmd);
        }

        void IssueCommands(CommandBuffer cmd)
        {
            RenderTexture screen = default;
            CoreUtils.SetRenderTarget(cmd, screen);
            
            var srcScaleBias = new Vector4(1f, 1f, 0f, 0f);
            var dstScaleBias = PresenterStrategy.DoStrategy(m_strategy, sourceSize, targetSize);

            // Flip Y for screen
            dstScaleBias.y = -dstScaleBias.y;
            dstScaleBias.w = 1f - dstScaleBias.w;
            
            Blitter.BlitQuad(cmd, m_source, srcScaleBias, dstScaleBias, 0, true);
        }
    }
}
