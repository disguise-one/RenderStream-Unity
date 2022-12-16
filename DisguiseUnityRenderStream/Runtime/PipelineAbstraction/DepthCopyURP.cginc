#ifndef DEPTH_COPY_URP_INCLUDED
#define DEPTH_COPY_URP_INCLUDED

#define REQUIRE_DEPTH_TEXTURE

#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

#define GetDepthForDisguise(uv) SampleSceneDepth(uv)
#include "DepthCopyNodes.cginc"

#include "DepthCopyCommon.cginc"

#endif
