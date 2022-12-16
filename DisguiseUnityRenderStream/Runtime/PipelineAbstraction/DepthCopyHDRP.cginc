#ifndef DEPTH_COPY_HDRP_INCLUDED
#define DEPTH_COPY_HDRP_INCLUDED

#define REQUIRE_DEPTH_TEXTURE

#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
#include "Packages/com.unity.render-pipelines.high-definition/Runtime/ShaderLibrary/ShaderVariables.hlsl"

#define GetDepthForDisguise(uv) SampleCameraDepth(uv)
#include "DepthCopyNodes.cginc"

#include "DepthCopyCommon.cginc"

#endif
