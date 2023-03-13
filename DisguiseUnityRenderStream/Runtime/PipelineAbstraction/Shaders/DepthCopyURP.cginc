#ifndef DEPTH_COPY_URP_INCLUDED
#define DEPTH_COPY_URP_INCLUDED

#define REQUIRE_DEPTH_TEXTURE

#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

// Define for DepthCopyNodes.cginc
#define GetDepthForDisguise(uv) SampleSceneDepth(uv)

// Defines depth operations
#include "DepthCopyNodes.cginc"

// Contains vertex and fragment functions
#include "DepthCopyCommon.cginc"

#endif
