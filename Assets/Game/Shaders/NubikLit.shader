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
        _GrassColor("Grass on flat ground (vertex colour mode)", Color) = (0.34, 0.6, 0.22, 1)
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
                // Per-pixel grass keeps a crisp lawn edge around holes instead of a vertex-colour smear.
                half grass = _VertexColor * step(0.8, normal.y) * step(-0.12, input.positionWS.y);
                albedo = lerp(albedo, _GrassColor.rgb, grass);
                float3 cell = floor(input.positionWS * 5.0);
                half grain = frac(sin(dot(cell, float3(12.9898, 78.233, 37.719))) * 43758.5453);
                albedo *= 1.0 + (grain - 0.5) * _Noise;

                Light mainLight = GetMainLight(TransformWorldToShadowCoord(input.positionWS));
                half3 light = mainLight.color * saturate(dot(normal, mainLight.direction)) * mainLight.shadowAttenuation;
                light += _NubikAmbient.rgb * (0.7 + 0.3 * normal.y);
                #if defined(_ADDITIONAL_LIGHTS)
                int count = GetAdditionalLightsCount();
                for (int index = 0; index < count; index++)
                {
                    Light extra = GetAdditionalLight(index, input.positionWS);
                    light += extra.color * (extra.distanceAttenuation * _ExtraLights) * saturate(dot(normal, extra.direction) * 0.8 + 0.2);
                }
                #endif

                half3 color = albedo * light + _Emission.rgb;
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
