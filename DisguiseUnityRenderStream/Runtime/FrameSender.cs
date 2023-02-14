using System;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;

namespace Disguise.RenderStream
{
    public class FrameSender
    {
        internal CameraCapture.CameraCaptureDescription description => m_description;
        public Rect subRegion => m_frameRegion;
        
        string m_name;
        Texture2D m_convertedTex;
        RenderTexture m_scratchTex;
        int m_temporaryTexId = Shader.PropertyToID("DisguiseOutputTemporaryRT");
        int m_lastFrameCount;

        UInt64 m_streamHandle;
        CameraCapture.CameraCaptureDescription m_description;
        RSPixelFormat m_pixelFormat;
        Rect m_frameRegion;
        
        public FrameSender(string name, StreamDescription stream)
        {
            m_name = name;

            Debug.Log($"Creating stream {m_name}");
            Debug.Log($"  Channel {stream.channel} at {stream.width}x{stream.height}@{stream.format}");

            m_lastFrameCount = -1;
            m_streamHandle = stream.handle;
            m_pixelFormat = stream.format;

            m_description = new CameraCapture.CameraCaptureDescription()
            {
                m_width = (int)stream.width,
                m_height = (int)stream.height,
                m_colorFormat = PluginEntry.ToRenderTextureFormat(m_pixelFormat),
                m_msaaSamples = 1,
                m_depthBufferBits = 24,
                m_copyDepth = false
            };

            m_frameRegion = new Rect(stream.clipping.left, stream.clipping.top, stream.clipping.right - stream.clipping.left, stream.clipping.bottom - stream.clipping.top);

            m_convertedTex = DisguiseTextures.CreateTexture(m_description.m_width, m_description.m_height, m_pixelFormat, false, stream.name + " Converted Texture");
            if (m_convertedTex == null)
            {
                Debug.LogError("Failed to create texture for Disguise");
            }

            m_scratchTex = new RenderTexture(m_convertedTex.width, m_convertedTex.height, m_convertedTex.graphicsFormat, GraphicsFormat.None, 1);
            
            Debug.Log($"Created stream {m_name} with handle {m_streamHandle}");
        }

        public bool GetCameraData(ref CameraData cameraData)
        {
            return PluginEntry.instance.getFrameCamera(m_streamHandle, ref cameraData) == RS_ERROR.RS_ERROR_SUCCESS;
        }

        public void SendFrame(ScriptableRenderContext context, FrameData frameData, CameraData cameraData, RenderTexture texture)
        {
            if (m_lastFrameCount == Time.frameCount)
                return;

            m_lastFrameCount = Time.frameCount;
            
            var cameraResponseData = new CameraResponseData { tTracked = frameData.tTracked, camera = cameraData };

            if (m_convertedTex.width != texture.width || m_convertedTex.height != texture.height)
            {
                m_convertedTex = DisguiseTextures.ResizeTexture(m_convertedTex, m_description.m_width, m_description.m_height, m_pixelFormat);
                if (m_convertedTex == null)
                {
                    Debug.LogError("Failed to resize texture for Disguise");
                }
                
                m_scratchTex = new RenderTexture(m_convertedTex.width, m_convertedTex.height, m_convertedTex.graphicsFormat, GraphicsFormat.None, 1);
            }

            // RenderTexture unflipped = RenderTexture.GetTemporary(texture.width, texture.height, 0, texture.format);
            // Graphics.Blit(texture, unflipped, new Vector2(1.0f, -1.0f), new Vector2(0.0f, 1.0f));
            // Graphics.ConvertTexture(unflipped, m_convertedTex);
            // RenderTexture.ReleaseTemporary(unflipped);
            //
            // SendFrame(m_convertedTex);
            
            var cmd = CommandBufferPool.Get("Disguise FrameSender");

            // cmd.GetTemporaryRT(m_temporaryTexId, m_convertedTex.width, m_convertedTex.height, 0, FilterMode.Point, m_convertedTex.graphicsFormat, 0);
            cmd.Blit(texture, m_scratchTex, new Vector2(1.0f, -1.0f), new Vector2(0.0f, 1.0f));
            cmd.CopyTexture(m_scratchTex, m_convertedTex);

            SendFrame(cmd, m_convertedTex, cameraResponseData);
            // cmd.ReleaseTemporaryRT(m_temporaryTexId);
                
            context.ExecuteCommandBuffer(cmd);
            context.Submit();
                
            CommandBufferPool.Release(cmd);
        }
        
        void SendFrame(CommandBuffer cmd, Texture2D frame, CameraResponseData cameraResponseData)
        {
            switch (PluginEntry.instance.GraphicsDeviceType)
            {
                case GraphicsDeviceType.Direct3D11:
                case GraphicsDeviceType.Direct3D12:
                    break;
                
                default:
                    Debug.LogError($"Unsupported graphics device type {PluginEntry.instance.GraphicsDeviceType}");
                    return;
            }
            
            NativeRenderingPlugin.SendFrameData sendFrameData = new NativeRenderingPlugin.SendFrameData()
            {
                m_rs_sendFrame = PluginEntry.instance.rs_sendFrame_ptr,
                m_StreamHandle = m_streamHandle,
                m_Texture = frame.GetNativeTexturePtr(),
                m_CameraResponseData = cameraResponseData
            };

            if (NativeRenderingPlugin.SendFrameDataPool.TryPreserve(sendFrameData, out var dataPtr))
            {
                cmd.IssuePluginEventAndData(
                    NativeRenderingPlugin.GetRenderEventCallback(),
                    (int)NativeRenderingPlugin.EventID.SendFrame,
                    dataPtr);
            }
        }
    }
}
