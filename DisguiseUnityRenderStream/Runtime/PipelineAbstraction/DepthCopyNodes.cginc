#ifndef DEPTH_COPY_NODES_INCLUDED
#define DEPTH_COPY_NODES_INCLUDED

// Based on SceneDepthNode.cs
// GetDepthForDisguise should be pre-defined

#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"

float SceneDepth_Linear01(float2 uv)
{
    return Linear01Depth(GetDepthForDisguise(uv.xy), _ZBufferParams);
}

float SceneDepth_Raw(float2 uv)
{
    return GetDepthForDisguise(uv.xy);
}

float SceneDepth_Eye(float2 uv)
{
    if (unity_OrthoParams.w == 1.0)
    {
        return LinearEyeDepth(ComputeWorldSpacePosition(uv.xy, GetDepthForDisguise(uv.xy), UNITY_MATRIX_I_VP), UNITY_MATRIX_V);
    }
    else
    {
        return LinearEyeDepth(GetDepthForDisguise(uv.xy), _ZBufferParams);
    }
}

#if DEPTH_COPY_RAW
#define SceneDepth_Auto(uv) SceneDepth_Raw(uv)
#elif DEPTH_COPY_EYE
#define SceneDepth_Auto(uv) SceneDepth_Eye(uv)
#elif DEPTH_COPY_LINEAR01
#define SceneDepth_Auto(uv) SceneDepth_Linear01(uv)
#endif

#endif
