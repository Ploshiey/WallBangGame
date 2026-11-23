// Soft ceramic / porcelain. Opaque, so unlike Custom/FrostedGlass it does NOT
// depend on the KawaseBlur feature or on there being anything behind it — it
// works as a wall, floor or prop material on its own.
//
// The look comes from wrapped diffuse (no hard terminator), a tinted shadow
// side, a tight gloss highlight, a warm Fresnel rim and cheap translucency
// where light passes through thin sections.
//
// Has ShadowCaster, DepthOnly and DepthNormals passes, so it casts shadows,
// writes into _CameraDepthTexture (which Custom/FrostedGlass reads for its
// thickness term) and is picked up by the SSAO feature.
Shader "Custom/SoftCeramic"
{
    Properties
    {
        [MainColor] _BaseColor      ("Base Color", Color) = (0.94, 0.93, 0.92, 1)
        _ShadowColor                ("Shadow Tint", Color) = (0.60, 0.58, 0.66, 1)
        _WrapAmount                 ("Diffuse Wrap", Range(0,1)) = 0.55
        _AmbientStrength            ("Ambient Strength", Range(0,2)) = 1.0

        [Space(8)]
        _Smoothness                 ("Smoothness", Range(0,1)) = 0.90
        _SpecularTint               ("Specular Tint", Color) = (1,1,1,1)
        _SpecularStrength           ("Specular Strength", Range(0,4)) = 1.4

        [Space(8)]
        _RimColor                   ("Rim Color", Color) = (1.0, 0.72, 0.35, 1)
        _RimPower                   ("Rim Power", Range(0.5,12)) = 4.0
        _RimStrength                ("Rim Strength", Range(0,2)) = 0.55

        [Space(8)]
        _TranslucencyColor          ("Translucency Color", Color) = (1.0, 0.55, 0.40, 1)
        _TranslucencyPower          ("Translucency Power", Range(1,16)) = 5.0
        _TranslucencyStrength       ("Translucency Strength", Range(0,2)) = 0.35

        [Space(8)]
        [Enum(UnityEngine.Rendering.CullMode)] _Cull ("Cull", Float) = 2
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
            "RenderType" = "Opaque"
            "Queue" = "Geometry"
            "RenderPipeline" = "UniversalPipeline"
        }

        HLSLINCLUDE
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                half4 _ShadowColor;
                half  _WrapAmount;
                half  _AmbientStrength;
                half  _Smoothness;
                half4 _SpecularTint;
                half  _SpecularStrength;
                half4 _RimColor;
                half  _RimPower;
                half  _RimStrength;
                half4 _TranslucencyColor;
                half  _TranslucencyPower;
                half  _TranslucencyStrength;
                float _Cull;
                half  _FlipNormals;
            CBUFFER_END
        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            ZWrite On
            Cull [_Cull]

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0

            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile _ _FORWARD_PLUS
            #pragma multi_compile_fog
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

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

                VertexPositionInputs p = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs n = GetVertexNormalInputs(input.normalOS);

                output.positionCS = p.positionCS;
                output.positionWS = p.positionWS;
                output.normalWS = n.normalWS;
                output.fogCoord = ComputeFogFactor(p.positionCS.z);
                return output;
            }

            // Softened N.L. A hard terminator is what makes a white surface read
            // as plastic; porcelain wraps light well past 90 degrees.
            half Wrapped(half3 normalWS, half3 lightDir)
            {
                half ndl = dot(normalWS, lightDir);
                return saturate((ndl + _WrapAmount) / (1.0h + _WrapAmount));
            }

            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                half3 N = NormalizeNormalPerPixel(input.normalWS) * (1.0h - 2.0h * _FlipNormals);
                half3 V = normalize(GetWorldSpaceViewDir(input.positionWS));
                half specPower = exp2(_Smoothness * 11.0h) + 1.0h;

                // Needed by LIGHT_LOOP_BEGIN on the Forward+ path.
                InputData inputData = (InputData)0;
                inputData.positionWS = input.positionWS;
                inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(input.positionCS);

                Light mainLight = GetMainLight(TransformWorldToShadowCoord(input.positionWS));
                half shadow = mainLight.shadowAttenuation;

                half3 albedo = _BaseColor.rgb;
                half wrap = Wrapped(N, mainLight.direction) * lerp(1.0h, shadow, 0.85h);

                half3 col = lerp(albedo * _ShadowColor.rgb, albedo, wrap) * mainLight.color;

                // Ambient, so the unlit side reads as white rather than black.
                col += SampleSH(N) * albedo * _AmbientStrength;

                half3 H = normalize(mainLight.direction + V);
                half spec = pow(saturate(dot(N, H)), specPower);
                col += mainLight.color * _SpecularTint.rgb * (spec * _SpecularStrength * shadow);

                #if defined(_ADDITIONAL_LIGHTS)
                    uint lightCount = GetAdditionalLightsCount();
                    LIGHT_LOOP_BEGIN(lightCount)
                        Light addLight = GetAdditionalLight(lightIndex, input.positionWS, half4(1,1,1,1));
                        half atten = addLight.distanceAttenuation * addLight.shadowAttenuation;
                        half addWrap = Wrapped(N, addLight.direction) * atten;

                        col += addLight.color * albedo * addWrap;

                        half3 addH = normalize(addLight.direction + V);
                        half addSpec = pow(saturate(dot(N, addH)), specPower);
                        col += addLight.color * _SpecularTint.rgb * (addSpec * _SpecularStrength * atten);
                    LIGHT_LOOP_END
                #endif

                // Light bleeding through thin sections, seen when looking toward
                // the light. Cheap stand-in for subsurface scattering.
                half trans = pow(saturate(dot(V, -mainLight.direction)), _TranslucencyPower);
                col += _TranslucencyColor.rgb * (trans * _TranslucencyStrength);

                // Warm Fresnel edge.
                half rim = pow(1.0h - saturate(dot(N, V)), _RimPower);
                col += _RimColor.rgb * (rim * _RimStrength);

                col = MixFog(col, input.fogCoord);
                return half4(col, 1.0h);
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
            Cull [_Cull]

            HLSLPROGRAM
            #pragma vertex shadowVert
            #pragma fragment shadowFrag
            #pragma target 3.0
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            float3 _LightDirection;
            float3 _LightPosition;

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings shadowVert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);

                #if defined(_CASTING_PUNCTUAL_LIGHT_SHADOW)
                    float3 lightDirectionWS = normalize(_LightPosition - positionWS);
                #else
                    float3 lightDirectionWS = _LightDirection;
                #endif

                float4 positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, lightDirectionWS));
                #if UNITY_REVERSED_Z
                    positionCS.z = min(positionCS.z, UNITY_NEAR_CLIP_VALUE);
                #else
                    positionCS.z = max(positionCS.z, UNITY_NEAR_CLIP_VALUE);
                #endif

                output.positionCS = positionCS;
                return output;
            }

            half4 shadowFrag(Varyings input) : SV_Target
            {
                return 0;
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }

            ZWrite On
            ColorMask R
            Cull [_Cull]

            HLSLPROGRAM
            #pragma vertex depthVert
            #pragma fragment depthFrag
            #pragma target 3.0
            #pragma multi_compile_instancing

            struct Attributes
            {
                float4 positionOS : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings depthVert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                return output;
            }

            half4 depthFrag(Varyings input) : SV_Target
            {
                return 0;
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormals" }

            ZWrite On
            Cull [_Cull]

            HLSLPROGRAM
            #pragma vertex depthNormalsVert
            #pragma fragment depthNormalsFrag
            #pragma target 3.0
            #pragma multi_compile_instancing

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 normalWS   : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings depthNormalsVert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.normalWS = GetVertexNormalInputs(input.normalOS).normalWS;
                return output;
            }

            half4 depthNormalsFrag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                return half4(NormalizeNormalPerPixel(input.normalWS) * (1.0h - 2.0h * _FlipNormals), 0.0);
            }
            ENDHLSL
        }
    }

    Fallback Off
}
