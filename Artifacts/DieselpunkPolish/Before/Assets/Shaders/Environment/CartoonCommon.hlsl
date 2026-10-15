#ifndef LEVIATHAN_CARTOON_COMMON_INCLUDED
#define LEVIATHAN_CARTOON_COMMON_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Packing.hlsl"

half3 CartoonLighting(float3 positionWS, half3 normalWS, half3 albedo,
    half3 shadowTint, half contrast, float bands, float softness)
{
#if defined(_MAIN_LIGHT_SHADOWS_SCREEN)
    float4 shadowCoord = ComputeScreenPos(TransformWorldToHClip(positionWS));
#else
    float4 shadowCoord = TransformWorldToShadowCoord(positionWS);
#endif
    Light light = GetMainLight(shadowCoord);
    float diffuse = saturate((dot(normalWS, light.direction) + 0.25) / 1.25);
    float first = smoothstep(0.3 - softness, 0.3 + softness, diffuse);
    float second = smoothstep(0.7 - softness, 0.7 + softness, diffuse);
    float tone = bands < 2.5 ? first : first * 0.55 + second * 0.45;
    tone *= lerp(1.0, light.shadowAttenuation, contrast);
    half3 direct = light.color * lerp(lerp(1.0.xxx, shadowTint, contrast), 1.0.xxx, tone);
    half3 ambient = max(SampleSH(normalWS), 0.025.xxx) * 0.45;
    return albedo * (direct + ambient);
}

half4 CartoonDepthNormal(half3 normalWS)
{
#if defined(_GBUFFER_NORMALS_OCT)
    float2 oct = PackNormalOctQuadEncode(normalize(normalWS));
    return half4(PackFloat2To888(saturate(oct * 0.5 + 0.5)), 0);
#else
    return half4(normalize(normalWS), 0);
#endif
}

float3 _LightDirection;
float3 _LightPosition;

float4 CartoonShadowPosition(float3 positionWS, float3 normalWS)
{
#if defined(_CASTING_PUNCTUAL_LIGHT_SHADOW)
    float3 direction = normalize(_LightPosition - positionWS);
#else
    float3 direction = _LightDirection;
#endif
    return ApplyShadowClamping(TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, direction)));
}

#endif
