using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Disguise.RenderStream
{
    static class PresenterStrategy
    {
        public enum Strategy
        {
            Fill,
            CenterWithoutResizing,
            FitWidth,
            FitHeight,
            Fit,
            Crop
        }

        public static Vector4 DoStrategy(Strategy strategy, Vector2 srcSize, Vector2 dstSize)
        {
            switch (strategy)
            {
                case Strategy.Fill:
                    return Fill(srcSize, dstSize);
                case Strategy.CenterWithoutResizing:
                    return CenterWithoutResizing(srcSize, dstSize);
                case Strategy.FitWidth:
                    return FitWidth(srcSize, dstSize);
                case Strategy.FitHeight:
                    return FitHeight(srcSize, dstSize);
                case Strategy.Fit:
                    return Fit(srcSize, dstSize);
                case Strategy.Crop:
                    return Crop(srcSize, dstSize);
                default:
                    throw new NotImplementedException();
            }
        }
        
        static Vector4 Fill(Vector2 srcSize, Vector2 dstSize)
        {
            return new Vector4(1f, 1f, 0f, 0f);
        }
        
        static Vector4 CenterWithoutResizing(Vector2 srcSize, Vector2 dstSize)
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
        
        static Vector4 Fit(Vector2 srcSize, Vector2 dstSize)
        {
            var scrAspect = AspectRatio(srcSize);
            var dstAspect = AspectRatio(dstSize);

            if (scrAspect > dstAspect)
                return FitWidth(srcSize, dstSize);
            else
                return FitHeight(srcSize, dstSize);
        }
        
        static Vector4 Crop(Vector2 srcSize, Vector2 dstSize)
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
    
    class Presenter : MonoBehaviour
    {
        const string k_profilerTag = "Disguise Presenter";

        public PresenterStrategy.Strategy m_strategy = PresenterStrategy.Strategy.Fit;
        public RenderTexture m_source;
        public RenderTexture m_target;

        bool IsValid => m_source != null;

        Vector2 sourceSize => new Vector2(m_source.width, m_source.height);
        
        Vector2 targetSize
        {
            get
            {
                if (m_target == null)
                    return new Vector2(Screen.width, Screen.height);
                else
                    return new Vector2(m_target.width, m_target.height);
            }
        }

        protected virtual void OnEnable()
        {
            RenderPipelineManager.endContextRendering += OnEndContextRendering;
        }
        
        void OnDisable()
        {
            RenderPipelineManager.endContextRendering -= OnEndContextRendering;
        }

        void OnEndContextRendering(ScriptableRenderContext ctx, List<Camera> cameras)
        {
            if (!IsValid)
                return;
            
            CommandBuffer cmd = CommandBufferPool.Get(k_profilerTag);
            cmd.Clear();

            Present(cmd);
            
            ctx.ExecuteCommandBuffer(cmd);
            
            cmd.Clear();
            CommandBufferPool.Release(cmd);
            
            ctx.Submit();
        }

        void Present(CommandBuffer cmd)
        {
            CoreUtils.SetRenderTarget(cmd, m_target);
            
            var srcScaleBias = new Vector4(1f, 1f, 0f, 0f);
            var dstScaleBias = PresenterStrategy.DoStrategy(m_strategy, sourceSize, targetSize);
            Blitter.BlitQuad(cmd, m_source, srcScaleBias, dstScaleBias, 0, true);
        }
    }
}
