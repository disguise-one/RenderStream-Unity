Shader "Hidden/Disguise/RenderStream/DepthCopyURP"
{
    Properties
    {
    }

    SubShader
    {
        PackageRequirements
        {
            "com.unity.render-pipelines.universal" : "13.1.8"
        }
        
        Tags
        {
            "RenderPipeline" = "UniversalRenderPipeline"
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
            #include "DepthCopyURP.cginc"
            ENDHLSL
        }
    }
    Fallback Off
}
