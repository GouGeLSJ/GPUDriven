Shader "Custom/UnlitTexture"
{
	Properties
	{
		[MainColor] _BaseColor("BaseColor", Color) = (1,1,1,1)
		[MainTexture] _BaseMap("BaseMap", 2D) = "white" {}
	}

		// Universal Render Pipeline subshader. If URP is installed this will be used.
		SubShader
	{
		Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalRenderPipeline"}

		Pass
		{
			Tags { "LightMode" = "UniversalForward" }

			HLSLPROGRAM
			#pragma vertex vert
			#pragma fragment frag

			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

			struct Attributes
			{
				float4 positionOS   : POSITION;
				float2 uv           : TEXCOORD0;
			};

			struct Varyings
			{
				float2 uv           : TEXCOORD0;
				float4 positionHCS  : SV_POSITION;
			};

			TEXTURE2D(_BaseMap);
			SAMPLER(sampler_BaseMap);

			CBUFFER_START(UnityPerMaterial)
			float4 _BaseMap_ST;
			half4 _BaseColor;
			StructuredBuffer<float3> _AllInstancesTransformBuffer;
			StructuredBuffer<uint> _VisibleInstanceOnlyTransformIDBuffer;
			CBUFFER_END

			Varyings vert(Attributes IN, uint instanceID : SV_InstanceID)
			{
				Varyings OUT;
				float3 positionWS = _AllInstancesTransformBuffer[_VisibleInstanceOnlyTransformIDBuffer[instanceID]];//we pre-transform to posWS in C# now

				OUT.positionHCS = TransformWorldToHClip(IN.positionOS.xyz + positionWS);
				OUT.uv = TRANSFORM_TEX(IN.uv, _BaseMap);
				return OUT;
			}

			half4 frag(Varyings IN) : SV_Target
			{
				return 1;
			}
			ENDHLSL
		}
	}
}