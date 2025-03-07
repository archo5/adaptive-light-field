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
	float3 color;
	int vecCount;
	LPVec vecs[27];
};
StructuredBuffer<LPoint> LightVolPoints;
StructuredBuffer<uint> LightVolGrid;
StructuredBuffer<uint> LightVolPtLists;
float4 LightVolMin;
float4 LightVolMax;
int LightVolGridSizeX;
int LightVolGridSizeY;
int LightVolGridSizeZ;

v2f vert(appdata v)
{
	v2f o;
	o.vertex = UnityObjectToClipPos(v.vertex);
	o.worldPos = mul(unity_ObjectToWorld, v.vertex);
	o.uv = TRANSFORM_TEX(v.uv, _MainTex);
	UNITY_TRANSFER_FOG(o,o.vertex);
	return o;
}

float CalcWeight(LPoint LP, float3 wpos)
{
	float wt = 0, wtsum = 0;
	float3 c2p = wpos - LP.pos;
	float3 c2pdir = normalize(c2p);
	float c2plen = length(c2p);
	float c2plensq = c2plen * c2plen;
	for (int i = 0; i < LP.vecCount; i++)
	{
		LPVec V = LP.vecs[i];
		float lensq = V.len * V.len;
		float dotf = saturate(dot(V.dir, c2pdir));

		//dotf = pow(dotf, 4.);
		dotf *= dotf;
		dotf *= dotf;

		//float distf = 1 - saturate(c2plensq / lensq);
		float distf = 1 - saturate(c2plen / V.len);
		//float distf = 1 / (0.01 + c2plensq / lensq);
		//distf = pow(distf, 0.1);
		float q = distf;
		float w = dotf;
		//retq = max(retq, q * w);
		wt += w * q;
		wtsum += w;
	}
	return wt / wtsum;
}

fixed4 frag(v2f input) : SV_Target
{
	fixed4 col = tex2D(_MainTex, input.uv);
	// apply lighting
	if (input.worldPos.x >= LightVolMin.x && input.worldPos.x <= LightVolMax.x &&
		input.worldPos.y >= LightVolMin.y && input.worldPos.y <= LightVolMax.y &&
		input.worldPos.z >= LightVolMin.z && input.worldPos.z <= LightVolMax.z)
	{
		float3 lighting = 0;
		float totalw = 0;
		uint count, dummy;
		LightVolPoints.GetDimensions(count, dummy);
#if 0
		for (uint i = 0; i < count; i++)
		{
#else
		int gridCellX = clamp(int((input.worldPos.x - LightVolMin.x) / (LightVolMax.x - LightVolMin.x) * LightVolGridSizeX), 0, LightVolGridSizeX - 1);
		int gridCellY = clamp(int((input.worldPos.y - LightVolMin.y) / (LightVolMax.y - LightVolMin.y) * LightVolGridSizeY), 0, LightVolGridSizeY - 1);
		int gridCellZ = clamp(int((input.worldPos.z - LightVolMin.z) / (LightVolMax.z - LightVolMin.z) * LightVolGridSizeZ), 0, LightVolGridSizeZ - 1);
		int gridCellNum = gridCellX + gridCellY * LightVolGridSizeX + gridCellZ * LightVolGridSizeX * LightVolGridSizeY;
		uint gridCellData = LightVolGrid[gridCellNum];
		uint ploff = gridCellData & 0xfffff;
		uint plcount = gridCellData >> 20;
		for (uint p = 0; p < plcount; p++)
		{
			uint i = LightVolPtLists[p + ploff];
#endif
			LPoint LP = LightVolPoints[i];
			if (length(LP.pos - input.worldPos) > LP.radius) continue;
#if 0
			float w = 1 - length(LP.pos - input.worldPos) / LP.radius;
			w *= w;
#else
			float w = CalcWeight(LP, input.worldPos);
			w *= w;
#endif
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
