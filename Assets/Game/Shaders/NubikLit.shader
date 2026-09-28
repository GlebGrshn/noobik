// Small URP forward shader for the whole prototype: vertex-coloured terrain,
// flat-coloured props, main light shadows, extra lights (headlamp) and a depth fog.
// Fog and ambient come from globals set by MineGame, so no fog keywords can be stripped.
Shader "Nubik/Lit"
{
    Properties
    {
        _BaseColor("Color", Color) = (1, 1, 1, 1)
        _Emission("Emission", Color) = (0, 0, 0, 0)
        _VertexColor("Use vertex color", Range(0, 1)) = 0
        _Noise("Surface grain", Range(0, 0.5)) = 0.06
        _ExtraLights("Lit by extra lights", Range(0, 1)) = 1
        _GrassColor("Grass on flat ground (vertex colour mode)", Color) = (0.42, 0.55, 0.30, 1)
        _Lawn("Lawn pattern", Range(0, 1)) = 0
        _Surface("Detail: wood / masonry / metal / cloth / crystal / skin / plaster / tile", Float) = 0
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        CBUFFER_START(UnityPerMaterial)
            half4 _BaseColor;
            half4 _Emission;
            half _VertexColor;
            half _Noise;
            half _ExtraLights;
            half4 _GrassColor;
            half _Lawn;
            half _Surface;
        CBUFFER_END
        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile _ _ADDITIONAL_LIGHTS
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            float Hash2(float2 p) { return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453); }

            float ValueNoise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                f = f * f * (3 - 2 * f);
                return lerp(lerp(Hash2(i), Hash2(i + float2(1, 0)), f.x), lerp(Hash2(i + float2(0, 1)), Hash2(i + 1), f.x), f.y);
            }

            half3 SurfaceDetail(float3 p, half3 n, half3 color)
            {
                if (_Surface < .5) return color;
                float3 an = abs(n);
                float2 uv = an.y > max(an.x, an.z) ? p.xz : an.x > an.z ? p.zy : p.xy;
                float noise = ValueNoise(uv * 3.7);
                float detail = 1;
                if (_Surface > .5 && _Surface < 1.5)
                {
                    float grain = sin(uv.y * 75 + ValueNoise(uv * float2(2, 7)) * 12 + sin(uv.x * 3) * 4);
                    float knot = sin(length((frac(uv * .7) - .5) * float2(2.2, .7)) * 60);
                    float aa = saturate(1 - length(fwidth(uv)) * 22);
                    detail = 1 + noise * .12 - .08 + (grain * .08 + knot * .035) * aa;
                }
                else if (_Surface > 1.5 && _Surface < 2.5 || _Surface > 7.5)
                {
                    float2 bricks = uv * (_Surface > 7.5 ? float2(3, 4) : float2(1.7, 2.8));
                    bricks.x += fmod(floor(bricks.y), 2) * .5;
                    float2 edge = min(frac(bricks), 1 - frac(bricks));
                    float seam = 1 - smoothstep(.025, .055 + length(fwidth(bricks)), min(edge.x, edge.y));
                    detail = .92 + Hash2(floor(bricks)) * .17 - seam * .22 + (noise - .5) * .1;
                }
                else if (_Surface > 2.5 && _Surface < 3.5)
                {
                    float brushed = sin(uv.y * 170) * saturate(1 - length(fwidth(uv)) * 45);
                    detail = .94 + noise * .09 + brushed * .035;
                    float polish = pow(saturate(dot(reflect(normalize(_WorldSpaceCameraPos - p), n), normalize(float3(-.4, .8, .3)))), 18);
                    color += polish * .1;
                }
                else if (_Surface > 3.5 && _Surface < 4.5)
                {
                    float weave = sin(uv.x * 130) * sin(uv.y * 130);
                    detail = .96 + noise * .07 + weave * .045 * saturate(1 - length(fwidth(uv)) * 40);
                }
                else if (_Surface > 4.5 && _Surface < 5.5)
                {
                    float facet = pow(saturate(dot(n, normalize(_WorldSpaceCameraPos - p))), 8);
                    detail = .88 + noise * .12 + facet * .23;
                }
                else if (_Surface > 5.5 && _Surface < 6.5)
                {
                    float pores = ValueNoise(uv * 19), veins = abs(noise - .5);
                    detail = .85 + pores * .2 - (1 - smoothstep(.01, .045, veins)) * .17;
                    color = lerp(color, color * half3(.72, 1.14, 1.08), noise * .35);
                }
                else if (_Surface > 6.5 && _Surface < 7.5)
                    detail = .95 + (noise - .5) * .08 + (ValueNoise(uv * 35) - .5) * .055;
                return color * detail;
            }

            half4 _NubikAmbient;
            half4 _NubikFogColor;
            float4 _NubikFog; // x: start distance, y: end distance

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                half4 color : COLOR;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                half3 normalWS : TEXCOORD1;
                half4 color : COLOR;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs position = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = position.positionCS;
                output.positionWS = position.positionWS;
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.color = input.color;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                half3 normal = normalize(input.normalWS);
                half3 albedo = _BaseColor.rgb * lerp(half3(1, 1, 1), input.color.rgb, _VertexColor);
                albedo = SurfaceDetail(input.positionWS, normal, albedo);
                // Per-pixel grass keeps a crisp lawn edge around holes instead of a vertex-colour smear.
                half grass = _VertexColor * step(0.8, normal.y) * step(-0.12, input.positionWS.y);
                albedo = lerp(albedo, _GrassColor.rgb, grass);
                // Subtle striations and fine grain replace the conspicuous square checker pattern.
                float3 cell = floor(input.positionWS * 28.0);
                half grain = frac(sin(dot(cell, float3(12.9898, 78.233, 37.719))) * 43758.5453);
                half strata = sin(input.positionWS.y * 13 + sin(input.positionWS.x * 1.3 + input.positionWS.z * 1.8));
                albedo *= 1.0 + (grain - 0.5) * _Noise * .45 + strata * _VertexColor * (1-grass) * .035;
                // Pebbles in dug ground: round light and dark specks, one possible per 20 cm cell.
                half rock = _VertexColor * (1 - grass);
                float3 pebbleSpace = input.positionWS * 9.0;
                float pebble = frac(sin(dot(floor(pebbleSpace), float3(17.1, 31.7, 47.3))) * 43758.5453);
                float3 pebbleCenter = 0.3 + 0.4 * frac(pebble * float3(13.7, 71.3, 37.9));
                half spot = 1 - smoothstep(0.15, 0.21, length(frac(pebbleSpace) - pebbleCenter));
                albedo *= 1 + rock * spot * (step(0.8, pebble) * 0.45 - step(pebble, 0.16) * 0.3);
                float strataNoise = ValueNoise(input.positionWS.xz * .8);
                float sediment = sin(input.positionWS.y * 17 + strataNoise * 6);
                albedo *= 1 + rock * (sediment * .045 + (ValueNoise(input.positionWS.xy * 6) - .5) * .12);
                // Lawn: soft patches, fine blades and mowing stripes in world space, so the dig patch
                // and the surrounding lawn slabs share one pattern without a seam.
                half lawn = max(grass, _Lawn);
                float2 ground = input.positionWS.xz;
                half patches = ValueNoise(ground * 0.33) - 0.5;
                half blades = ValueNoise(ground * 7.3) - 0.5;
                half stripe = frac(ground.x * 0.21 + 0.3) > 0.5 ? 0.045 : -0.035;
                albedo *= 1.0 + lawn * (patches * 0.28 + blades * 0.12 + stripe);
                albedo = lerp(albedo, albedo * half3(1.14, 1.08, 0.78), lawn * saturate(patches * 2.2));

                Light mainLight = GetMainLight(TransformWorldToShadowCoord(input.positionWS));
                half3 light = mainLight.color * saturate(dot(normal, mainLight.direction) * .8 + .2) * lerp(.25,1,mainLight.shadowAttenuation);
                light += _NubikAmbient.rgb * (0.7 + 0.3 * normal.y);
                #if defined(_ADDITIONAL_LIGHTS)
                int count = GetAdditionalLightsCount();
                for (int index = 0; index < count; index++)
                {
                    Light extra = GetAdditionalLight(index, input.positionWS);
                    light += extra.color * (extra.distanceAttenuation * _ExtraLights) * saturate(dot(normal, extra.direction) * 0.8 + 0.2);
                }
                #endif

                // Glowing finds, runes and lanterns glint now and then; the phase varies with position.
                half glint = pow(saturate(sin(_Time.y * 2.6 + dot(input.positionWS, float3(4.1, 6.3, 5.2)))), 8);
                half3 color = albedo * light + _Emission.rgb * (0.8 + 0.9 * glint);
                float distance = length(input.positionWS - _WorldSpaceCameraPos);
                half fog = saturate((distance - _NubikFog.x) / max(0.01, _NubikFog.y - _NubikFog.x));
                return half4(lerp(color, _NubikFogColor.rgb, fog), 1);
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }
            ZWrite On
            ZTest LEqual
            ColorMask 0
            HLSLPROGRAM
            #pragma vertex ShadowVert
            #pragma fragment ShadowFrag
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            float3 _LightDirection;
            float3 _LightPosition;

            struct ShadowAttributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
            };

            float4 ShadowVert(ShadowAttributes input) : SV_POSITION
            {
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);
                #if _CASTING_PUNCTUAL_LIGHT_SHADOW
                float3 lightDirection = normalize(_LightPosition - positionWS);
                #else
                float3 lightDirection = _LightDirection;
                #endif
                float4 positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, lightDirection));
                #if UNITY_REVERSED_Z
                positionCS.z = min(positionCS.z, UNITY_NEAR_CLIP_VALUE);
                #else
                positionCS.z = max(positionCS.z, UNITY_NEAR_CLIP_VALUE);
                #endif
                return positionCS;
            }

            half4 ShadowFrag() : SV_Target { return 0; }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ZWrite On
            ColorMask R
            HLSLPROGRAM
            #pragma vertex DepthVert
            #pragma fragment DepthFrag
            float4 DepthVert(float4 positionOS : POSITION) : SV_POSITION { return TransformObjectToHClip(positionOS.xyz); }
            half DepthFrag() : SV_Target { return 0; }
            ENDHLSL
        }
    }
}
