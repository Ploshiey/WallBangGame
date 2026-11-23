Shader "Custom/RenderFeature/KawaseBlur"
{
    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "RenderPipeline" = "UniversalPipeline"
        }

        LOD 100
        ZTest Always
        ZWrite Off
        Cull Off

        Pass
        {
            Name "KawaseBlur"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            // Blit.hlsl draws a procedural fullscreen triangle, which GLES2 cannot do.
            #pragma exclude_renderers gles

            // Include order matters. Blit.hlsl uses TEXTURE2D_X, which is defined in
            // TextureXR.hlsl — pulled in by URP's Core.hlsl, not by Common.hlsl.
            // Including Blit.hlsl on its own fails with "unrecognized identifier
            // 'TEXTURE2D_X'". This is the same order URP's own Bloom.shader uses.
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            // Provides Vert, Varyings, _BlitTexture, _BlitScaleBias, _BlitMipLevel
            // and the global samplers (sampler_LinearClamp).
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            // Kawase ring offset, in texels. Set per-blit from KawaseBlur.cs.
            float _offset;

            // xy = 1/width, 1/height of the source target. Set per-blit from
            // KawaseBlur.cs: _BlitTexture is bound through a property block, so
            // the automatic _TexelSize companion is not available here.
            float4 _KawaseTexelSize;

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                float2 uv = input.texcoord;
                float2 res = _KawaseTexelSize.xy;
                float i = _offset;

                half3 col = SAMPLE_TEXTURE2D_X_LOD(_BlitTexture, sampler_LinearClamp, uv, _BlitMipLevel).rgb;
                col += SAMPLE_TEXTURE2D_X_LOD(_BlitTexture, sampler_LinearClamp, uv + float2( i,  i) * res, _BlitMipLevel).rgb;
                col += SAMPLE_TEXTURE2D_X_LOD(_BlitTexture, sampler_LinearClamp, uv + float2( i, -i) * res, _BlitMipLevel).rgb;
                col += SAMPLE_TEXTURE2D_X_LOD(_BlitTexture, sampler_LinearClamp, uv + float2(-i,  i) * res, _BlitMipLevel).rgb;
                col += SAMPLE_TEXTURE2D_X_LOD(_BlitTexture, sampler_LinearClamp, uv + float2(-i, -i) * res, _BlitMipLevel).rgb;
                col /= 5.0h;

                return half4(col, 1.0h);
            }
            ENDHLSL
        }
    }

    Fallback Off
}
