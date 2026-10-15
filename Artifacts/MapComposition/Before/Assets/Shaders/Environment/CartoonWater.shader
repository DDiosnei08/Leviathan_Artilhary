Shader "Leviathan/Cartoon Water"
{
    Properties
    {
        [MainColor] _ShallowColor("Shallow Color", Color) = (0.12,0.67,0.68,1)
        _DeepColor("Deep Color", Color) = (0.055,0.29,0.48,1)
        _PatternColor("Surface Highlights", Color) = (0.42,0.88,0.93,1)
        _FoamColor("Foam Color", Color) = (0.85,0.96,0.93,1)
        _DepthColorDistance("Deep Color Depth (m)", Range(0.5,40)) = 12
        _WaveHeight("Wave Height (m)", Range(0,0.3)) = 0.1
        _WaveScale("World Wave Scale", Range(0.01,1)) = 0.18
        _WaveSpeed("Wave Speed", Range(0,3)) = 0.6
        _PatternScale("World Pattern Scale", Range(0.01,1)) = 0.16
        _PatternSpeed("Pattern Speed", Range(0,1)) = 0.05
        _PatternStrength("Pattern Strength", Range(0,1)) = 0.3
        _FoamWidth("Shore Foam Depth (m)", Range(0.05,8)) = 1.5
        _FoamStrength("Foam Strength", Range(0,1)) = 0.7
        _DetailFadeDistance("Detail Fade Distance (m)", Range(50,1500)) = 450
        _ShadowTint("Shadow Tint", Color) = (0.6,0.73,0.8,1)
        _ShadowContrast("Shadow Contrast", Range(0,1)) = 0.5
        _SkyReflectionColor("Stylized Sky Reflection", Color) = (0.38,0.54,0.58,1)
        _FresnelStrength("Sky Reflection Strength", Range(0,0.5)) = 0.18
        _SpecularStrength("Sun Glint Strength", Range(0,1)) = 0.28
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry+10" }
        HLSLINCLUDE
        #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DOTS.hlsl"
        #include "CartoonCommon.hlsl"
        CBUFFER_START(UnityPerMaterial)
            half4 _ShallowColor;
            half4 _DeepColor;
            half4 _PatternColor;
            half4 _FoamColor;
            half4 _ShadowTint;
            float _DepthColorDistance;
            float _WaveHeight;
            float _WaveScale;
            float _WaveSpeed;
            float _PatternScale;
            float _PatternSpeed;
            float _PatternStrength;
            float _FoamWidth;
            float _FoamStrength;
            float _DetailFadeDistance;
            float _ShadowContrast;
            half4 _SkyReflectionColor;
            float _FresnelStrength;
            float _SpecularStrength;
        CBUFFER_END

        #ifdef UNITY_DOTS_INSTANCING_ENABLED
        UNITY_DOTS_INSTANCING_START(MaterialPropertyMetadata)
            UNITY_DOTS_INSTANCED_PROP(float4, _ShallowColor)
        UNITY_DOTS_INSTANCING_END(MaterialPropertyMetadata)
        static float4 cartoonShallowColor;
        void SetupCartoonWaterPropertyCaches()
        {
            cartoonShallowColor = UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(float4, _ShallowColor);
        }
        #undef UNITY_SETUP_DOTS_MATERIAL_PROPERTY_CACHES
        #define UNITY_SETUP_DOTS_MATERIAL_PROPERTY_CACHES() SetupCartoonWaterPropertyCaches()
        #define _ShallowColor cartoonShallowColor
        #endif

        struct Attributes
        {
            float4 positionOS : POSITION;
            float3 normalOS : NORMAL;
            float3 waterData : TEXCOORD1;
            UNITY_VERTEX_INPUT_INSTANCE_ID
        };
        struct Varyings
        {
            float4 positionCS : SV_POSITION;
            float3 positionWS : TEXCOORD0;
            half3 normalWS : TEXCOORD1;
            float3 waterData : TEXCOORD2;
            half fog : TEXCOORD3;
            UNITY_VERTEX_INPUT_INSTANCE_ID
            UNITY_VERTEX_OUTPUT_STEREO
        };
        Varyings WaterVertex(Attributes input)
        {
            Varyings output = (Varyings)0;
            UNITY_SETUP_INSTANCE_ID(input);
            UNITY_TRANSFER_INSTANCE_ID(input, output);
            UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
            float3 p = TransformObjectToWorld(input.positionOS.xyz);
            float shoreFade = lerp(saturate(input.waterData.x / max(_WaveHeight * 4, 0.01)),
                saturate(input.waterData.x / max(_WaveHeight * 2, 0.01)) * (1 - input.waterData.y), input.waterData.z);
            float phaseA = (p.x * 0.8 + p.z * 0.6) * _WaveScale + _Time.y * _WaveSpeed;
            float phaseB = (p.x * -0.6 + p.z * 0.8) * _WaveScale * 0.73 - _Time.y * _WaveSpeed * 0.81;
            p.y += (sin(phaseA) + sin(phaseB) * 0.5) * _WaveHeight * shoreFade;
            float slopeA = cos(phaseA) * _WaveHeight * _WaveScale * shoreFade;
            float slopeB = cos(phaseB) * _WaveHeight * _WaveScale * 0.365 * shoreFade;
            output.normalWS = normalize(TransformObjectToWorldNormal(input.normalOS) +
                float3(-slopeA * 0.8 + slopeB * 0.6, 0, -slopeA * 0.6 - slopeB * 0.8));
            output.positionWS = p;
            output.positionCS = TransformWorldToHClip(p);
            output.waterData = input.waterData;
            output.fog = ComputeFogFactor(output.positionCS.z);
            return output;
        }
        float2 WaterHash(float2 p)
        {
            return frac(sin(float2(dot(p, float2(127.1,311.7)), dot(p,float2(269.5,183.3)))) * 43758.5453);
        }
        float WaterCells(float2 p)
        {
            float2 cell = floor(p);
            float2 local = frac(p);
            float first = 8;
            float second = 8;
            [unroll] for (int y = -1; y <= 1; y++)
            [unroll] for (int x = -1; x <= 1; x++)
            {
                float2 offset = float2(x,y);
                float2 delta = offset + WaterHash(cell + offset) - local;
                float d = dot(delta,delta);
                second = d < first ? first : min(second,d);
                first = min(first,d);
            }
            return sqrt(second) - sqrt(first);
        }
        half4 WaterFragment(Varyings input) : SV_Target
        {
            UNITY_SETUP_INSTANCE_ID(input);
            UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
            float2 p = input.positionWS.xz * _PatternScale + _Time.y * _PatternSpeed * float2(0.7,0.3);
            p += 0.12 * sin(p.yx * 1.2 + _Time.y * _PatternSpeed);
            float cells = WaterCells(p);
            float edge = 1 - smoothstep(0.025, 0.075 + fwidth(cells), cells);
            float fade = saturate(1 - distance(input.positionWS, _WorldSpaceCameraPos) / _DetailFadeDistance);
            half3 albedo = lerp(_ShallowColor.rgb, _DeepColor.rgb, saturate(input.waterData.x / _DepthColorDistance));
            albedo = lerp(albedo, _PatternColor.rgb, edge * _PatternStrength * fade);
            float shore = lerp(1 - smoothstep(0, _FoamWidth, input.waterData.x), input.waterData.y, input.waterData.z);
            float foam = shore * (0.6 + edge * 0.4) * _FoamStrength;
            albedo = lerp(albedo, _FoamColor.rgb, foam);
            half3 color = CartoonLighting(input.positionWS, normalize(input.normalWS), albedo,
                _ShadowTint.rgb, _ShadowContrast, 3, 0.12);
            half3 normal = normalize(input.normalWS);
            half3 view = SafeNormalize(_WorldSpaceCameraPos - input.positionWS);
            float fresnel = pow(1 - saturate(dot(normal, view)), 4) * _FresnelStrength;
            color = lerp(color, _SkyReflectionColor.rgb, fresnel);
            Light sun = GetMainLight();
            float glint = pow(saturate(dot(normal, SafeNormalize(sun.direction + view))), 96);
            color += glint * _SpecularStrength * sun.color * fade;
            return half4(MixFog(color, input.fog), 1);
        }
        half4 WaterNormals(Varyings input) : SV_Target
        {
            UNITY_SETUP_INSTANCE_ID(input);
            return CartoonDepthNormal(input.normalWS);
        }
        half4 EmptyFragment(Varyings input) : SV_Target { return 0; }
        ENDHLSL
        Pass
        {
            Name "Forward"
            Tags { "LightMode"="UniversalForwardOnly" }
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex WaterVertex
            #pragma fragment WaterFragment
            #pragma multi_compile_instancing
            #pragma multi_compile_fog
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            ENDHLSL
        }
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode"="DepthOnly" }
            ZWrite On
            ColorMask R
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex WaterVertex
            #pragma fragment EmptyFragment
            #pragma multi_compile_instancing
            ENDHLSL
        }
        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode"="DepthNormalsOnly" }
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex WaterVertex
            #pragma fragment WaterNormals
            #pragma multi_compile_instancing
            #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT
            ENDHLSL
        }
    }
    Fallback Off
}
