Shader "Leviathan/Cartoon Terrain"
{
    Properties
    {
        [MainTexture] _BaseMap("Biome Colors", 2D) = "white" {}
        [MainColor] _BaseColor("Tint", Color) = (1,1,1,1)
        _ColorIntensity("Color Intensity", Range(0.5,1.5)) = 1.08
        _ShadowTint("Shadow Tint", Color) = (0.58,0.68,0.74,1)
        _ShadowContrast("Shadow Contrast", Range(0,1)) = 0.7
        [Enum(Two,2,Three,3)] _Bands("Lighting Bands", Float) = 3
        _BandSoftness("Band Softness", Range(0.01,0.25)) = 0.08
        _DetailScale("World Detail Scale", Range(0.001,0.5)) = 0.025
        _DetailStrength("Detail Strength", Range(0,0.15)) = 0.025
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry" }
        HLSLINCLUDE
        #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DOTS.hlsl"
        #include "CartoonCommon.hlsl"
        TEXTURE2D(_BaseMap);
        SAMPLER(sampler_BaseMap);
        CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_ST;
            half4 _BaseColor;
            half4 _ShadowTint;
            float _ColorIntensity;
            float _ShadowContrast;
            float _Bands;
            float _BandSoftness;
            float _DetailScale;
            float _DetailStrength;
        CBUFFER_END

        #ifdef UNITY_DOTS_INSTANCING_ENABLED
        UNITY_DOTS_INSTANCING_START(MaterialPropertyMetadata)
            UNITY_DOTS_INSTANCED_PROP(float4, _BaseColor)
        UNITY_DOTS_INSTANCING_END(MaterialPropertyMetadata)
        static float4 cartoonBaseColor;
        void SetupCartoonTerrainPropertyCaches()
        {
            cartoonBaseColor = UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(float4, _BaseColor);
        }
        #undef UNITY_SETUP_DOTS_MATERIAL_PROPERTY_CACHES
        #define UNITY_SETUP_DOTS_MATERIAL_PROPERTY_CACHES() SetupCartoonTerrainPropertyCaches()
        #define _BaseColor cartoonBaseColor
        #endif

        struct Attributes
        {
            float4 positionOS : POSITION;
            float3 normalOS : NORMAL;
            float2 uv : TEXCOORD0;
            UNITY_VERTEX_INPUT_INSTANCE_ID
        };
        struct Varyings
        {
            float4 positionCS : SV_POSITION;
            float3 positionWS : TEXCOORD0;
            half3 normalWS : TEXCOORD1;
            float2 uv : TEXCOORD2;
            half fog : TEXCOORD3;
            UNITY_VERTEX_INPUT_INSTANCE_ID
            UNITY_VERTEX_OUTPUT_STEREO
        };
        Varyings TerrainVertex(Attributes input)
        {
            Varyings output = (Varyings)0;
            UNITY_SETUP_INSTANCE_ID(input);
            UNITY_TRANSFER_INSTANCE_ID(input, output);
            UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
            output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
            output.positionCS = TransformWorldToHClip(output.positionWS);
            output.normalWS = TransformObjectToWorldNormal(input.normalOS);
            output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
            output.fog = ComputeFogFactor(output.positionCS.z);
            return output;
        }
        half4 TerrainFragment(Varyings input) : SV_Target
        {
            UNITY_SETUP_INSTANCE_ID(input);
            UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
            half3 albedo = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv).rgb * _BaseColor.rgb;
            float2 p = input.positionWS.xz * _DetailScale;
            float detail = sin(p.x + sin(p.y * 0.7)) * sin(p.y * 0.83 + p.x * 0.27);
            float distanceFade = saturate(1 - distance(_WorldSpaceCameraPos, input.positionWS) / 600);
            albedo *= _ColorIntensity * (1 + detail * _DetailStrength * distanceFade);
            half3 color = CartoonLighting(input.positionWS, normalize(input.normalWS), albedo,
                _ShadowTint.rgb, _ShadowContrast, _Bands, _BandSoftness);
            return half4(MixFog(color, input.fog), 1);
        }
        half4 TerrainNormals(Varyings input) : SV_Target
        {
            UNITY_SETUP_INSTANCE_ID(input);
            return CartoonDepthNormal(input.normalWS);
        }
        Varyings TerrainShadow(Attributes input)
        {
            Varyings output = TerrainVertex(input);
            output.positionCS = CartoonShadowPosition(output.positionWS, output.normalWS);
            return output;
        }
        half4 EmptyFragment(Varyings input) : SV_Target { return 0; }
        ENDHLSL

        Pass
        {
            Name "Forward"
            Tags { "LightMode"="UniversalForwardOnly" }
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex TerrainVertex
            #pragma fragment TerrainFragment
            #pragma multi_compile_instancing
            #pragma multi_compile_fog
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            ENDHLSL
        }
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode"="ShadowCaster" }
            ZWrite On
            ColorMask 0
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex TerrainShadow
            #pragma fragment EmptyFragment
            #pragma multi_compile_instancing
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
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
            #pragma vertex TerrainVertex
            #pragma fragment EmptyFragment
            #pragma multi_compile_instancing
            ENDHLSL
        }
        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode"="DepthNormalsOnly" }
            ZWrite On
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex TerrainVertex
            #pragma fragment TerrainNormals
            #pragma multi_compile_instancing
            #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT
            ENDHLSL
        }
    }
    Fallback Off
}
