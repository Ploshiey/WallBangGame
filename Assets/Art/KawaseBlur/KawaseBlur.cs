using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Kawase dual-ring blur as a URP ScriptableRendererFeature.
/// Ported to the URP 14 (Unity 2022.3) RTHandle + Blitter API.
/// </summary>
public class KawaseBlur : ScriptableRendererFeature
{
    [System.Serializable]
    public class KawaseBlurSettings
    {
        public RenderPassEvent renderPassEvent = RenderPassEvent.AfterRenderingTransparents;
        public Material blurMaterial = null;

        [Range(2, 15)]
        public int blurPasses = 1;

        [Range(1, 4)]
        public int downsample = 1;

        [Tooltip("Write the blurred result back over the camera colour. Leave off to only " +
                 "publish it as the global texture named below.")]
        public bool copyToFramebuffer;

        public string targetName = "_blurTexture";
    }

    public KawaseBlurSettings settings = new KawaseBlurSettings();

    class CustomRenderPass : ScriptableRenderPass
    {
        static readonly int OffsetId = Shader.PropertyToID("_offset");
        static readonly int TexelSizeId = Shader.PropertyToID("_KawaseTexelSize");

        public Material blurMaterial;
        public int passes;
        public int downsample;
        public bool copyToFramebuffer;
        public string targetName;

        readonly ProfilingSampler m_ProfilingSampler;

        RTHandle m_Source;
        RTHandle m_TmpRT1;
        RTHandle m_TmpRT2;

        int m_SourceWidth;
        int m_SourceHeight;
        int m_TmpWidth;
        int m_TmpHeight;

        public CustomRenderPass(string profilerTag)
        {
            m_ProfilingSampler = new ProfilingSampler(profilerTag);
        }

        /// <summary>
        /// Must be called from ScriptableRendererFeature.SetupRenderPasses — reading
        /// cameraColorTargetHandle any earlier logs an error in URP 13+.
        /// </summary>
        public void SetSource(RTHandle source)
        {
            m_Source = source;
        }

        public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
        {
            var desc = renderingData.cameraData.cameraTargetDescriptor;
            m_SourceWidth = desc.width;
            m_SourceHeight = desc.height;

            desc.depthBufferBits = 0;
            desc.msaaSamples = 1;
            desc.width = Mathf.Max(1, desc.width / downsample);
            desc.height = Mathf.Max(1, desc.height / downsample);
            m_TmpWidth = desc.width;
            m_TmpHeight = desc.height;

            RenderingUtils.ReAllocateIfNeeded(ref m_TmpRT1, desc, FilterMode.Bilinear,
                TextureWrapMode.Clamp, name: "_KawaseBlurRT1");
            RenderingUtils.ReAllocateIfNeeded(ref m_TmpRT2, desc, FilterMode.Bilinear,
                TextureWrapMode.Clamp, name: "_KawaseBlurRT2");
        }

        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            if (blurMaterial == null || m_Source == null || m_TmpRT1 == null || m_TmpRT2 == null)
                return;

            CommandBuffer cmd = CommandBufferPool.Get();
            using (new ProfilingScope(cmd, m_ProfilingSampler))
            {
                // First pass: camera colour -> tmp1 (downsampled).
                SetTexelSize(cmd, m_SourceWidth, m_SourceHeight);
                cmd.SetGlobalFloat(OffsetId, 1.5f);
                Blitter.BlitCameraTexture(cmd, m_Source, m_TmpRT1, blurMaterial, 0);

                // Intermediate ping-pong passes, widening the ring each time.
                for (var i = 1; i < passes - 1; i++)
                {
                    SetTexelSize(cmd, m_TmpWidth, m_TmpHeight);
                    cmd.SetGlobalFloat(OffsetId, 0.5f + i);
                    Blitter.BlitCameraTexture(cmd, m_TmpRT1, m_TmpRT2, blurMaterial, 0);

                    (m_TmpRT1, m_TmpRT2) = (m_TmpRT2, m_TmpRT1);
                }

                // Final pass.
                SetTexelSize(cmd, m_TmpWidth, m_TmpHeight);
                cmd.SetGlobalFloat(OffsetId, 0.5f + passes - 1f);

                if (copyToFramebuffer)
                {
                    Blitter.BlitCameraTexture(cmd, m_TmpRT1, m_Source, blurMaterial, 0);
                }
                else
                {
                    Blitter.BlitCameraTexture(cmd, m_TmpRT1, m_TmpRT2, blurMaterial, 0);
                    cmd.SetGlobalTexture(targetName, m_TmpRT2);

                    // Leave the camera target bound for whatever runs next.
                    CoreUtils.SetRenderTarget(cmd, m_Source);
                }
            }

            context.ExecuteCommandBuffer(cmd);
            cmd.Clear();
            CommandBufferPool.Release(cmd);
        }

        static void SetTexelSize(CommandBuffer cmd, int width, int height)
        {
            cmd.SetGlobalVector(TexelSizeId,
                new Vector4(1f / Mathf.Max(1, width), 1f / Mathf.Max(1, height), width, height));
        }

        public override void OnCameraCleanup(CommandBuffer cmd)
        {
            m_Source = null;
        }

        public void Dispose()
        {
            m_TmpRT1?.Release();
            m_TmpRT2?.Release();
            m_TmpRT1 = null;
            m_TmpRT2 = null;
        }
    }

    CustomRenderPass m_ScriptablePass;

    public override void Create()
    {
        m_ScriptablePass ??= new CustomRenderPass("KawaseBlur");

        m_ScriptablePass.blurMaterial = settings.blurMaterial;
        m_ScriptablePass.passes = settings.blurPasses;
        m_ScriptablePass.downsample = settings.downsample;
        m_ScriptablePass.copyToFramebuffer = settings.copyToFramebuffer;
        m_ScriptablePass.targetName = settings.targetName;
        m_ScriptablePass.renderPassEvent = settings.renderPassEvent;
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        if (settings.blurMaterial == null)
        {
            Debug.LogWarning("KawaseBlur renderer feature has no blur material assigned; skipping.");
            return;
        }

        // Material previews and reflection probes have no use for this.
        if (renderingData.cameraData.cameraType == CameraType.Preview)
            return;

        renderer.EnqueuePass(m_ScriptablePass);
    }

    public override void SetupRenderPasses(ScriptableRenderer renderer, in RenderingData renderingData)
    {
        if (settings.blurMaterial == null)
            return;

        if (renderingData.cameraData.cameraType == CameraType.Preview)
            return;

        m_ScriptablePass.SetSource(renderer.cameraColorTargetHandle);
    }

    protected override void Dispose(bool disposing)
    {
        m_ScriptablePass?.Dispose();
    }
}
