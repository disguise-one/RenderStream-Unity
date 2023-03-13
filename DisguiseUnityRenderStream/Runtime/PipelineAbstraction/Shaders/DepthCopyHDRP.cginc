#ifndef DEPTH_COPY_HDRP_INCLUDED
#define DEPTH_COPY_HDRP_INCLUDED

#define REQUIRE_DEPTH_TEXTURE

#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
#include "Packages/com.unity.render-pipelines.high-definition/Runtime/ShaderLibrary/ShaderVariables.hlsl"

// Define for DepthCopyNodes.cginc
#define GetDepthForDisguise(uv) SampleCameraDepth(uv)

// Defines depth operations
#include "DepthCopyNodes.cginc"

// Contains vertex and fragment functions
#include "DepthCopyCommon.cginc"

#endif
