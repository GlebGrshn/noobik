// Small URP forward shader for the whole prototype: vertex-coloured terrain,
// textured props, main light shadows, extra lights (headlamp) and a depth fog.
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
        _Surface("Surface layer + 1 (0: plain); see Shapes", Float) = 0
        _Surfaces("Surface set (normal xy, albedo, mask)", 2DArray) = "" {}
        _SurfaceScale("Surface texture density", Float) = 1
        _Lava("Molten lava surface", Range(0, 1)) = 0
        _Water("Water surface", Range(0, 1)) = 0
        _Sway("Wind sway (grass 1, crowns less)", Range(0, 1)) = 0
        _Detail("Rock detail (normal xy, height, cracks)", 2D) = "gray" {}
        _DetailStrength("Rock detail strength", Range(0, 1)) = 0
        _DetailScale("Rock detail tiles per metre", Float) = 0.6
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
            half _SurfaceScale;
            half _Lava;
            half _Water;
            half _Sway;
            half _DetailStrength;
            float _DetailScale;
        CBUFFER_END
        TEXTURE2D(_Detail);
        SAMPLER(sampler_Detail);
        TEXTURE2D_ARRAY(_Surfaces);
        SAMPLER(sampler_Surfaces);
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
            #pragma require 2darray
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            float _NubikLowDetail; // graphics "Low": one texture sample instead of three, no bump on mapped props and rock

            float Hash2(float2 p) { return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453); }

            float ValueNoise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                f = f * f * (3 - 2 * f);
                return lerp(lerp(Hash2(i), Hash2(i + float2(1, 0)), f.x), lerp(Hash2(i + float2(0, 1)), Hash2(i + 1), f.x), f.y);
            }

            // Per layer (surface - 1): texture tiles per metre, bump strength, highlight tightness and strength.
            // Wood, masonry, metal, cloth, crystal, skin, plaster, roof, planks, painted, rubber, wallpaper, rug, leather, bark, brick,
            // grass, leaves, soil, fur.
            static const float SurfaceTiles[20] = { 2, .8, 2, 4, 1.6, 1.2, .66, 1, 1, 2, 10, 1.66, .7, 3, 1.2, 1.1, 1.8, 1.4, 1.2, 4 };
            static const half SurfaceBump[20] = { .7, 1, .55, .7, 1, .9, .55, .9, .85, .7, .8, .5, .6, .8, 1, .9, .7, .9, .9, .5 };
            static const half SurfaceGloss[20] = { 24, 12, 48, 6, 90, 30, 8, 20, 18, 56, 10, 12, 4, 24, 6, 10, 8, 14, 6, 6 };
            static const half SurfaceShine[20] = { .05, .03, .45, 0, .6, .25, .02, .08, .05, .3, .06, .04, 0, .12, 0, .03, .02, .05, 0, 0 };

            half4 SampleSurface(float2 uv, int layer) { return SAMPLE_TEXTURE2D_ARRAY(_Surfaces, sampler_Surfaces, uv, layer); }

            /// Boxes carry UVs in metres and tangents (uv.z = 1); spheres and custom meshes are mapped by their
            /// scaled object-space position. Either way the texture sticks to the object.
            void SurfaceTexture(float3 uv, half4 tangentWS, float3 positionOS, half3 normalOS, int layer, inout half3 normal, inout half3 albedo, out half mask)
            {
                float tiles = SurfaceTiles[layer] * _SurfaceScale;
                half bump = SurfaceBump[layer];
                half4 s;
                if (uv.z > .5)
                {
                    s = SampleSurface(uv.xy * tiles, layer);
                    half2 t = (s.xy * 2 - 1) * bump;
                    half3 tangent = normalize(tangentWS.xyz - normal * dot(normal, tangentWS.xyz));
                    half3 bitangent = cross(normal, tangent) * (tangentWS.w < 0 ? -1 : 1);
                    normal = normalize(tangent * t.x + bitangent * t.y + normal);
                }
                else if (_NubikLowDetail > .5)
                {
                    // One sample along the main axis, no bump.
                    float3 p = positionOS * tiles;
                    half3 a = abs(normalOS);
                    s = SampleSurface(a.x > a.y && a.x > a.z ? p.zy : a.y > a.z ? p.xz : p.xy, layer);
                }
                else
                {
                    float3 p = positionOS * tiles;
                    half3 n = normalize(normalOS);
                    half3 blend = pow(abs(n), 4);
                    blend /= blend.x + blend.y + blend.z;
                    half4 sx = SampleSurface(p.zy, layer), sy = SampleSurface(p.xz, layer), sz = SampleSurface(p.xy, layer);
                    s = sx * blend.x + sy * blend.y + sz * blend.z;
                    half3 nx = half3((sx.xy * 2 - 1) * bump + n.zy, n.x);
                    half3 ny = half3((sy.xy * 2 - 1) * bump + n.xz, n.y);
                    half3 nz = half3((sz.xy * 2 - 1) * bump + n.xy, n.z);
                    normal = normalize(TransformObjectToWorldNormal(nx.zyx * blend.x + ny.xzy * blend.y + nz * blend.z));
                }
                albedo *= s.b * 2;
                mask = s.a;
                // What the mask means depends on the material.
                if (layer == 0) albedo *= 1 - mask * .25;                                        // wood pores
                else if (layer == 1) albedo = lerp(albedo, albedo * .45 + .2, mask);             // mortar
                else if (layer == 2) albedo += mask * .12;                                       // scratches
                else if (layer == 4) albedo += mask * .25;                                       // facet edges
                else if (layer == 5) albedo = lerp(albedo, albedo * half3(.6, 1.05, .95), mask);  // skin spots
                else if (layer == 7) albedo *= 1 - mask * .45;                                   // roof overlap
                else if (layer == 8) albedo *= 1 - mask * .7;                                    // board gaps
                else if (layer == 9) albedo = lerp(albedo, half3(.34, .35, .37), mask);          // chipped paint
                else if (layer == 11) albedo = lerp(albedo, albedo * 1.12 + .07, mask);          // wallpaper motif
                else if (layer == 12) albedo = lerp(albedo, half3(.88, .72, .44), mask);         // rug ornament
                else if (layer == 15) albedo = lerp(albedo, half3(.56, .54, .5), mask);          // brick mortar
                else if (layer == 16) albedo *= 1 + mask * .2;                                   // sunlit blade tips
                else if (layer == 17) albedo *= lerp(.82, 1.18, mask);                           // one leaf lighter than the next
                else if (layer == 18) albedo = lerp(albedo, half3(.55, .52, .48), mask * .7);    // pebbles in the soil
            }

            /// Triplanar rock relief: bends the normal, darkens hollows and fractures. Three samples, only where needed.
            half RockRelief(float3 p, inout half3 normal, inout half3 albedo, half amount)
            {
                float3 uv = p * _DetailScale;
                if (_NubikLowDetail > .5)
                {
                    half3 a = abs(normal);
                    half4 one = SAMPLE_TEXTURE2D(_Detail, sampler_Detail, a.x > a.y && a.x > a.z ? uv.zy : a.y > a.z ? uv.xz : uv.xy);
                    half flat = one.a * smoothstep(0.3, 0.7, ValueNoise(p.xz * 0.23 + p.y * 0.31)) * amount * amount;
                    albedo *= lerp(1, 0.74 + 0.38 * one.b, amount) * (1 - flat * 0.6);
                    return flat;
                }
                half3 blend = pow(abs(normal), 4);
                blend /= blend.x + blend.y + blend.z;
                half4 sx = SAMPLE_TEXTURE2D(_Detail, sampler_Detail, uv.zy);
                half4 sy = SAMPLE_TEXTURE2D(_Detail, sampler_Detail, uv.xz);
                half4 sz = SAMPLE_TEXTURE2D(_Detail, sampler_Detail, uv.xy);
                // Whiteout blend of the three projected tangent normals.
                half3 nx = half3((sx.xy * 2 - 1) * amount + normal.zy, normal.x);
                half3 ny = half3((sy.xy * 2 - 1) * amount + normal.xz, normal.y);
                half3 nz = half3((sz.xy * 2 - 1) * amount + normal.xy, normal.z);
                normal = normalize(nx.zyx * blend.x + ny.xzy * blend.y + nz * blend.z);
                half height = sx.b * blend.x + sy.b * blend.y + sz.b * blend.z;
                half cracks = sx.a * blend.x + sy.a * blend.y + sz.a * blend.z;
                // Fractures come in patches, so walls do not repeat the tile; soft ground barely cracks.
                cracks *= smoothstep(0.3, 0.7, ValueNoise(p.xz * 0.23 + p.y * 0.31)) * amount * amount;
                albedo *= lerp(1, 0.74 + 0.38 * height, amount) * (1 - cracks * 0.6);
                return cracks;
            }

            half4 _NubikAmbient;
            float4 _NubikMagma; // x: depth where cracks start to glow, y: depth of full glow
            half4 _NubikFogColor;
            float4 _NubikFog; // x: start distance, y: end distance

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float4 tangentOS : TANGENT;
                float3 uv : TEXCOORD0;
                half4 color : COLOR;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                half3 normalWS : TEXCOORD1;
                float3 uv : TEXCOORD2;
                half4 tangentWS : TEXCOORD3;
                // Object-space position in metres (object scale applied) and normal, for position-mapped textures.
                float3 positionOS : TEXCOORD4;
                half3 normalOS : TEXCOORD5;
                half4 color : COLOR;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                if (_Sway > 0)
                {
                    // Wind: blades bend from the ground up, crowns rock a little; gusts roll across the yard.
                    float t = _Time.y;
                    float2 gust = float2(sin(t * 1.7 + positionWS.x * .6 + positionWS.z * .35), cos(t * 1.3 + positionWS.z * .5 - positionWS.x * .3));
                    positionWS.xz += gust * (.06 * _Sway * saturate(positionWS.y * 2.5) * (.7 + .3 * sin(t * .4 + positionWS.x * .05)));
                }
                output.positionCS = TransformWorldToHClip(positionWS);
                output.positionWS = positionWS;
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.color = input.color;
                output.uv = input.uv;
                output.tangentWS = half4(TransformObjectToWorldDir(input.tangentOS.xyz), input.tangentOS.w * GetOddNegativeScale());
                float4x4 world = GetObjectToWorldMatrix();
                float3 scale = float3(length(world._m00_m10_m20), length(world._m01_m11_m21), length(world._m02_m12_m22));
                output.positionOS = input.positionOS.xyz * scale;
                output.normalOS = input.normalOS;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                half3 normal = normalize(input.normalWS);
                half3 view = normalize(_WorldSpaceCameraPos - input.positionWS);
                half3 albedo = _BaseColor.rgb * lerp(half3(1, 1, 1), input.color.rgb, _VertexColor);
                if (_Water > .5)
                {
                    // Pond: drifting ripples bend the normal; at a grazing angle the water mirrors the sky.
                    float2 w = input.positionWS.xz;
                    float t = _Time.y;
                    half2 ripple = half2(ValueNoise(w * 3 + t * .5) - ValueNoise(w * 3 + float2(.7, .3) + t * .5),
                        ValueNoise(w * 2.3 - t * .4) - ValueNoise(w * 2.3 + float2(.2, .9) - t * .4));
                    normal = normalize(half3(ripple.x * .4, 1, ripple.y * .4));
                    half fresnel = pow(1 - saturate(dot(normal, view)), 3);
                    albedo = lerp(albedo, half3(.66, .8, .88), fresnel * .85);
                }
                int layer = -1;
                half mask = 0;
                if (_Surface > .5)
                {
                    layer = clamp((int)round(_Surface) - 1, 0, 19);
                    SurfaceTexture(input.uv, input.tangentWS, input.positionOS, input.normalOS, layer, normal, albedo, mask);
                }
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
                // Relief of dug rock by its hardness (vertex alpha); props have their own surface textures.
                half relief = _DetailStrength * lerp(1, input.color.a, _VertexColor) * (1 - grass);
                half cracks = 0;
                if (relief > 0.01) cracks = RockRelief(input.positionWS, normal, albedo, relief);
                // Near the bottom the cracks of dug rock glow with magma, breathing slowly.
                half heat = _NubikMagma.y > _NubikMagma.x ? _VertexColor * saturate((-input.positionWS.y - _NubikMagma.x) / (_NubikMagma.y - _NubikMagma.x)) : 0;
                half hot = smoothstep(.42, .72, ValueNoise(input.positionWS.xz * .35 + input.positionWS.y * .21));
                half3 magma = heat * hot * saturate(cracks * 1.4) * half3(1, .34, .06) * (.75 + .35 * sin(_Time.y * 1.6 + dot(input.positionWS, float3(.7, .4, .9))));
                // Lava: dark crust drifting over a bright molten flow.
                half3 molten = 0;
                if (_Lava > .5)
                {
                    float2 q = input.positionWS.xz * 1.4;
                    float t = _Time.y;
                    half flow = ValueNoise(q + float2(t * .13, t * .07)) * .6 + ValueNoise(q * 2.3 - float2(t * .09, -t * .16)) * .4;
                    half crust = smoothstep(.5, .66, flow);
                    albedo = lerp(albedo * .4, albedo, crust);
                    molten = _Emission.rgb * (1 - crust) * (.8 + .3 * sin(t * 2.3 + flow * 11));
                }
                // Lawn: soft patches, fine blades and mowing stripes in world space, so the dig patch
                // and the surrounding lawn slabs share one pattern without a seam.
                half lawn = max(grass, _Lawn);
                float2 ground = input.positionWS.xz;
                half patches = ValueNoise(ground * 0.33) - 0.5;
                half blades = ValueNoise(ground * 7.3) - 0.5;
                half stripe = frac(ground.x * 0.21 + 0.3) > 0.5 ? 0.045 : -0.035;
                albedo *= 1.0 + lawn * (patches * 0.28 + blades * 0.12 + stripe);
                albedo = lerp(albedo, albedo * half3(1.14, 1.08, 0.78), lawn * saturate(patches * 2.2));
                if (lawn > 0)
                {
                    // Blades of grass from the surface set, then clover patches, sun-dried spots and scattered flowers.
                    half4 blade = SampleSurface(ground * 1.8, 16);
                    albedo *= lerp(1, blade.b * 2, lawn * .85);
                    if (_NubikLowDetail < .5) normal = normalize(normal + half3(blade.x * 2 - 1, 0, blade.y * 2 - 1) * (.6 * lawn));
                    half clover = smoothstep(.6, .72, ValueNoise(ground * .9 + 11.3));
                    albedo *= lerp(half3(1, 1, 1), half3(.8, 1.03, .84), lawn * clover * .8);
                    half dry = smoothstep(.68, .8, ValueNoise(ground * .42 + 5.7));
                    albedo = lerp(albedo, albedo * half3(1.25, 1.12, .7), lawn * dry * .55);
                    float2 cell = floor(ground * 5.5);
                    float pick = Hash2(cell);
                    float2 centre = (float2(Hash2(cell + 3.1), Hash2(cell + 7.7)) - .5) * .5;
                    half bloom = (1 - smoothstep(.035, .07, length(frac(ground * 5.5) - .5 - centre))) * step(.975, pick) * lawn;
                    half3 petal = pick > .99 ? half3(1, .84, .3) : half3(.94, .93, .88);
                    albedo = lerp(albedo, petal, bloom * .75);
                }

                Light mainLight = GetMainLight(TransformWorldToShadowCoord(input.positionWS));
                half shadow = lerp(.25, 1, mainLight.shadowAttenuation);
                half3 light = mainLight.color * saturate(dot(normal, mainLight.direction) * .8 + .2) * shadow;
                light += _NubikAmbient.rgb * (0.7 + 0.3 * normal.y);
                // Highlights: metal, fresh paint, crystal and wet skin catch the sun and the headlamp.
                half3 shine = 0;
                half gloss = 0, strength = 0;
                if (_Water > .5) { gloss = 140; strength = .9; shine = mainLight.color * pow(saturate(dot(normal, normalize(mainLight.direction + view))), gloss) * strength * shadow; }
                if (layer >= 0)
                {
                    gloss = SurfaceGloss[layer];
                    strength = SurfaceShine[layer] * (layer == 9 ? lerp(1, 1.4, mask) : 1);
                    if (strength > 0) shine = mainLight.color * pow(saturate(dot(normal, normalize(mainLight.direction + view))), gloss) * strength * shadow;
                }
                #if defined(_ADDITIONAL_LIGHTS)
                int count = GetAdditionalLightsCount();
                for (int index = 0; index < count; index++)
                {
                    Light extra = GetAdditionalLight(index, input.positionWS);
                    half3 reach = extra.color * (extra.distanceAttenuation * _ExtraLights);
                    light += reach * saturate(dot(normal, extra.direction) * 0.8 + 0.2);
                    if (strength > 0) shine += reach * pow(saturate(dot(normal, normalize(extra.direction + view))), gloss) * strength;
                }
                #endif

                // Glowing finds, runes and lanterns glint now and then; the phase varies with position.
                half glint = pow(saturate(sin(_Time.y * 2.6 + dot(input.positionWS, float3(4.1, 6.3, 5.2)))), 8);
                // Metal highlights take the metal's colour; others stay white.
                half3 emission = _Lava > .5 ? molten : _Emission.rgb * (0.8 + 0.9 * glint);
                half3 color = albedo * light + shine * (layer == 2 ? albedo * 1.6 : 1) + emission + magma;
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
