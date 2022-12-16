Shader "Hidden/Disguise/RenderStream/DepthCopyHDRP"
{
    Properties
    {
    }

    SubShader
    {
        PackageRequirements
        {
            "com.unity.render-pipelines.high-definition" : "13.1.8"
        }
        
        Tags
        {
            "RenderPipeline" = "HDRenderPipeline"
        }

        Pass
        {
            Name "Depth Copy"

            Cull Off
            ZTest Always
            ZWrite Off
            Blend Off

            HLSLPROGRAM
            #pragma fragment Frag
            #pragma vertex Vert
            #include "DepthCopyHDRP.cginc"
            ENDHLSL
        }
    }
    Fallback Off
}
