Shader "Leviathan/Cartoon Trees"
{
    Properties
    {
        _BaseColor("Tint",Color)=(1,1,1,1)
        _TreeWind("Wind direction / strength / speed",Vector)=(.7,.3,.025,1)
        _LeafTint("Foliage Tint",Color)=(0.72,0.84,0.74,1)
        _LeafTintStrength("Foliage Tint Strength",Range(0,1))=0.25
        _Saturation("Color Saturation",Range(0,1.5))=0.75
        _ColorVariation("Per Tree Variation",Range(0,0.25))=0.12
        _ShadowTint("Shadow Tint",Color)=(0.4,0.5,0.53,1)
    }
    SubShader
    {
        Tags {"RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry"}
        HLSLINCLUDE
        #include "../Environment/CartoonCommon.hlsl"
        CBUFFER_START(UnityPerMaterial)
            half4 _BaseColor;
            float4 _TreeWind;
            half4 _LeafTint, _ShadowTint;
            float _LeafTintStrength, _Saturation, _ColorVariation;
        CBUFFER_END
        struct Attributes {float4 positionOS:POSITION;float3 normalOS:NORMAL;half4 color:COLOR;UNITY_VERTEX_INPUT_INSTANCE_ID};
        struct Varyings {float4 positionCS:SV_POSITION;float3 positionWS:TEXCOORD0;half3 normalWS:TEXCOORD1;half3 color:TEXCOORD2;half fog:TEXCOORD3;UNITY_VERTEX_INPUT_INSTANCE_ID};
        float3 TreePosition(Attributes input)
        {
            float3 root=TransformObjectToWorld(float3(0,0,0));
            float wave=sin(_Time.y*_TreeWind.w+dot(root.xz,float2(.021,.033)))*.7+
                sin(_Time.y*1.9+root.x*.07)*.3;
            float3 p=input.positionOS.xyz;
            p.xz+=normalize(_TreeWind.xy+float2(.0001,0))*wave*_TreeWind.z*input.color.a*saturate(p.y)*saturate(p.y);
            return TransformObjectToWorld(p);
        }
        Varyings TreeVertex(Attributes input)
        {
            UNITY_SETUP_INSTANCE_ID(input);
            Varyings output;UNITY_TRANSFER_INSTANCE_ID(input,output);
            output.positionWS=TreePosition(input);output.positionCS=TransformWorldToHClip(output.positionWS);
            output.normalWS=TransformObjectToWorldNormal(input.normalOS);
            half3 color=input.color.rgb;
            half luminance=dot(color,half3(.2126,.7152,.0722));
            color=lerp(luminance.xxx,color,_Saturation);
            color*=lerp(1.0.xxx,_LeafTint.rgb,input.color.a*_LeafTintStrength);
            float3 root=TransformObjectToWorld(float3(0,0,0));
            color*=1+(SurfaceHash(root)*2-1)*_ColorVariation;
            output.color=color*_BaseColor.rgb;output.fog=ComputeFogFactor(output.positionCS.z);return output;
        }
        half4 TreeFragment(Varyings input):SV_Target
        {
            UNITY_SETUP_INSTANCE_ID(input);
            half3 n=normalize(input.normalWS);
            half3 color=CartoonLighting(input.positionWS,n,input.color,_ShadowTint.rgb,.65,3,.14);
            return half4(MixFog(color,input.fog),1);
        }
        Varyings TreeShadow(Attributes input)
        {
            Varyings output=TreeVertex(input);
            output.positionCS=CartoonShadowPosition(output.positionWS,output.normalWS);
            return output;
        }
        half4 TreeDepth(Varyings input):SV_Target {return 0;}
        half4 TreeNormals(Varyings input):SV_Target {return CartoonDepthNormal(input.normalWS);}
        ENDHLSL
        Pass
        {
            Name "UniversalForward" Tags {"LightMode"="UniversalForwardOnly"}
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex TreeVertex
            #pragma fragment TreeFragment
            #pragma multi_compile_instancing
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fog
            ENDHLSL
        }
        Pass
        {
            Name "ShadowCaster" Tags {"LightMode"="ShadowCaster"} ZWrite On ColorMask 0
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex TreeShadow
            #pragma fragment TreeDepth
            #pragma multi_compile_instancing
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            ENDHLSL
        }
        Pass
        {
            Name "DepthOnly" Tags {"LightMode"="DepthOnly"} ZWrite On ColorMask R
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex TreeVertex
            #pragma fragment TreeDepth
            #pragma multi_compile_instancing
            ENDHLSL
        }
        Pass
        {
            Name "DepthNormals" Tags {"LightMode"="DepthNormalsOnly"} ZWrite On
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex TreeVertex
            #pragma fragment TreeNormals
            #pragma multi_compile_instancing
            #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT
            ENDHLSL
        }
    }
}
