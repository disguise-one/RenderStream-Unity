using System;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Disguise.RenderStream
{
    public class DepthCopy
    {
        // TODO only Linear01 right now, use shader keywords for the other modes
        public enum Mode
        {
            Raw,
            Eye,
            Linear01
        }
        
        public struct FrameData
        {
            public RenderTexture m_depthOutput;

            public bool IsValid => m_depthOutput != null;
        }
        
        struct ShaderResources : IDisposable
        {
            public Shader m_shader;
            public Material m_material;
            public int m_pass;

            public void Create(string shaderPath, string shaderPass)
            {
                m_shader = Shader.Find(shaderPath);
                m_material = CoreUtils.CreateEngineMaterial(m_shader);
                m_pass = m_material.FindPass(shaderPass);
            }

            public void Dispose()
            {
                CoreUtils.Destroy(m_material);
            }
        }
        
        const string k_profilerTag = "Disguise Depth Copy";
        const string k_ShaderPass = "Depth Copy";

        static ShaderResources s_shaderResources;

        public DepthCopy()
        {
            if (s_shaderResources.m_shader == null)
            {
#if HDRP_13_1_8_OR_NEWER
                s_shaderResources.Create("Hidden/Disguise/RenderStream/DepthCopyHDRP", k_ShaderPass);
#elif URP_13_1_8_OR_NEWER
                s_shaderResources.Create("Hidden/Disguise/RenderStream/DepthCopyURP", k_ShaderPass);
#endif
                
                Assert.IsNotNull(s_shaderResources.m_shader, "Couldn't load the shader for DepthCopy");
                Assert.IsNotNull(s_shaderResources.m_material, "Couldn't create the material for DepthCopy");
            }
        }

        public void Execute(ScriptableRenderContext context, FrameData data)
        {
            if (!data.IsValid || s_shaderResources.m_material == null)
                return;

            ValidatePipeline();
            
            CommandBuffer cmd = CommandBufferPool.Get(k_profilerTag);
            cmd.Clear();

            IssueCommands(cmd, data);
            
            context.ExecuteCommandBuffer(cmd);
            
            cmd.Clear();
            CommandBufferPool.Release(cmd);
            
            context.Submit();
        }
        
        void IssueCommands(CommandBuffer cmd, FrameData data)
        {
            CoreUtils.DrawFullScreen(cmd, s_shaderResources.m_material, data.m_depthOutput, shaderPassId: s_shaderResources.m_pass);
        }

        void ValidatePipeline()
        {
#if URP_13_1_8_OR_NEWER
            var pipeline = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            Assert.IsNotNull(pipeline);
            if (!pipeline.supportsCameraDepthTexture)
            {
                Debug.LogError($"Can't copy camera depth because the Depth Texture option isn't enabled in the current {nameof(UniversalRenderPipelineAsset)}");
            }
#endif
        }
    }
}
