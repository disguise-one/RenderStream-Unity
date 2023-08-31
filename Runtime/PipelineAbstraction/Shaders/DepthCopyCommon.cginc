#ifndef DEPTH_COPY_COMMON_INCLUDED
#define DEPTH_COPY_COMMON_INCLUDED

struct Attributes
{
    uint vertexID : SV_VertexID;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct Varyings
{
    float4 positionCS : SV_Position;
    float2 texcoord   : TEXCOORD0;
    UNITY_VERTEX_OUTPUT_STEREO
};

Varyings Vert(Attributes input)
{
    Varyings output;
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
    output.positionCS = GetFullScreenTriangleVertexPosition(input.vertexID);
    output.texcoord = GetFullScreenTriangleTexCoord(input.vertexID);
    return output;
}

float2 FragUV(Varyings input)
{
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
    return input.texcoord.xy;
    // Up to this point this is minimal screen-space blit boilerplate
}

float FragRaw(Varyings input) : SV_Target
{
    return SceneDepth_Raw(FragUV(input));
}

float FragEye(Varyings input) : SV_Target
{
    return SceneDepth_Eye(FragUV(input));
}

float FragLinear01(Varyings input) : SV_Target
{
    return SceneDepth_Linear01(FragUV(input));
}

#endif
