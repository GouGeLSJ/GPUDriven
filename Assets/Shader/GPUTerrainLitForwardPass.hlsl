#ifndef UNIVERSAL_GPUTERRAIN_FORWARD_LIT_PASS_INCLUDED
#define UNIVERSAL_GPUTERRAIN_FORWARD_LIT_PASS_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

struct Attributes
{
    float4 positionOS   : POSITION;
    float2 texcoord     : TEXCOORD0;
    half4 color         : COLOR;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct Varyings
{
    float4 uvMainAndLM : TEXCOORD0; // xy: control, zw: lightmap
#ifndef TERRAIN_SPLAT_BASEPASS
    float4 uvSplat01 : TEXCOORD1; // xy: splat0, zw: splat1
    float4 uvSplat23 : TEXCOORD2; // xy: splat2, zw: splat3
#endif

    float3 normal : TEXCOORD3;
    float3 viewDir : TEXCOORD4;
    half3 vertexSH : TEXCOORD5; // SH

    half4 fogFactorAndVertexLight : TEXCOORD6; // x: fogFactor, yzw: vertex light
    float3 positionWS : TEXCOORD7;
#if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR)
    float4 shadowCoord              : TEXCOORD8;
#endif
    float4 clipPos : SV_POSITION;
    UNITY_VERTEX_OUTPUT_STEREO
};

struct NodeInfoData
{
    float4 rect;
    int mipmap;
    int neighbor;
};
StructuredBuffer<NodeInfoData> _AllInstancesTransformBuffer;
StructuredBuffer<uint> _VisibleInstanceOnlyTransformIDBuffer;

TEXTURE2D(_TerrainHeightmapTexture);
TEXTURE2D(_TerrainNormalmapTexture);
SAMPLER(sampler_TerrainNormalmapTexture);
float4 terrainParam;

void InitializeInputData(Varyings input, half3 normalTS, out InputData inputData)
{
    inputData = (InputData)0;
    inputData.positionWS = input.positionWS;
    half3 viewDirWS = input.viewDir;
    
#if SHADER_HINT_NICE_QUALITY
    viewDirWS = SafeNormalize(viewDirWS);
#endif
    
    half3 normalWS = TransformObjectToWorldNormal(normalize(SAMPLE_TEXTURE2D(_TerrainNormalmapTexture, sampler_TerrainNormalmapTexture, input.positionWS.xz / terrainParam.x).rgb * 2 - 1));
    half3 tangentWS = cross(GetObjectToWorldMatrix()._13_23_33, normalWS);
    inputData.normalWS = TransformTangentToWorld(normalTS, half3x3(-tangentWS, cross(normalWS, tangentWS), normalWS));
    half3 SH = SampleSH(inputData.normalWS.xyz);
    
    inputData.normalWS = NormalizeNormalPerPixel(inputData.normalWS);
    inputData.viewDirectionWS = viewDirWS;

    inputData.shadowCoord = TransformWorldToShadowCoord(inputData.positionWS);

    inputData.fogCoord = input.fogFactorAndVertexLight.x;
    inputData.vertexLighting = input.fogFactorAndVertexLight.yzw;
    inputData.bakedGI = SAMPLE_GI(input.uvMainAndLM.zw, SH, inputData.normalWS);
}

///////////////////////////////////////////////////////////////////////////////
//                  Vertex and Fragment functions                            //
///////////////////////////////////////////////////////////////////////////////

// Used in Standard (Physically Based) shader
Varyings LitPassVertex(Attributes input, uint instanceID : SV_InstanceID)
{
    Varyings output = (Varyings)0;
    NodeInfoData infoData = _AllInstancesTransformBuffer[_VisibleInstanceOnlyTransformIDBuffer[instanceID]];
    float4 rect = infoData.rect;
    int neighbor = infoData.neighbor;
    float2 diff = 0;
    if (neighbor & 1)
    {
        diff.x = -input.color.r;
    }
    if (neighbor & 2)
    {
        diff.x = -input.color.g;
    }
    if (neighbor & 4)
    {
        diff.y = -input.color.b;
    }
    if (neighbor & 8)
    {
        diff.y = -input.color.a;
    }

    float2 positionWS = rect.zw * 0.25 * (input.positionOS.xz + diff) + rect.xy; //we pre-transform to posWS in C# now
    VertexPositionInputs vertexInput;
    vertexInput.positionWS = TransformObjectToWorld(positionWS.xyy);
    float height = UnpackHeightmap(_TerrainHeightmapTexture.Load(int3(vertexInput.positionWS.xz , 0)));
    float3 normalWS = _TerrainNormalmapTexture.Load(int3(vertexInput.positionWS.xz, 0)).rgb * 2 - 1;
    vertexInput.positionWS.y = height * terrainParam.y * 2;
    vertexInput.positionVS = TransformWorldToView(vertexInput.positionWS);
    vertexInput.positionCS = TransformWorldToHClip(vertexInput.positionWS);
    
    half3 viewDirWS = GetCameraPositionWS() - vertexInput.positionWS;
#if !SHADER_HINT_NICE_QUALITY
    viewDirWS = SafeNormalize(viewDirWS);
#endif
    output.normal = normalWS;
    output.viewDir = viewDirWS;
    output.vertexSH = SampleSH(output.normal);
    
    output.fogFactorAndVertexLight.x = ComputeFogFactor(vertexInput.positionCS.z);
    output.fogFactorAndVertexLight.yzw = VertexLighting(vertexInput.positionWS, output.normal.xyz);
    output.positionWS = vertexInput.positionWS;
    output.clipPos = vertexInput.positionCS;
    
    return output;
}

// Used in Standard (Physically Based) shader
half4 LitPassFragment(Varyings input) : SV_Target
{
    InputData inputData;
    InitializeInputData(input, half3(0, 0, 1), inputData);
    //return half4(inputData.normalWS * 0.5 + 0.5, 1);
    half3 albedo = 1;
    float metallic = 0;
    float smoothness = 0.5;
    float occlusion = 1;
    float alpha = 1;
    half4 color = UniversalFragmentPBR(inputData, albedo, metallic, /* specular */half3(0.0h, 0.0h, 0.0h), smoothness, occlusion, /* emission */half3(0, 0, 0), alpha);
    
    color.rgb = MixFog(color.rgb, inputData.fogCoord);

    return color;
}

float3 _LightDirection;

struct AttributesLean
{
    float4 position : POSITION;
    float3 normalOS : NORMAL;
#ifdef _ALPHATEST_ON
	float2 texcoord     : TEXCOORD0;
#endif
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct VaryingsLean
{
    float4 clipPos : SV_POSITION;
#ifdef _ALPHATEST_ON
    float2 texcoord     : TEXCOORD0;
#endif
    UNITY_VERTEX_OUTPUT_STEREO
};
VaryingsLean ShadowPassVertex(AttributesLean v, uint instanceID : SV_InstanceID)
{
    float4 rect = _AllInstancesTransformBuffer[_VisibleInstanceOnlyTransformIDBuffer[instanceID]].rect;
    float2 posXZ = rect.zw * 0.25 * v.position.xz + rect.xy; //we pre-transform to posWS in C# now
    VaryingsLean o = (VaryingsLean) 0;

    float3 positionWS = TransformObjectToWorld(posXZ.xyy);
    float height = UnpackHeightmap(_TerrainHeightmapTexture.Load(int3(positionWS.xz, 0)));
    positionWS.y = height * terrainParam.y * 2;
    float3 normalWS = _TerrainNormalmapTexture.Load(int3(positionWS.xz, 0)).rgb * 2 - 1;

    float4 clipPos = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, _LightDirection));

#if UNITY_REVERSED_Z
    clipPos.z = min(clipPos.z, clipPos.w * UNITY_NEAR_CLIP_VALUE);
#else
    clipPos.z = max(clipPos.z, clipPos.w * UNITY_NEAR_CLIP_VALUE);
#endif

    o.clipPos = clipPos;

#ifdef _ALPHATEST_ON
	o.texcoord = v.texcoord;
#endif

    return o;
}

half4 ShadowPassFragment(VaryingsLean IN) : SV_TARGET
{
#ifdef _ALPHATEST_ON
	ClipHoles(IN.texcoord);
#endif
    return 0;
}

#endif
