using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace VoxelEngine
{
    /// <summary>
    /// Responsible for calling the VoxelRenderer.Render after URP rendering.
    /// </summary>
    public class VoxelRendererFeature : ScriptableRendererFeature
    {
        class VoxelRendererRenderPass : ScriptableRenderPass
        {
            private VoxelRenderer m_VoxelRenderer;

            public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
            {
                if(m_VoxelRenderer == null)
                {
                    m_VoxelRenderer = FindObjectOfType<VoxelRenderer>();
                }
            }

            public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
            {
                if (!Application.isPlaying)
                {
                    return;
                }

                CommandBuffer cmd = CommandBufferPool.Get("Voxel Renderer Pass");

                m_VoxelRenderer.Render(cmd);

                context.ExecuteCommandBuffer(cmd);
                CommandBufferPool.Release(cmd);
            }

            public override void OnCameraCleanup(CommandBuffer cmd)
            {
            }
        }

        VoxelRendererRenderPass m_VoxelRendererRenderPass;

        public override void Create()
        {
            m_VoxelRendererRenderPass = new VoxelRendererRenderPass();

            // Configures where the render pass should be injected.
            m_VoxelRendererRenderPass.renderPassEvent = RenderPassEvent.AfterRendering;
        }

        // Here you can inject one or multiple render passes in the renderer.
        // This method is called when setting up the renderer once per-camera.
        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            renderer.EnqueuePass(m_VoxelRendererRenderPass);
        }
    }
}
