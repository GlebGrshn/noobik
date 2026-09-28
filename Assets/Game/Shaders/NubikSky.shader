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
            struct Varyings { float4 positionCS:SV_POSITION; float3 direction:TEXCOORD0; };
            Varyings Vert(float4 position:POSITION)
            {
                Varyings o; o.positionCS=TransformObjectToHClip(position.xyz); o.direction=position.xyz; return o;
            }
            half4 Frag(Varyings i):SV_Target
            {
                float3 d=normalize(i.direction);
                half3 horizon=half3(.78,.83,.73);
                half3 sky=lerp(horizon,_Tint.rgb,saturate(d.y*1.5));
                float sun=dot(d,normalize(float3(-.4,.65,-.6)));
                sky+=half3(1,.77,.39)*pow(saturate(sun),64)*.18;
                sky=lerp(sky,half3(1,.95,.72),smoothstep(.9985,.9992,sun));
                return half4(sky,1);
            }
            ENDHLSL
        }
    }
}
