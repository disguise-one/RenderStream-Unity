using System;
using Disguise.RenderStream;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.Rendering;

class BlitExtended
{
    public enum ColorSpaceConversion
    {
        None,
        LinearToSRGB,
        SRGBToLinear,
        Max
    }

    enum Geometry
    {
        FullscreenTriangle,
        Quad
    }
    
    static class ShaderIDs
    {
        public static readonly int _BlitTexture = Shader.PropertyToID("_BlitTexture");
        public static readonly int _BlitScaleBias = Shader.PropertyToID("_BlitScaleBias");
        public static readonly int _BlitScaleBiasRt = Shader.PropertyToID("_BlitScaleBiasRt");
    }
    
    static BlitExtended s_Instance;

    public static BlitExtended Instance
    {
        get
        {
            if (s_Instance == null)
                s_Instance = new BlitExtended();

            return s_Instance;
        }
    }

    public static Vector4 IdentityScaleBias { get; } = new Vector4(1f, 1f, 0f, 0f);
    public static Vector4 FlippedYScaleBias { get; } = FlipYScaleBias(IdentityScaleBias);
    public static Vector4 FlipYScaleBias(Vector4 scaleBias) => new Vector4(scaleBias.x, -scaleBias.y, scaleBias.z, 1f - scaleBias.w);

    public static ColorSpaceConversion GetSRGBConversion(SRGBConversions.Conversion conversion) => conversion switch
    {
        SRGBConversions.Conversion.None => ColorSpaceConversion.None,
        SRGBConversions.Conversion.LinearToSRGB => ColorSpaceConversion.LinearToSRGB,
        SRGBConversions.Conversion.SRGBToLinear => ColorSpaceConversion.SRGBToLinear,
        _ => throw new ArgumentOutOfRangeException()
    };

    static int GetPass(Geometry geometry, ColorSpaceConversion conversion)
    {
        return (int)geometry * (int)ColorSpaceConversion.Max + (int)conversion;
    }
    
#if UNITY_PIPELINE_HDRP && HDRP_VERSION_SUPPORTED
    public const string k_ShaderName = "Hidden/Disguise/RenderStream/BlitExtendedHDRP";
#elif UNITY_PIPELINE_URP && URP_VERSION_SUPPORTED
    public const string k_ShaderName = "Hidden/Disguise/RenderStream/BlitExtendedURP";
#else
    public const string k_ShaderName = null;
#endif

    Material m_Blit;
    MaterialPropertyBlock m_PropertyBlock = new MaterialPropertyBlock();

    BlitExtended()
    {
#if !(UNITY_PIPELINE_HDRP && HDRP_VERSION_SUPPORTED) && !(UNITY_PIPELINE_URP && URP_VERSION_SUPPORTED)
        Debug.LogError($"No supported render pipeline was found for {nameof(BlitExtended)}.");
#endif
            
        var shader = Shader.Find(k_ShaderName);
        if (shader != null)
            m_Blit = CoreUtils.CreateEngineMaterial(shader);
        
        Assert.IsTrue(shader != null && m_Blit != null, $"Couldn't load the shader resources for {nameof(BlitExtended)}");
    }
    
    public void BlitTexture(CommandBuffer cmd, RenderTexture source, RenderTexture destination, ColorSpaceConversion conversion, Vector4 scaleBias)
    {
        m_PropertyBlock.SetTexture(ShaderIDs._BlitTexture, source);
        m_PropertyBlock.SetVector(ShaderIDs._BlitScaleBias, scaleBias);

        var shaderPass = GetPass(Geometry.FullscreenTriangle, conversion);
        
        CoreUtils.DrawFullScreen(cmd, m_Blit, destination, m_PropertyBlock, shaderPass);
    }
    
    public void BlitQuad(CommandBuffer cmd, RenderTexture source, ColorSpaceConversion conversion, Vector4 srcScaleBias, Vector4 dstScaleBias)
    {
        m_PropertyBlock.SetTexture(ShaderIDs._BlitTexture, source);
        m_PropertyBlock.SetVector(ShaderIDs._BlitScaleBias, srcScaleBias);
        m_PropertyBlock.SetVector(ShaderIDs._BlitScaleBiasRt, dstScaleBias);
        
        var shaderPass = GetPass(Geometry.Quad, conversion);
        
        DrawQuad(cmd, m_Blit, shaderPass);
    }
    
    void DrawQuad(CommandBuffer cmd, Material material, int shaderPass)
    {
        cmd.DrawProcedural(Matrix4x4.identity, material, shaderPass, MeshTopology.Quads, 4, 1, m_PropertyBlock);
    }
}
