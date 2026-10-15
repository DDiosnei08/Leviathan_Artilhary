Shader "Leviathan/Spline Track"
{
    Properties
    {
        _BaseColor("Gravel", Color) = (.38,.4,.34,1)
        _TrackColor("Wheel Tracks", Color) = (.18,.21,.19,1)
        _TrackStrength("Track Strength", Range(0,1)) = .65
        _DetailScale("Gravel Scale", Float) = 3
        _EdgeWear("Irregular Edge", Range(0,.2)) = .06
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="TransparentCutout" "Queue"="AlphaTest" }
        HLSLINCLUDE
        #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DOTS.hlsl"
        #include "CartoonCommon.hlsl"
        CBUFFER_START(UnityPerMaterial)
        half4 _BaseColor;
        half4 _TrackColor;
        float _TrackStrength;
        float _DetailScale;
        float _EdgeWear;
        CBUFFER_END
        #ifdef UNITY_DOTS_INSTANCING_ENABLED
        UNITY_DOTS_INSTANCING_START(MaterialPropertyMetadata)
            UNITY_DOTS_INSTANCED_PROP(float4, _BaseColor)
        UNITY_DOTS_INSTANCING_END(MaterialPropertyMetadata)
        #define _BaseColor UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(float4, _BaseColor)
        #endif
        struct A { float4 p:POSITION; float3 n:NORMAL; float2 uv:TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
        struct V { float4 p:SV_POSITION; float3 world:TEXCOORD0; float3 n:TEXCOORD1; float2 uv:TEXCOORD2; float fog:TEXCOORD3; UNITY_VERTEX_INPUT_INSTANCE_ID };
        V Vert(A v)
        {
            V o; UNITY_SETUP_INSTANCE_ID(v); UNITY_TRANSFER_INSTANCE_ID(v,o);
            o.world=TransformObjectToWorld(v.p.xyz); o.p=TransformWorldToHClip(o.world);
            o.n=TransformObjectToWorldNormal(v.n); o.uv=v.uv; o.fog=ComputeFogFactor(o.p.z); return o;
        }
        void Edge(V i) { clip(min(i.uv.x,1-i.uv.x)-_EdgeWear*SurfaceNoise(i.world.xz*.8)); }
        half4 Frag(V i):SV_Target
        {
            UNITY_SETUP_INSTANCE_ID(i); Edge(i);
            float track=1-smoothstep(.055,.115,min(abs(i.uv.x-.24),abs(i.uv.x-.76)));
            float gravel=SurfaceNoise(i.world.xz*_DetailScale);
            half3 color=lerp(_BaseColor.rgb,_TrackColor.rgb,track*_TrackStrength)*lerp(.85,1.12,gravel);
            return half4(MixFog(CartoonLighting(i.world,normalize(i.n),color,half3(.62,.7,.73),.65,3,.14),i.fog),1);
        }
        half4 Depth(V i):SV_Target { Edge(i); return 0; }
        half4 Normals(V i):SV_Target { Edge(i); return CartoonDepthNormal(i.n); }
        V Shadow(A v) { V o=Vert(v); o.p=CartoonShadowPosition(o.world,o.n); return o; }
        ENDHLSL
        Pass
        {
            Name "Forward" Tags { "LightMode"="UniversalForwardOnly" }
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma target 3.5
            #pragma multi_compile_instancing
            #pragma multi_compile_fog
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            ENDHLSL
        }
        Pass
        {
            Name "DepthOnly" Tags { "LightMode"="DepthOnly" } ColorMask 0
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Depth
            #pragma target 3.5
            #pragma multi_compile_instancing
            ENDHLSL
        }
        Pass
        {
            Name "DepthNormals" Tags { "LightMode"="DepthNormals" }
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Normals
            #pragma target 3.5
            #pragma multi_compile_instancing
            #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT
            ENDHLSL
        }
        Pass
        {
            Name "ShadowCaster" Tags { "LightMode"="ShadowCaster" } ColorMask 0
            HLSLPROGRAM
            #pragma vertex Shadow
            #pragma fragment Depth
            #pragma target 3.5
            #pragma multi_compile_instancing
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            ENDHLSL
        }
    }
}
