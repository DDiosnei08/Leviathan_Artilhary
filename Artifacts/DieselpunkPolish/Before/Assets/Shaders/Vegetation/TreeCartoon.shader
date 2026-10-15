Shader "Leviathan/Cartoon Trees"
{
    Properties
    {
        _BaseColor("Tint",Color)=(1,1,1,1)
        _TreeWind("Wind direction / strength / speed",Vector)=(.7,.3,.025,1)
    }
    SubShader
    {
        Tags {"RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry"}
        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
        CBUFFER_START(UnityPerMaterial)
            half4 _BaseColor;
            float4 _TreeWind;
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
            output.color=input.color.rgb*_BaseColor.rgb;output.fog=ComputeFogFactor(output.positionCS.z);return output;
        }
        half4 TreeFragment(Varyings input):SV_Target
        {
            UNITY_SETUP_INSTANCE_ID(input);
            half3 n=normalize(input.normalWS);
            Light light=GetMainLight(TransformWorldToShadowCoord(input.positionWS));
            half diffuse=saturate(dot(n,light.direction)*.7+.3);
            half bands=lerp(.4,.7,smoothstep(.15,.3,diffuse))+ .3*smoothstep(.55,.75,diffuse);
            half3 color=input.color*(SampleSH(n)*.8+light.color*bands*lerp(.5,1,light.shadowAttenuation));
            return half4(MixFog(color,input.fog),1);
        }
        float3 _LightDirection, _LightPosition;
        Varyings TreeShadow(Attributes input)
        {
            Varyings output=TreeVertex(input);
            float3 direction=_LightDirection;
            #if defined(_CASTING_PUNCTUAL_LIGHT_SHADOW)
                direction=normalize(_LightPosition-output.positionWS);
            #endif
            output.positionCS=TransformWorldToHClip(ApplyShadowBias(output.positionWS,output.normalWS,direction));
            #if UNITY_REVERSED_Z
                output.positionCS.z=min(output.positionCS.z,UNITY_NEAR_CLIP_VALUE*output.positionCS.w);
            #else
                output.positionCS.z=max(output.positionCS.z,UNITY_NEAR_CLIP_VALUE*output.positionCS.w);
            #endif
            return output;
        }
        half4 TreeDepth(Varyings input):SV_Target {return 0;}
        ENDHLSL
        Pass
        {
            Name "UniversalForward" Tags {"LightMode"="UniversalForward"}
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex TreeVertex
            #pragma fragment TreeFragment
            #pragma multi_compile_instancing
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _SHADOWS_SOFT
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
    }
}
