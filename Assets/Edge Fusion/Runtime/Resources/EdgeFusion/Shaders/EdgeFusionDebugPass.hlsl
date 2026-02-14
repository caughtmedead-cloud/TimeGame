#ifndef EDGE_FUSION_DEBUG_PASS_INCLUDED
#define EDGE_FUSION_DEBUG_PASS_INCLUDED

#include "EdgeFusionBlendLibrary.hlsl"


float4 FragDebug(EFVaryings i) : SV_Target
{
	UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
	float2 uv = UnityStereoTransformScreenSpaceTex(i.texcoord);

	float4 baseColor = SampleColor(uv);

	float4 oidA = SampleObjectData(uv);
	float id = oidA.x;
	if (abs(id) <= 1e-6) return baseColor;
	float3 color = HashToColor(floor(id));

	#if EDGE_FUSION_DEBUG_OBJECT_IDS
		return float4(color, 1.0);
	#endif

	float linearDepth = LinearEyeDepth(oidA.y, _ZBufferParams);
	if (linearDepth > MAX_BLEND_DISTANCE) return baseColor;

	#if EDGE_FUSION_DEBUG_DEPTH
		float depth01 = saturate(_DepthDebugMultiplier * linearDepth / MAX_BLEND_DISTANCE);
		return float4(saturate(depth01).xxx, 1.0);
	#endif

	#if EDGE_FUSION_DEBUG_EDGES
		float2 nearestEdgeUV;
		float foundEdge = FindEdgeNeighbour(uv, nearestEdgeUV);
		if (foundEdge > 0) {
			float edgeDistance = length( (nearestEdgeUV - uv) * _ScreenParams.xy);
			if (edgeDistance < 2.0) {
				return float4(saturate(1.0 - color.rgb), 1.0);
			}
		}
		return float4(color, 1.0);
	#endif

	#if EDGE_FUSION_DEBUG_BLEND_NORMALS
	    float3 normalA = GetNormal(oidA);
		return float4(normalA * 0.5 + 0.5, 1.0);
	#endif

	#if EDGE_FUSION_DEBUG_BLEND_PIXELS
		float2 uvNeighbour;
		float foundEdge = FindEdgeNeighbour(uv, uvNeighbour);
		float blendAmount = foundEdge * foundEdge;
		blendAmount *= (0.5 * GLOBAL_INTENSITY);
		color.rgb = blendAmount;
		return float4(color, 1.0);
	#endif

	#if EDGE_FUSION_DEBUG_SPECIAL_GROUP
		bool isSpecial = UnpackIdFromPacked(oidA.r) == 32;
		return isSpecial ? float4(1, 0.6, 0.1, 1) : baseColor;
	#endif

	return float4(color, 1.0);
}

#endif // EDGE_FUSION_DEBUG_PASS_INCLUDED