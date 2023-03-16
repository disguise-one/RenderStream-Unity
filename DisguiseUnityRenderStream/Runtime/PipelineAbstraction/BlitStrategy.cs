using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace Disguise.RenderStream
{
    /// <summary>
    /// <para>
    /// Represents a transformation applied to a source or destination texture inside a blit shader.
    /// </para>
    /// 
    /// <para>
    /// The transformation applied is:
    /// TransformedUV = OriginalUV * <see cref="Scale"/> + <see cref="Bias"/>
    /// </para>
    /// </summary>
    public readonly struct ScaleBias
    {
        /// <summary>
        /// UVs remain unchanged.
        /// </summary>
        public static ScaleBias Identity { get; } = new ScaleBias(1f, 1f, 0f, 0f);
        
        /// <summary>
        /// The UVs are flipped vertically.
        /// </summary>
        public static ScaleBias FlippedY { get; } = FlipY(Identity);

        /// <summary>
        /// Blit shaders expect a packed Vector4.
        /// </summary>
        public Vector4 Vector { get; }

        public float ScaleX => Vector.x;
        public float ScaleY => Vector.y;
        public float BiasX => Vector.z;
        public float BiasY => Vector.w;

        public Vector2 Scale => new Vector2(ScaleX, ScaleY);
        public Vector2 Bias => new Vector2(BiasX, BiasY);

        public ScaleBias(float scaleX, float scaleY, float biasX, float biasY)
        {
            Vector = new Vector4(scaleX, scaleY, biasX, biasY);
        }

        /// <summary>
        /// Returns a copy of <paramref name="scaleBias"/> flipped vertically.
        /// </summary>
        public static ScaleBias FlipY(ScaleBias scaleBias) =>
            new ScaleBias(scaleBias.ScaleX, -scaleBias.ScaleY, scaleBias.BiasX, 1f - scaleBias.BiasY);
    }
    
    /// <summary>
    /// Strategy calculations for the <see cref="UnityEngine.Rendering.Blitter"/> and <see cref="BlitExtended"/> APIs.
    /// </summary>
    static class BlitStrategy
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
        /// <returns>A <see cref="ScaleBias"/> to apply to the blit destination texture's UV coordinates.</returns>
        public static ScaleBias DoStrategy(Strategy strategy, Vector2 srcSize, Vector2 dstSize)
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
        
        static ScaleBias Stretch(Vector2 srcSize, Vector2 dstSize)
        {
            return ScaleBias.Identity;
        }
        
        static ScaleBias NoResize(Vector2 srcSize, Vector2 dstSize)
        {
            var scale = srcSize / dstSize;
            var offset = CenterUVOffset(scale);
            
            return new ScaleBias(scale.x, scale.y, offset.x, offset.y);
        }
        
        static ScaleBias FitWidth(Vector2 srcSize, Vector2 dstSize)
        {
            var yScale = InverseAspectRatio(srcSize) * AspectRatio(dstSize);
            var yOffset = CenterUVOffset(yScale);
            
            return new ScaleBias(1f, yScale, 0f, yOffset);
        }
        
        static ScaleBias FitHeight(Vector2 srcSize, Vector2 dstSize)
        {
            var xScale = AspectRatio(srcSize) * InverseAspectRatio(dstSize);
            var xOffset = CenterUVOffset(xScale);
            
            return new ScaleBias(xScale, 1f, xOffset, 0f);
        }
        
        static ScaleBias Letterbox(Vector2 srcSize, Vector2 dstSize)
        {
            var scrAspect = AspectRatio(srcSize);
            var dstAspect = AspectRatio(dstSize);

            if (scrAspect > dstAspect)
                return FitWidth(srcSize, dstSize);
            else
                return FitHeight(srcSize, dstSize);
        }
        
        static ScaleBias Fill(Vector2 srcSize, Vector2 dstSize)
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
}
