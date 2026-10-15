Shader "Leviathan/Dieselpunk Surface"
{
    Properties
    {
        [MainTexture] _BaseMap("Albedo",2D)="white"{}
        [MainColor] _BaseColor("Paint / Surface Color",Color)=(0.24,0.32,0.32,1)
        _VertexColorStrength("Vertex Color Strength",Range(0,1))=0
        _Saturation("Vertex Color Saturation",Range(0,1.5))=0.85
        _Metallic("Metallic",Range(0,1))=0.15
        _Smoothness("Smoothness",Range(0,1))=0.35
        _VertexMetalMask("Metal Mask From Neutral Vertex Colors",Range(0,1))=0
        _WearColor("Exposed Metal / Oxide",Color)=(0.33,0.22,0.15,1)
        _WearAmount("Paint Chipping",Range(0,0.5))=0.12
        _GrimeColor("Dust / Grime",Color)=(0.36,0.34,0.28,1)
        _GrimeStrength("Grime Strength",Range(0,0.5))=0.15
        _WeatherScale("Weathering Scale (per meter)",Range(0.1,8))=1.7
        _GrainStrength("Surface Grain",Range(0,0.2))=0.055
        [HDR] _EmissionColor("Emission",Color)=(0,0,0,1)
    }
    SubShader
    {
        Tags {"RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry"}
        HLSLINCLUDE
        #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DOTS.hlsl"
        #include "CartoonCommon.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/SurfaceData.hlsl"
        TEXTURE2D(_BaseMap);SAMPLER(sampler_BaseMap);
        CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_ST;
            half4 _BaseColor, _WearColor, _GrimeColor, _EmissionColor;
            float _VertexColorStrength, _Saturation, _Metallic, _Smoothness, _VertexMetalMask;
            float _WearAmount, _GrimeStrength, _WeatherScale, _GrainStrength;
        CBUFFER_END
        #ifdef UNITY_DOTS_INSTANCING_ENABLED
        UNITY_DOTS_INSTANCING_START(MaterialPropertyMetadata)
            UNITY_DOTS_INSTANCED_PROP(float4, _BaseColor)
        UNITY_DOTS_INSTANCING_END(MaterialPropertyMetadata)
        static float4 dieselBaseColor;
        void SetupDieselPropertyCaches()
        {dieselBaseColor=UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(float4,_BaseColor);}
        #undef UNITY_SETUP_DOTS_MATERIAL_PROPERTY_CACHES
        #define UNITY_SETUP_DOTS_MATERIAL_PROPERTY_CACHES() SetupDieselPropertyCaches()
        #define _BaseColor dieselBaseColor
        #endif

        struct Attributes
        {
            float4 positionOS:POSITION;float3 normalOS:NORMAL;float2 uv:TEXCOORD0;half4 color:COLOR;
            UNITY_VERTEX_INPUT_INSTANCE_ID
        };
        struct Varyings
        {
            float4 positionCS:SV_POSITION;float3 positionWS:TEXCOORD0;half3 normalWS:TEXCOORD1;
            float2 uv:TEXCOORD2;half4 color:TEXCOORD3;float3 surfacePosition:TEXCOORD4;half fog:TEXCOORD5;
            UNITY_VERTEX_INPUT_INSTANCE_ID
            UNITY_VERTEX_OUTPUT_STEREO
        };
        Varyings SurfaceVertex(Attributes input)
        {
            Varyings output=(Varyings)0;
            UNITY_SETUP_INSTANCE_ID(input);UNITY_TRANSFER_INSTANCE_ID(input,output);
            UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
            output.positionWS=TransformObjectToWorld(input.positionOS.xyz);
            output.positionCS=TransformWorldToHClip(output.positionWS);
            output.normalWS=TransformObjectToWorldNormal(input.normalOS);
            output.uv=TRANSFORM_TEX(input.uv,_BaseMap);output.color=input.color;
            // Meter-scaled local coordinates keep paint attached to rotating/recoiling parts.
            float4x4 objectToWorld=GetObjectToWorldMatrix();
            float3 scale=float3(length(objectToWorld._m00_m10_m20),
                length(objectToWorld._m01_m11_m21),length(objectToWorld._m02_m12_m22));
            output.surfacePosition=input.positionOS.xyz*scale;
            output.fog=ComputeFogFactor(output.positionCS.z);
            return output;
        }
        half4 SurfaceFragment(Varyings input):SV_Target
        {
            UNITY_SETUP_INSTANCE_ID(input);UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
            half3 vertex=input.color.rgb;
            half luminance=dot(vertex,half3(.2126,.7152,.0722));
            vertex=lerp(luminance.xxx,vertex,_Saturation);
            half3 albedo=SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,input.uv).rgb*_BaseColor.rgb;
            albedo*=lerp(1.0.xxx,vertex,_VertexColorStrength);
            float coarse=SurfaceNoise3D(input.surfacePosition*_WeatherScale);
            float grain=SurfaceNoise3D(input.surfacePosition*_WeatherScale*4.1);
            float fade=1-smoothstep(90,240,distance(input.positionWS,_WorldSpaceCameraPos));
            float wear=smoothstep(1-_WearAmount-.055,1-_WearAmount+.055,coarse)*step(.0001,_WearAmount)*fade;
            albedo=lerp(albedo,_WearColor.rgb,wear);
            half3 n=normalize(input.normalWS);
            float dust=saturate(n.y*.5+.5)*coarse*_GrimeStrength;
            albedo=lerp(albedo,_GrimeColor.rgb,dust);
            albedo*=1+(grain*2-1)*_GrainStrength*fade;
            float vertexSaturation=(max(vertex.r,max(vertex.g,vertex.b))-min(vertex.r,min(vertex.g,vertex.b)))/max(.001,max(vertex.r,max(vertex.g,vertex.b)));
            float metalMask=lerp(1,1-smoothstep(.12,.42,vertexSaturation),_VertexMetalMask);
            InputData lighting=(InputData)0;
            lighting.positionWS=input.positionWS;lighting.positionCS=input.positionCS;lighting.normalWS=n;
            lighting.viewDirectionWS=SafeNormalize(_WorldSpaceCameraPos-input.positionWS);
            #if defined(_MAIN_LIGHT_SHADOWS_SCREEN)
                lighting.shadowCoord=ComputeScreenPos(TransformWorldToHClip(input.positionWS));
            #else
                lighting.shadowCoord=TransformWorldToShadowCoord(input.positionWS);
            #endif
            lighting.bakedGI=SampleSH(n);lighting.shadowMask=1.0.xxxx;
            lighting.normalizedScreenSpaceUV=GetNormalizedScreenSpaceUV(input.positionCS);
            SurfaceData surface=(SurfaceData)0;
            surface.albedo=albedo;surface.metallic=_Metallic*metalMask;
            surface.smoothness=saturate(_Smoothness-grain*.09-dust*.18);
            surface.normalTS=half3(0,0,1);surface.occlusion=1;surface.alpha=1;surface.emission=_EmissionColor.rgb;
            half4 color=UniversalFragmentPBR(lighting,surface);
            color.rgb=MixFog(color.rgb,input.fog);return color;
        }
        Varyings SurfaceShadow(Attributes input)
        {Varyings output=SurfaceVertex(input);output.positionCS=CartoonShadowPosition(output.positionWS,output.normalWS);return output;}
        half4 SurfaceDepth(Varyings input):SV_Target {return 0;}
        half4 SurfaceNormals(Varyings input):SV_Target
        {UNITY_SETUP_INSTANCE_ID(input);return CartoonDepthNormal(input.normalWS);}
        ENDHLSL
        Pass
        {
            Name "Forward" Tags {"LightMode"="UniversalForwardOnly"}
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex SurfaceVertex
            #pragma fragment SurfaceFragment
            #pragma multi_compile_instancing
            #pragma multi_compile_fog
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            ENDHLSL
        }
        Pass
        {
            Name "ShadowCaster" Tags {"LightMode"="ShadowCaster"} ZWrite On ColorMask 0
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex SurfaceShadow
            #pragma fragment SurfaceDepth
            #pragma multi_compile_instancing
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            ENDHLSL
        }
        Pass
        {
            Name "DepthOnly" Tags {"LightMode"="DepthOnly"} ZWrite On ColorMask R
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex SurfaceVertex
            #pragma fragment SurfaceDepth
            #pragma multi_compile_instancing
            ENDHLSL
        }
        Pass
        {
            Name "DepthNormals" Tags {"LightMode"="DepthNormalsOnly"} ZWrite On
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex SurfaceVertex
            #pragma fragment SurfaceNormals
            #pragma multi_compile_instancing
            #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT
            ENDHLSL
        }
    }
    Fallback Off
}
