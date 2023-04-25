using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.Rendering;

namespace Disguise.RenderStream
{
    /// <summary>
    /// Copies a camera's depth into a texture.
    /// </summary>
    class DepthCopy
    {
#if UNITY_PIPELINE_HDRP && HDRP_VERSION_SUPPORTED
        public const string ShaderName = "Hidden/Disguise/RenderStream/DepthCopyHDRP";
#elif UNITY_PIPELINE_URP && URP_VERSION_SUPPORTED
        public const string ShaderName = "Hidden/Disguise/RenderStream/DepthCopyURP";
#else
        public const string ShaderName = null;
#endif
        
        /// <summary>
        /// Represents the encoding of depth inside the captured depth texture.
        /// The modes are equivalent to the depth sampling modes of the Shader Graph's Scene Depth Node:
        /// https://docs.unity3d.com/Packages/com.unity.shadergraph@15.0/manual/Scene-Depth-Node.html.
        /// </summary>
        public enum Mode
        {
            Raw,
            Eye,
            Linear01
        }

        static int GetPass(Mode mode)
        {
            return (int)mode;
        }
        
        const string k_profilerTag = "Disguise Depth Copy";

        public static DepthCopy instance { get; }

        static DepthCopy()
        {
            instance = new DepthCopy();
        }
        
        readonly Material m_material;

        DepthCopy()
        {
#if !(UNITY_PIPELINE_HDRP && HDRP_VERSION_SUPPORTED) && !(UNITY_PIPELINE_URP && URP_VERSION_SUPPORTED)
            Debug.LogError($"No supported render pipeline was found for {nameof(DepthCopy)}.");
#endif
            
            var shader = Shader.Find(ShaderName);
            if (shader != null)
            {
                m_material = CoreUtils.CreateEngineMaterial(shader);
            }
            
            Assert.IsTrue(shader != null && m_material != null, $"Couldn't load the shader resources for {nameof(DepthCopy)}");
        }

        /// <summary>
        /// Performs the copy using the SRP's currently active camera and the provided <see cref="FrameData"/>.
        /// </summary>
        /// <param name="context"></param>
        /// <param name="depthOutput">The texture to which the depth will be written to.</param>
        /// <param name="mode">The texture to which the depth will be written to.</param>
        public void Execute(ScriptableRenderContext context, RenderTexture depthOutput, Mode mode)
        {
            Assert.IsNotNull(depthOutput);

            ValidatePipeline();
            
            var cmd = CommandBufferPool.Get(k_profilerTag);
            IssueCommands(cmd, depthOutput, mode);
            
            context.ExecuteCommandBuffer(cmd);
            context.Submit();
            
            CommandBufferPool.Release(cmd);
        }
        
        void IssueCommands(CommandBuffer cmd, RenderTexture depthOutput, Mode mode)
        {
            Assert.IsNotNull(m_material);
            
            CoreUtils.DrawFullScreen(cmd, m_material, depthOutput, shaderPassId: GetPass(mode));
        }

        void ValidatePipeline()
        {
            // HDRP cameras always have a depth texture
            
#if UNITY_PIPELINE_URP && URP_VERSION_SUPPORTED
            var pipeline = GraphicsSettings.currentRenderPipeline as UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset;
            Assert.IsNotNull(pipeline);
            if (!pipeline.supportsCameraDepthTexture)
            {
                Debug.LogError($"Can't copy camera depth because the Depth Texture option isn't enabled in the current {nameof(UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset)}");
            }
#endif
        }
    }
}
