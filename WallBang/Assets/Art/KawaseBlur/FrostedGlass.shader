// Frosted glass / milky acrylic. Reads the blurred backdrop published by the
// KawaseBlur renderer feature as the global _blurTexture, then gives it an actual
// surface: absorption tint, scattering (milkiness), thickness-driven density,
// a Fresnel rim and main-light shading.
//
// Requires: the KawaseBlur renderer feature on the active renderer, with
// copyToFramebuffer OFF (so it publishes _blurTexture instead of overwriting
// the camera colour).
Shader "Custom/FrostedGlass"
{
    Properties
    {
        [MainColor] _BaseColor      ("Tint", Color) = (1.0, 0.72, 0.66, 1.0)
        _TintStrength               ("Tint Strength (absorption)", Range(0,1)) = 0.8
        _Milkiness                  ("Milkiness (scattering)", Range(0,1)) = 0.45
        _Opacity                    ("Opacity", Range(0,1)) = 1.0

        [Space(8)]
        [Toggle(_THICKNESS_ON)] _UseThickness ("Use Depth Thickness", Float) = 1
        _ThicknessDistance          ("Thickness Distance", Range(0.01, 20)) = 2.5
        _ThicknessStrength          ("Thickness Strength", Range(0,1)) = 0.6

        [Space(8)]
        _FresnelColor               ("Fresnel Color", Color) = (1,1,1,1)
        _FresnelPower               ("Fresnel Power", Range(0.5, 10)) = 3.0
        _FresnelStrength            ("Fresnel Strength", Range(0,1)) = 0.35

        [Space(8)]
        _LightInfluence             ("Light Influence", Range(0,1)) = 0.35
        _Smoothness                 ("Smoothness", Range(0,1)) = 0.75
        _SpecularStrength           ("Specular Strength", Range(0,2)) = 0.35

        [Space(8)]
        [Enum(UnityEngine.Rendering.CullMode)] _Cull ("Cull", Float) = 2
        [Enum(Off,0,On,1)] _ZWriteMode ("ZWrite", Float) = 1
        // Mirrored transforms (negative scale on an odd number of axes) come out
        // of the normal transform pointing inward. Tick this on those.
        // (ShaderLab's Enum attribute cannot parse a negative literal, so this
        // is a 0/1 checkbox and the sign is derived in HLSL.)
        [ToggleUI] _FlipNormals ("Flip Normals", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
            "IgnoreProjector" = "True"
        }

        Pass
        {
            Name "FrostedGlassForward"
            Tags { "LightMode" = "UniversalForward" }

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite [_ZWriteMode]
            Cull [_Cull]

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #pragma shader_feature_local_fragment _THICKNESS_ON
            #pragma multi_compile_fog
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            // sampler_LinearClamp. Reachable transitively via Lighting.hlsl today,
            // but that is incidental — include it outright.
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/GlobalSamplers.hlsl"

            // Published by the KawaseBlur renderer feature via SetGlobalTexture.
            // Declared here only — never as a material Property, or the material's
            // own (empty) slot would shadow the global.
            TEXTURE2D_X(_blurTexture);

            CBUFFER_START(UnityPerMaterial)
                half4  _BaseColor;
                half   _TintStrength;
                half   _Milkiness;
                half   _Opacity;
                float  _ThicknessDistance;
                half   _ThicknessStrength;
                half4  _FresnelColor;
                half   _FresnelPower;
                half   _FresnelStrength;
                half   _LightInfluence;
                half   _Smoothness;
                half   _SpecularStrength;
                // Not read in HLSL, but every Property must live here or the
                // SRP Batcher rejects the shader.
                float  _UseThickness;
                float  _Cull;
                float  _ZWriteMode;
                half   _FlipNormals;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS   : TEXCOORD1;
                float  fogCoord   : TEXCOORD2;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings vert(Attributes input)
            {
                Varyings output = (Varyings)0;

                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                VertexPositionInputs positions = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs normals = GetVertexNormalInputs(input.normalOS);

                output.positionCS = positions.positionCS;
                output.positionWS = positions.positionWS;
                output.normalWS = normals.normalWS;
                output.fogCoord = ComputeFogFactor(positions.positionCS.z);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                float2 screenUV = GetNormalizedScreenSpaceUV(input.positionCS);
                half3 backdrop = SAMPLE_TEXTURE2D_X(_blurTexture, sampler_LinearClamp, screenUV).rgb;

                half3 normalWS = normalize(input.normalWS) * (1.0h - 2.0h * _FlipNormals);
                half3 viewDirWS = normalize(GetWorldSpaceViewDir(input.positionWS));

                // How much glass is the view ray travelling through? Distance from this
                // surface to the opaque scene behind it. Without this, every frosted
                // surface is equally milky and overlapping ones are indistinguishable.
                half density = 1.0h;
                #if defined(_THICKNESS_ON)
                    float rawDepth = SampleSceneDepth(screenUV);
                    float sceneDepth = LinearEyeDepth(rawDepth, _ZBufferParams);
                    float surfaceDepth = -TransformWorldToView(input.positionWS).z;
                    half thickness = saturate((sceneDepth - surfaceDepth) / _ThicknessDistance);
                    density = lerp(1.0h, thickness, _ThicknessStrength);
                #endif

                // Absorption: the backdrop picks up the tint as it passes through.
                half3 absorption = lerp(half3(1.0h, 1.0h, 1.0h), _BaseColor.rgb, _TintStrength * density);
                half3 col = backdrop * absorption;

                // Scattering: milky haze replacing the backdrop entirely.
                col = lerp(col, _BaseColor.rgb, _Milkiness * density);

                // Main light, wrapped so faces read apart without going black.
                Light mainLight = GetMainLight();
                half wrap = saturate(dot(normalWS, mainLight.direction) * 0.5h + 0.5h);
                col *= lerp(1.0h, 0.6h + 0.8h * wrap, _LightInfluence);

                half3 halfVec = normalize(mainLight.direction + viewDirWS);
                half specPower = exp2(_Smoothness * 11.0h) + 1.0h;
                half spec = pow(saturate(dot(normalWS, halfVec)), specPower);
                col += mainLight.color * (spec * _SpecularStrength);

                // Fresnel rim — what actually makes an edge visible against an
                // identical blurred backdrop.
                half fresnel = pow(1.0h - saturate(dot(normalWS, viewDirWS)), _FresnelPower);
                col = lerp(col, _FresnelColor.rgb, saturate(fresnel * _FresnelStrength));

                col = MixFog(col, input.fogCoord);

                return half4(col, _Opacity * _BaseColor.a);
            }
            ENDHLSL
        }
    }

    Fallback Off
}
