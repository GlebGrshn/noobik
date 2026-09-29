// Sky: a deep zenith over a warm hazy horizon, a soft sun with a golden halo, and two layers of drifting
// procedural clouds (fluffy low ones and thin high streaks). Below the horizon it fades into the far fields' haze.
Shader "Nubik/Sky"
{
    Properties { _Tint("Sky tint", Color) = (0.32,0.62,0.75,1) }
    SubShader
    {
        Tags { "Queue"="Background" "RenderType"="Background" "PreviewType"="Skybox" "RenderPipeline"="UniversalPipeline" }
        Cull Off ZWrite Off
        Pass
        {
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            half4 _Tint;
            float _NubikLowDetail;
            struct Varyings { float4 positionCS:SV_POSITION; float3 direction:TEXCOORD0; };

            float Hash(float2 p) { return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453); }
            float Noise(float2 p)
            {
                float2 i = floor(p), f = frac(p);
                f = f * f * (3 - 2 * f);
                return lerp(lerp(Hash(i), Hash(i + float2(1, 0)), f.x), lerp(Hash(i + float2(0, 1)), Hash(i + 1), f.x), f.y);
            }
            float Fbm(float2 p)
            {
                float v = 0, a = .5;
                for (int i = 0; i < 4; i++) { v += Noise(p) * a; p = p * 2.03 + 17.1; a *= .5; }
                return v;
            }

            Varyings Vert(float4 position:POSITION)
            {
                Varyings o; o.positionCS=TransformObjectToHClip(position.xyz); o.direction=position.xyz; return o;
            }

            half4 Frag(Varyings i):SV_Target
            {
                float3 d = normalize(i.direction);
                float3 sunDir = normalize(float3(-.4, .65, -.6));
                half3 horizon = half3(.84, .86, .76);
                half3 zenith = _Tint.rgb * half3(.78, .9, 1.06);
                float up = saturate(d.y);
                half3 sky = lerp(horizon, _Tint.rgb, saturate(up * 2.2));
                sky = lerp(sky, zenith, saturate((up - .45) * 1.6));
                float sun = dot(d, sunDir);
                // Warm halo around the sun and a gentle glow along the horizon beneath it.
                sky += half3(1, .74, .42) * (pow(saturate(sun), 8) * .12 + pow(saturate(sun), 64) * .22);
                sky += half3(1, .8, .55) * pow(saturate(1 - abs(d.y)), 6) * saturate(dot(normalize(d.xz + 1e-4), normalize(sunDir.xz))) * .08;
                sky = lerp(sky, half3(1, .96, .78), smoothstep(.9982, .9992, sun));
                if (d.y > .01 && _NubikLowDetail < .5)
                {
                    float t = _Time.y;
                    // Low fluffy clouds: projected on a flat ceiling, thinning towards the horizon.
                    float2 low = d.xz / (d.y + .12) * .55 + float2(t * .004, t * .0015);
                    float puffy = Fbm(low * 1.3);
                    float cover = smoothstep(.52, .72, puffy) * smoothstep(.02, .22, d.y);
                    half3 cloud = lerp(half3(.78, .82, .88), half3(1, .99, .96), saturate((puffy - .5) * 3 + pow(saturate(sun), 4) * .4));
                    sky = lerp(sky, cloud, cover * .85);
                    // High thin streaks drawn out along one direction.
                    float2 high = d.xz / (d.y + .05) * float2(.35, 1.4) + float2(t * .007, 0);
                    float streak = smoothstep(.56, .8, Fbm(high * 1.7)) * smoothstep(.1, .5, d.y) * .35;
                    sky = lerp(sky, half3(1, 1, 1), streak);
                }
                // Below the horizon: the haze of the fields.
                sky = lerp(sky, horizon * .92, saturate(-d.y * 6));
                return half4(sky, 1);
            }
            ENDHLSL
        }
    }
}
