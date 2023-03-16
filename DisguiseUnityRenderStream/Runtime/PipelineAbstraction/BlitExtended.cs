using System;
using System.Linq;
using Disguise.RenderStream;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.Rendering;

/// <summary>
/// <para>
/// Based on:
/// <see cref="UnityEngine.Rendering.Blitter"/>,
/// com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl,
/// com.unity.render-pipelines.high-definition/Runtime/ShaderLibrary/Blit.shader,
/// com.unity.render-pipelines.universal/Shaders/Utils/CoreBlit.shader.
/// </para>
///
/// <para>
/// This version of <see cref="UnityEngine.Rendering.Blitter"/> performs color space
/// conversion at the same time as the blit.
/// </para>
/// </summary>
class BlitExtended
{
    public enum ColorSpaceConversion
    {
        None,
        LinearToSRGB,
        SRGBToLinear
    }

    enum Geometry
    {
        FullscreenTriangle,
        Quad
    }
    
    /// <summary>
    /// The strings are identical to <see cref="UnityEngine.Rendering.Blitter.BlitShaderIDs"/>
    /// </summary>
    static class ShaderIDs
    {
        public static readonly int _BlitTexture = Shader.PropertyToID("_BlitTexture");
        public static readonly int _BlitScaleBias = Shader.PropertyToID("_BlitScaleBias");
        public static readonly int _BlitScaleBiasRt = Shader.PropertyToID("_BlitScaleBiasRt");
    }
    
    public static BlitExtended Instance { get; }

    static BlitExtended()
    {
        Instance = new BlitExtended();
    }

    public static ColorSpaceConversion GetSRGBConversion(SRGBConversions.Conversion conversion) => conversion switch
    {
        SRGBConversions.Conversion.None or
            SRGBConversions.Conversion.HardwareLinearToSRGB or
            SRGBConversions.Conversion.HardwareSRGBToLinear => ColorSpaceConversion.None,
        
        SRGBConversions.Conversion.SoftwareLinearToSRGB => ColorSpaceConversion.LinearToSRGB,
        
        SRGBConversions.Conversion.SoftwareSRGBToLinear => ColorSpaceConversion.SRGBToLinear,
        
        _ => throw new ArgumentOutOfRangeException()
    };

    static readonly int k_ColorSpaceConversionCount = Enum.GetValues(typeof(ColorSpaceConversion)).Cast<int>().Max() + 1;

    /// <remarks>
    /// This function and the order of the Pass { ... } blocks in <see cref="ShaderName"/> have to match
    /// </remarks>
    static int GetPass(Geometry geometry, ColorSpaceConversion conversion)
    {
        return (int)geometry * k_ColorSpaceConversionCount + (int)conversion;
    }
    
#if UNITY_PIPELINE_HDRP && HDRP_VERSION_SUPPORTED
    public const string ShaderName = "Hidden/Disguise/RenderStream/BlitExtendedHDRP";
#elif UNITY_PIPELINE_URP && URP_VERSION_SUPPORTED
    public const string ShaderName = "Hidden/Disguise/RenderStream/BlitExtendedURP";
#else
    public const string ShaderName = null;
#endif

    readonly Material m_Blit;
    readonly MaterialPropertyBlock m_PropertyBlock = new MaterialPropertyBlock();

    BlitExtended()
    {
#if !(UNITY_PIPELINE_HDRP && HDRP_VERSION_SUPPORTED) && !(UNITY_PIPELINE_URP && URP_VERSION_SUPPORTED)
        Debug.LogError($"No supported render pipeline was found for {nameof(BlitExtended)}.");
#endif
            
        var shader = Shader.Find(ShaderName);
        if (shader != null)
            m_Blit = CoreUtils.CreateEngineMaterial(shader);
        
        Assert.IsTrue(shader != null && m_Blit != null, $"Couldn't load the shader resources for {nameof(BlitExtended)}");
    }
    
    /// <summary>
    /// Similar to <see cref="UnityEngine.Rendering.Blitter.BlitTexture(CommandBuffer, RTHandle, Vector4, float, bool)"/>
    /// </summary>
    public void BlitTexture(CommandBuffer cmd, RenderTexture source, RenderTexture destination, ColorSpaceConversion conversion, ScaleBias scaleBias)
    {
        m_PropertyBlock.SetTexture(ShaderIDs._BlitTexture, source);
        m_PropertyBlock.SetVector(ShaderIDs._BlitScaleBias, scaleBias.Vector);

        var shaderPass = GetPass(Geometry.FullscreenTriangle, conversion);
        
        CoreUtils.DrawFullScreen(cmd, m_Blit, destination, m_PropertyBlock, shaderPass);
    }
    
    /// <summary>
    /// Similar to <see cref="UnityEngine.Rendering.Blitter.BlitQuad"/>
    /// </summary>
    public void BlitQuad(CommandBuffer cmd, RenderTexture source, ColorSpaceConversion conversion, ScaleBias srcScaleBias, ScaleBias dstScaleBias)
    {
        m_PropertyBlock.SetTexture(ShaderIDs._BlitTexture, source);
        m_PropertyBlock.SetVector(ShaderIDs._BlitScaleBias, srcScaleBias.Vector);
        m_PropertyBlock.SetVector(ShaderIDs._BlitScaleBiasRt, dstScaleBias.Vector);
        
        var shaderPass = GetPass(Geometry.Quad, conversion);
        
        DrawQuad(cmd, m_Blit, shaderPass);
    }
    
    /// <summary>
    /// Similar to <see cref="UnityEngine.Rendering.Blitter.DrawQuad"/>
    /// </summary>
    void DrawQuad(CommandBuffer cmd, Material material, int shaderPass)
    {
        cmd.DrawProcedural(Matrix4x4.identity, material, shaderPass, MeshTopology.Quads, 4, 1, m_PropertyBlock);
    }
}
