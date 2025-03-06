Shader "LightVolSampler"
{
	Properties
	{
		_MainTex("Texture", 2D) = "white" {}
	}
	SubShader
	{
		Tags { "RenderType" = "Opaque" }
		LOD 100

		Pass
		{
			CGPROGRAM
#pragma target 5.0
#pragma vertex vert
#pragma fragment frag
// make fog work
#pragma multi_compile_fog

#include "UnityCG.cginc"

struct appdata
{
	float4 vertex : POSITION;
	float2 uv : TEXCOORD0;
};

struct v2f
{
	float2 uv : TEXCOORD0;
	UNITY_FOG_COORDS(1)
	float4 vertex : SV_POSITION;
	float3 worldPos : WORLDPOS;
};

sampler2D _MainTex;
float4 _MainTex_ST;

struct LPVec
{
	float3 dir;
	float len;
};
struct LPoint
{
	float3 pos;
	float radius;
	float4 color;
	LPVec vecs[27];
};
StructuredBuffer<LPoint> LightVolPoints;

v2f vert(appdata v)
{
	v2f o;
	o.vertex = UnityObjectToClipPos(v.vertex);
	o.worldPos = mul(unity_ObjectToWorld, v.vertex);
	o.uv = TRANSFORM_TEX(v.uv, _MainTex);
	UNITY_TRANSFER_FOG(o,o.vertex);
	return o;
}

fixed4 frag(v2f input) : SV_Target
{
	fixed4 col = tex2D(_MainTex, input.uv);
	// apply lighting
	{
		float3 lighting = 0;
		float totalw = 0;
		uint count, dummy;
		LightVolPoints.GetDimensions(count, dummy);
		for (uint i = 0; i < count; i++)
		{
			LPoint LP = LightVolPoints[i];
			if (length(LP.pos - input.worldPos) > LP.radius)
				continue;
			float w = 1 - length(LP.pos - input.worldPos) / LP.radius;
			lighting += LP.color.rgb * w;
			totalw += w;
		}
		lighting /= totalw;
		col.rgb *= lighting;
	}
	UNITY_APPLY_FOG(input.fogCoord, col);
	return col;
}
			ENDCG
		}
	}
}
