#ifndef LEVIATHAN_GRASS_RENDER
#define LEVIATHAN_GRASS_RENDER
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Packing.hlsl"
#define UNITY_INDIRECT_DRAW_ARGS IndirectDrawIndexedArgs
#include "UnityIndirect.cginc"
#include "GrassData.hlsl"
StructuredBuffer<GrassBlade> _GrassBlades;
StructuredBuffer<uint> _GrassVisible;
float4x4 _TileToWorld;
float _PatchFade;
float2 _TerrainLodBlend;
float3 _LightDirection;
float3 _LightPosition;
CBUFFER_START(UnityPerMaterial)
float4 _RootColor, _TipColor, _Appearance, _Wind, _Player;
float4 _LodDistances, _DensityLods, _WidthScales;
float _WindNoiseScale, _ViewThickening, _UseCulling, _ShadowDistance;
CBUFFER_END

float2 GrassGradient(int2 p)
{
    float angle = GrassRandom(asuint(p.x) ^ GrassHash(asuint(p.y))) * 6.2831853;
    return float2(cos(angle), sin(angle));
}
float GrassNoise(float2 p)
{
    int2 cell = (int2)floor(p); float2 f = frac(p), u = f * f * (3 - 2 * f);
    float a = dot(GrassGradient(cell), f), b = dot(GrassGradient(cell + int2(1,0)), f - float2(1,0));
    float c = dot(GrassGradient(cell + int2(0,1)), f - float2(0,1));
    float d = dot(GrassGradient(cell + int2(1,1)), f - 1);
    return lerp(lerp(a,b,u.x), lerp(c,d,u.x), u.y);
}
struct GrassAttributes { float3 positionOS : POSITION; float2 uv : TEXCOORD0; uint instanceID : SV_InstanceID; };
struct GrassVaryings
{
    float4 positionCS : SV_POSITION;
    float3 positionWS : TEXCOORD0;
    half3 normalWS : TEXCOORD1;
    half3 color : TEXCOORD2;
    float4 rootHeight : TEXCOORD3;
    nointerpolation uint seed : TEXCOORD4;
    half fog : TEXCOORD5;
};
GrassVaryings GrassVertex(GrassAttributes input)
{
#if defined(LEVIATHAN_LEGACY_INDIRECT)
    uint instance = input.instanceID;
#else
    InitIndirectDrawArgs(0);
    uint instance = GetIndirectInstanceID(input.instanceID);
#endif
    GrassBlade b = _GrassBlades[_GrassVisible[instance]];
    float2 shape = UnpackGrassHalf2(b.shape), angleBend = UnpackGrassHalf2(b.directionBend);
    float3 root = mul(_TileToWorld, float4(b.position, 1)).xyz;
    float3 up = normalize(mul((float3x3)_TileToWorld, UnpackGrassNormal(b.normal)));
    up = normalize(lerp(float3(0,1,0), up, 0.65));
    float3 right = float3(cos(angleBend.x), 0, sin(angleBend.x));
    right = normalize(right - up * dot(right, up));
    float3 forward = normalize(cross(right, up));
    float t = input.uv.y;
    float2 windDir = normalize(_Wind.xy + float2(0.0001,0));
    float noise = GrassNoise(root.xz * _WindNoiseScale - windDir * _Time.y * _Wind.z * _WindNoiseScale);
    float strength = _Wind.w * (0.6 + noise * 0.8 + 0.12 * sin(_Time.y * 3 + GrassRandom(b.seed) * 6));
    float3 bend = forward * angleBend.y * shape.x + float3(windDir.x,0,windDir.y) * strength;
    float2 toPlayer = root.xz - _Player.xz;
    float push = saturate(1 - length(toPlayer) / max(_Player.w, 0.001));
    push *= saturate(1 - abs(root.y - _Player.y) / 2) * step(0.01, _Player.w);
    bend += float3(toPlayer.x,0,toPlayer.y) / max(length(toPlayer),0.001) * push * 0.55;
    // Quadratic Bezier: P0=root, P1=root+up*h/2, P2=root+up*h+bend.
    float3 center = root + up * shape.x * t + bend * t * t;
    float3 tangent = normalize(up * shape.x + 2 * bend * t);
    float3 facing = normalize(cross(right, tangent));
    float3 view = normalize(_WorldSpaceCameraPos - center);
    float3 billboardRight = normalize(cross(tangent, view) + right * 0.0001);
    if (dot(billboardRight, right) < 0) billboardRight = -billboardRight;
    float edgeOn = 1 - abs(dot(facing, view));
    float3 spread = lerp(right, billboardRight, edgeOn * _ViewThickening);
    float taper = pow(saturate(1 - t), 0.65);
    float rootDistance = distance(root, _WorldSpaceCameraPos);
    float terrainWeight = smoothstep(_LodDistances.y, _LodDistances.y + _LodDistances.w, rootDistance);
    float midWidth = max(smoothstep(_LodDistances.x, _LodDistances.x + _LodDistances.w, rootDistance), _TerrainLodBlend.x * terrainWeight);
    float farWidth = max(smoothstep(_LodDistances.y, _LodDistances.y + _LodDistances.w, rootDistance), _TerrainLodBlend.y * terrainWeight);
    float widthScale = _UseCulling > 0.5 ? lerp(lerp(1, _WidthScales.x, midWidth), _WidthScales.y, farWidth) : 1;
    float3 position = center + spread * input.positionOS.x * shape.y * widthScale * taper;
    float3 normal = normalize(facing + right * input.positionOS.x * _Appearance.w);
    normal = normalize(lerp(up, normal, saturate(t * 3)));
    GrassVaryings output;
    output.positionWS = position;
    output.positionCS = TransformWorldToHClip(position);
    output.normalWS = normal;
    half3 color = lerp(_RootColor.rgb, _TipColor.rgb, t);
    color = lerp(color, UnpackGrassColor(b.color) * lerp(0.55, 1.25, t), _Appearance.x);
    output.color = color * lerp(0.55, 1, sqrt(t)) * lerp(0.88, 1.08, GrassRandom(b.seed + 9));
    output.rootHeight = float4(root, t);
    output.seed = b.seed;
    output.fog = ComputeFogFactor(output.positionCS.z);
    return output;
}
void GrassClip(GrassVaryings input)
{
    float distanceToCamera = distance(input.rootHeight.xyz, _WorldSpaceCameraPos);
    float fade = _UseCulling > 0.5 ? saturate((_LodDistances.z - distanceToCamera) / max(_LodDistances.w, 1)) : 1;
    // Stable stochastic coverage: no transparent sorting or blended overdraw.
    uint2 pixel = (uint2)input.positionCS.xy;
    float threshold = GrassRandom(input.seed ^ GrassHash((pixel.x & 3) + (pixel.y & 3) * 4));
    clip(min(fade, _PatchFade) - threshold);
    // Match smooth density transitions without moving the surviving roots.
    float mid = smoothstep(_LodDistances.x, _LodDistances.x + _LodDistances.w, distanceToCamera);
    float far = smoothstep(_LodDistances.y, _LodDistances.y + _LodDistances.w, distanceToCamera);
    float terrainWeight = smoothstep(_LodDistances.y, _LodDistances.y + _LodDistances.w, distanceToCamera);
    mid = max(mid, _TerrainLodBlend.x * terrainWeight); far = max(far, _TerrainLodBlend.y * terrainWeight);
    float coverage = lerp(lerp(1, _DensityLods.x, mid), _DensityLods.y, far);
    float r = GrassRandom(input.seed + 19);
    float smoothing = 0.04 * max(4 * mid * (1 - mid), 4 * far * (1 - far));
    float dissolve = _UseCulling > 0.5 ? saturate((coverage - r) / max(smoothing, 0.00001)) : 1;
    clip(dissolve - threshold);
}
half4 GrassForward(GrassVaryings input, FRONT_FACE_TYPE face : FRONT_FACE_SEMANTIC) : SV_Target
{
    GrassClip(input);
    half3 n = normalize(input.normalWS) * IS_FRONT_VFACE(face, 1, -1);
#if defined(_MAIN_LIGHT_SHADOWS_SCREEN)
    float4 shadowCoord = ComputeScreenPos(TransformWorldToHClip(input.positionWS));
#else
    float4 shadowCoord = TransformWorldToShadowCoord(input.positionWS);
#endif
    Light light = GetMainLight(shadowCoord);
    half diffuse = saturate(dot(n, light.direction) * 0.65 + 0.35);
    half transmission = saturate(dot(-n, light.direction)) * _Appearance.y;
    half3 view = normalize(_WorldSpaceCameraPos - input.positionWS);
    half spec = pow(saturate(dot(n, normalize(light.direction + view))), 24) * _Appearance.z;
    half3 ambient = max(SampleSH(n), half3(0.055,0.065,0.05));
    half3 result = input.color * (ambient + light.color * (diffuse + transmission) * light.shadowAttenuation) + spec * light.color * light.shadowAttenuation;
    return half4(MixFog(result, input.fog), 1);
}
GrassVaryings GrassShadowVertex(GrassAttributes input)
{
    GrassVaryings output = GrassVertex(input);
    float3 lightDirection = _LightDirection;
#if defined(_CASTING_PUNCTUAL_LIGHT_SHADOW)
    lightDirection = normalize(_LightPosition - output.positionWS);
#endif
    output.positionCS = TransformWorldToHClip(ApplyShadowBias(output.positionWS, output.normalWS, lightDirection));
    output.positionCS = ApplyShadowClamping(output.positionCS);
    return output;
}
half4 GrassDepth(GrassVaryings input) : SV_Target { GrassClip(input); return 0; }
half4 GrassShadowDepth(GrassVaryings input) : SV_Target
{
    GrassClip(input);
    float fade = saturate((_ShadowDistance - distance(input.rootHeight.xyz, _WorldSpaceCameraPos)) / 4);
    uint2 pixel = (uint2)input.positionCS.xy;
    clip(fade - GrassRandom(input.seed ^ GrassHash((pixel.x & 3) + (pixel.y & 3) * 4)));
    return 0;
}
half4 GrassDepthNormal(GrassVaryings input, FRONT_FACE_TYPE face : FRONT_FACE_SEMANTIC) : SV_Target
{
    GrassClip(input);
    float3 normal = normalize(input.normalWS) * IS_FRONT_VFACE(face, 1, -1);
#if defined(_GBUFFER_NORMALS_OCT)
    float2 encoded = saturate(PackNormalOctQuadEncode(normal) * 0.5 + 0.5);
    return half4(PackFloat2To888(encoded), 0);
#else
    return half4(normal, 0);
#endif
}
#endif
