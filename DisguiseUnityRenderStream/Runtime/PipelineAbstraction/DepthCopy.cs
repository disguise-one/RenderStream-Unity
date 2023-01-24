using System;
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
#if HDRP
        const string k_ShaderName = "Hidden/Disguise/RenderStream/DepthCopyHDRP";
#elif URP
        const string k_ShaderName = "Hidden/Disguise/RenderStream/DepthCopyURP";
#else
        const string k_ShaderName = null;
#endif
        
        /// <summary>
        /// Defines how to transform the depth values into the 0-1 range.
        /// The modes are equivalent to the depth sampling modes of the Shader Graph's Scene Depth Node:
        /// https://docs.unity3d.com/Packages/com.unity.shadergraph@15.0/manual/Scene-Depth-Node.html.
        /// </summary>
        public enum Mode
        {
            Raw,
            Eye,
            Linear01
        }
        
        // TODO: use ShaderVariantCollection + preload in GraphicsSettings
        // Once that's done DepthCopy can be made Serializable
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
                
                m_shaderResources = m_mode switch {
                    Mode.Raw => s_shaderVariantResources.m_Raw,
                    Mode.Eye => s_shaderVariantResources.m_Eye,
                    Mode.Linear01 => s_shaderVariantResources.m_linear01,
                    _ => throw new ArgumentOutOfRangeException()
                };
            }
        }
        
        const string k_profilerTag = "Disguise Depth Copy";
        const string k_ShaderPass = "Depth Copy";

        static readonly ShaderVariantResources s_shaderVariantResources;

        Mode m_mode = Mode.Linear01;
        ShaderResources m_shaderResources;
        
        static DepthCopy()
        {
#if DISGUISE_CAPTURE_DEPTH
#if (!HDRP) && !(URP)
            Debug.LogError($"No supported render pipeline was found for {nameof(DepthCopy)}.");
#endif
            
            s_shaderVariantResources.Create(k_ShaderName, k_ShaderPass);
            Assert.IsTrue(s_shaderVariantResources.IsLoaded, $"Couldn't load the shader resources for {nameof(DepthCopy)}");
#endif
        }

        public DepthCopy()
        {
#if DISGUISE_CAPTURE_DEPTH
            mode = Mode.Linear01;
#endif
        }

        /// <summary>
        /// Performs the copy using the SRP's currently active camera and the provided <see cref="FrameData"/>.
        /// </summary>
        /// <param name="depthOutput">The texture to which the depth will be written to.</param>
        public void Execute(ScriptableRenderContext context, RenderTexture depthOutput)
        {
#if DISGUISE_CAPTURE_DEPTH
            Assert.IsNotNull(depthOutput);
            Assert.IsTrue(m_shaderResources.IsLoaded);

            ValidatePipeline();
            
            CommandBuffer cmd = CommandBufferPool.Get(k_profilerTag);
            IssueCommands(cmd, depthOutput);
            
            context.ExecuteCommandBuffer(cmd);
            context.Submit();
            
            CommandBufferPool.Release(cmd);
#endif
        }
        
        void IssueCommands(CommandBuffer cmd, RenderTexture depthOutput)
        {
            CoreUtils.DrawFullScreen(cmd, m_shaderResources.m_material, depthOutput, shaderPassId: m_shaderResources.m_pass);
        }

        void ValidatePipeline()
        {
            // HDRP cameras always have a depth texture
            
#if URP
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
