Shader "Leviathan/Layered Sky"
{
    Properties
    {
        [Header(Day and Twilight)]
        _DayZenith ("Day Zenith", Color) = (0.16,0.39,0.65,1)
        _DayHorizon ("Day Horizon", Color) = (0.68,0.79,0.83,1)
        _DawnColor ("Dawn", Color) = (1.0,0.48,0.24,1)
        _DuskColor ("Dusk", Color) = (0.85,0.28,0.43,1)
        [Header(Night)]
        _NightZenith ("Night Zenith", Color) = (0.004,0.009,0.027,1)
        _NightHorizon ("Night Horizon", Color) = (0.035,0.055,0.11,1)
        _StarDensity ("Star Density", Range(0,1)) = 0.72
        _StarBrightness ("Star Brightness", Range(0,5)) = 2.1
        _StarScale ("Star Field Scale", Range(80,600)) = 280
        _SkySeed ("Sky Seed", Float) = 7301
        _MoonBrightness ("Moon Brightness", Range(0,5)) = 1.4
        [Header(Aurora)]
        _AuroraGreen ("Aurora Green", Color) = (0.10,0.85,0.47,1)
        _AuroraBlue ("Aurora Cyan", Color) = (0.08,0.48,0.80,1)
        _AuroraPink ("Aurora Pink", Color) = (0.62,0.18,0.55,1)
        _AuroraStrength ("Aurora Strength", Range(0,3)) = 0.8
        _AuroraSpeed ("Aurora Speed", Range(0,3)) = 0.7
        _AuroraBearing ("Aurora Bearing", Range(0,360)) = 0
        [Header(Cloud Layers)]
        _CloudLowHeight ("Lower Altitude (m)", Float) = 1350
        _CloudHighHeight ("Upper Altitude (m)", Float) = 3000
        _CloudLowScale ("Lower Noise Scale", Float) = 0.0015
        _CloudHighScale ("Upper Noise Scale", Float) = 0.00075
        _CloudLowCoverage ("Lower Coverage", Range(0,1)) = 0.52
        _CloudHighCoverage ("Upper Coverage", Range(0,1)) = 0.32
        _CloudSoftness ("Cloud Edge Softness", Range(0.02,0.4)) = 0.13
        _CloudOpacity ("Cloud Opacity", Range(0,1)) = 0.88
        _CloudDayColor ("Cloud Day Color", Color) = (0.92,0.93,0.89,1)
        _CloudNightColor ("Cloud Night Color", Color) = (0.045,0.065,0.12,1)
        _CloudWindMultiplier ("Cloud Wind Multiplier", Range(0,8)) = 2
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Background" "RenderType"="Background" "PreviewType"="Skybox" }
        Cull Off ZWrite Off
        Pass
        {
            Name "Sky"
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma target 3.5
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
                half4 _DayZenith, _DayHorizon, _DawnColor, _DuskColor, _NightZenith, _NightHorizon;
                half4 _AuroraGreen, _AuroraBlue, _AuroraPink, _CloudDayColor, _CloudNightColor;
                float _StarDensity, _StarBrightness, _StarScale, _SkySeed, _MoonBrightness;
                float _AuroraStrength, _AuroraSpeed, _AuroraBearing;
                float _CloudLowHeight, _CloudHighHeight, _CloudLowScale, _CloudHighScale;
                float _CloudLowCoverage, _CloudHighCoverage, _CloudSoftness, _CloudOpacity, _CloudWindMultiplier;
            CBUFFER_END
            // Controller globals, not material properties: the material asset is never mutated at runtime.
            float4 _LeviathanSkySun, _LeviathanSkyState, _LeviathanSkyMotion;
            struct Attributes { float3 positionOS : POSITION; };
            struct Varyings { float4 positionCS : SV_POSITION; float3 direction : TEXCOORD0; };
            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS);
                output.positionCS.z = output.positionCS.w * UNITY_RAW_FAR_CLIP_VALUE;
                output.direction = input.positionOS;
                return output;
            }
            float Hash(float3 p)
            {
                p = frac(p * 0.1031); p += dot(p, p.yzx + 33.33);
                return frac((p.x + p.y) * p.z);
            }
            float Noise(float2 p)
            {
                float2 c = floor(p), f = frac(p); f = f * f * (3 - 2 * f);
                return lerp(lerp(Hash(float3(c, _SkySeed)), Hash(float3(c + float2(1,0), _SkySeed)), f.x),
                    lerp(Hash(float3(c + float2(0,1), _SkySeed)), Hash(float3(c + 1, _SkySeed)), f.x), f.y);
            }
            float CloudsNoise(float2 p)
            {
                float value = Noise(p) * 0.57;
                p = float2(p.x * 1.6 - p.y * 1.2, p.x * 1.2 + p.y * 1.6) + 17.3;
                value += Noise(p) * 0.28;
                value += Noise(p * 2.03 + 9.7) * 0.15;
                return value;
            }
            half3 Stars(float3 direction, float scale, float density, float clock)
            {
                float3 p = direction * scale, cell = floor(p);
                float h = Hash(cell + _SkySeed);
                float3 center = float3(Hash(cell + 7.13), Hash(cell + 29.7), Hash(cell + 63.1));
                float distanceToStar = length(frac(p) - lerp(0.1, 0.9, center));
                float radius = lerp(0.07, 0.16, Hash(cell + 37.2));
                float pixel = max(0.015, length(fwidth(p)) * 0.35);
                float starSpot = 1 - smoothstep(radius, radius + pixel, distanceToStar);
                float selected = step(1 - density * 0.038, h);
                float twinkle = 0.88 + 0.12 * sin(clock * (0.4 + h) + h * 100);
                half3 tint = lerp(half3(0.65,0.80,1), half3(1,0.86,0.65), center.z);
                return tint * starSpot * selected * twinkle * lerp(0.5,1.8, center.x);
            }
            half3 Aurora(float3 direction, float azimuth, float clock)
            {
                float t = clock * _AuroraSpeed * 0.045;
                float bearing = radians(_AuroraBearing);
                float facing = pow(saturate(cos(azimuth - bearing)), 2);
                float path = 0.29 + 0.07 * sin(azimuth * 3 + t) + 0.045 * sin(azimuth * 7 - t * 0.7);
                float above = direction.y - path;
                float ribbon = exp2(-abs(above) * 70);
                float rays = pow(0.5 + 0.5 * sin(azimuth * 95 + Noise(float2(azimuth * 4, t)) * 12), 3);
                float curtain = step(0, above) * exp2(-above * 17) * rays;
                float lowerFade = smoothstep(0.02, 0.16, direction.y);
                half3 baseColor = lerp(_AuroraGreen.rgb, _AuroraBlue.rgb, 0.5 + 0.5 * sin(azimuth * 2 + t));
                half3 color = lerp(baseColor, _AuroraPink.rgb, saturate(above * 7));
                return color * (ribbon * 0.8 + curtain * 0.7) * facing * lowerFade * _AuroraStrength;
            }
            float4 CloudLayer(float3 direction, float altitude, float scale, float coverage, float speed, float3 sun, float day, float twilight, half3 warm)
            {
                float planeDistance = altitude - _WorldSpaceCameraPos.y;
                if (direction.y <= 0.012 || planeDistance <= 0) return 0;
                // Intersect the view ray with a world-space layer, so camera translation creates parallax.
                float2 planeUV = _WorldSpaceCameraPos.xz + direction.xz * (planeDistance / max(direction.y, 0.025));
                planeUV -= _LeviathanSkyMotion.yz * _CloudWindMultiplier * speed;
                float shape = CloudsNoise(planeUV * scale + speed * 57);
                float edge = 1 - coverage;
                float alpha = smoothstep(edge - _CloudSoftness, edge + _CloudSoftness, shape);
                alpha *= smoothstep(0.015,0.10,direction.y) * _CloudOpacity;
                float silver = pow(saturate(dot(direction, day > 0.5 ? sun : -sun)), 10);
                half3 color = lerp(_CloudNightColor.rgb, _CloudDayColor.rgb, day);
                color = lerp(color, warm * 0.8 + 0.2, twilight * (0.25 + 0.4 * silver));
                color *= lerp(0.66,1.02,saturate((shape - edge) / max(coverage,0.05)));
                color += silver * lerp(half3(0.09,0.13,0.2), half3(0.24,0.23,0.19), day);
                return float4(color, alpha);
            }
            half4 Frag(Varyings input) : SV_Target
            {
                float3 direction = normalize(input.direction);
                bool driven = _LeviathanSkyState.w > 0.5;
                float3 sun = driven ? normalize(_LeviathanSkySun.xyz) : normalize(float3(0.4,0.7,0.4));
                float day = driven ? _LeviathanSkyState.x : 1;
                float twilight = driven ? _LeviathanSkyState.y : 0;
                float dawn = driven ? _LeviathanSkyState.z : 1;
                float clock = _LeviathanSkyMotion.x;
                float height = pow(saturate(direction.y),0.55);
                half3 daytime = lerp(_DayHorizon.rgb, _DayZenith.rgb, height);
                half3 nighttime = lerp(_NightHorizon.rgb, _NightZenith.rgb, height);
                half3 sky = lerp(nighttime, daytime, day);
                half3 warm = lerp(_DuskColor.rgb, _DawnColor.rgb, dawn);
                float glow = pow(saturate(dot(direction, normalize(float3(sun.x,0.08,sun.z)))),4);
                sky = lerp(sky, warm, twilight * (1 - height) * (0.25 + glow * 0.65));
                float hemisphere = smoothstep(-0.02,0.08,direction.y);
                float night = (1 - day) * hemisphere;
                if (night > 0.001)
                {
                    float azimuth = atan2(direction.x,direction.z);
                    sky += (Stars(direction,_StarScale,_StarDensity,clock) + Stars(direction,_StarScale * 0.44,_StarDensity * 0.65,clock)) * _StarBrightness * night;
                    float galaxy = pow(saturate(1 - abs(dot(direction,normalize(float3(0.4,0.65,-0.3)))) * 4),3);
                    sky += half3(0.017,0.019,0.036) * galaxy * night;
                    sky += Aurora(direction,azimuth,clock) * night;
                }
                float angularPixel = max(0.00025,length(fwidth(direction)));
                float sunDistance = length(direction - sun);
                sky += half3(1,0.84,0.56) * (1 - smoothstep(0.009,0.009 + angularPixel,sunDistance)) * 6 * hemisphere;
                sky += half3(1,0.65,0.35) * exp2(-sunDistance * 35) * 0.18 * day;
                float3 moon = -sun;
                float moonDistance = length(direction - moon);
                float moonDisc = 1 - smoothstep(0.018,0.018 + angularPixel,moonDistance);
                float moonDetail = 0.8 + 0.2 * Noise(direction.xz * 650);
                sky += half3(0.65,0.79,1) * moonDisc * moonDetail * _MoonBrightness * night;
                float4 high = CloudLayer(direction,_CloudHighHeight,max(0.00001,_CloudHighScale),_CloudHighCoverage,0.55,sun,day,twilight,warm);
                sky = lerp(sky,high.rgb,high.a * 0.72);
                float4 low = CloudLayer(direction,_CloudLowHeight,max(0.00001,_CloudLowScale),_CloudLowCoverage,1,sun,day,twilight,warm);
                sky = lerp(sky,low.rgb,low.a);
                return half4(max(sky,0),1);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
