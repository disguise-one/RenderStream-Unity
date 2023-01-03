using System;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.Rendering;

namespace Disguise.RenderStream
{
    class DepthCopy
    {
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
        
        // TODO: use ShaderVariantCollection + preload in GraphicsSettings
        struct ShaderVariantResources : IDisposable
        {
            public ShaderResources m_Raw;
            public ShaderResources m_Eye;
            public ShaderResources m_linear01;

            public bool IsLoaded => m_Raw.IsLoaded && m_Eye.IsLoaded && m_linear01.IsLoaded;

            public void Create(string shaderPath, string shaderPass)
            {
                m_Raw.Create(shaderPath, shaderPass);
                m_Eye.Create(shaderPath, shaderPass);
                m_linear01.Create(shaderPath, shaderPass);
                
                m_Raw.SetKeyword("DEPTH_COPY_RAW", true);
                m_Eye.SetKeyword("DEPTH_COPY_EYE", true);
                m_linear01.SetKeyword("DEPTH_COPY_LINEAR01", true);
            }

            public void Dispose()
            {
                m_Raw.Dispose();
                m_Eye.Dispose();
                m_linear01.Dispose();
            }
        }
        
        struct ShaderResources : IDisposable
        {
            public Shader m_shader;
            public Material m_material;
            public int m_pass;

            public bool IsLoaded => m_shader != null && m_material != null && m_pass >= 0;

            public void SetKeyword(string keyword, bool value)
            {
                if (IsLoaded)
                {
                    if (value)
                        m_material.EnableKeyword(keyword);
                    else
                        m_material.DisableKeyword(keyword);
                }
            }

            public void Create(string shaderPath, string shaderPass)
            {
                m_shader = Shader.Find(shaderPath);
                if (m_shader != null)
                {
                    m_material = CoreUtils.CreateEngineMaterial(m_shader);
                    m_pass = m_material.FindPass(shaderPass);
                }
            }

            public void Dispose()
            {
                CoreUtils.Destroy(m_material);
            }
        }

        public Mode mode
        {
            get => m_mode;
            set
            {
                m_mode = value;
                
                switch (m_mode)
                {
                    case Mode.Raw:
                        m_shaderResources = s_shaderVariantResources.m_Raw;
                        break;
                    case Mode.Eye:
                        m_shaderResources = s_shaderVariantResources.m_Eye;
                        break;
                    case Mode.Linear01:
                        m_shaderResources = s_shaderVariantResources.m_linear01;
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }
        }
        
        const string k_profilerTag = "Disguise Depth Copy";
        const string k_ShaderPass = "Depth Copy";

        static ShaderVariantResources s_shaderVariantResources;

        Mode m_mode = Mode.Linear01;
        ShaderResources m_shaderResources;

        public DepthCopy()
        {
            if (!s_shaderVariantResources.IsLoaded)
            {
#if HDRP_13_1_8_OR_NEWER
                s_shaderVariantResources.Create("Hidden/Disguise/RenderStream/DepthCopyHDRP", k_ShaderPass);
#elif URP_13_1_8_OR_NEWER
                s_shaderVariantResources.Create("Hidden/Disguise/RenderStream/DepthCopyURP", k_ShaderPass);
#endif
                
                Assert.IsTrue(s_shaderVariantResources.IsLoaded, "Couldn't load the shader resources for DepthCopy");
            }

            mode = Mode.Linear01;
        }

        public void Execute(ScriptableRenderContext context, FrameData data)
        {
            if (!data.IsValid || !m_shaderResources.IsLoaded)
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
            CoreUtils.DrawFullScreen(cmd, m_shaderResources.m_material, data.m_depthOutput, shaderPassId: m_shaderResources.m_pass);
        }

        void ValidatePipeline()
        {
#if URP_13_1_8_OR_NEWER
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
