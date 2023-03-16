using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace Disguise.RenderStream
{
    public class FrameSender
    {
        internal CameraCaptureDescription description => m_description;
        public Rect subRegion => m_frameRegion;
        
        string m_name;
        int m_lastFrameCount;

        UInt64 m_streamHandle;
        CameraCaptureDescription m_description;
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

            m_description = new CameraCaptureDescription()
            {
                m_colorSpace = CameraCaptureDescription.ColorSpace.sRGB,
                m_autoFlipY = true,
                m_width = (int)stream.width,
                m_height = (int)stream.height,
                m_colorFormat = PluginEntry.ToGraphicsFormat(m_pixelFormat, true),
                m_msaaSamples = 1,
                m_depthBufferBits = 24,
                m_copyDepth = false
            };

            m_frameRegion = new Rect(stream.clipping.left, stream.clipping.top, stream.clipping.right - stream.clipping.left, stream.clipping.bottom - stream.clipping.top);

            // Create texture ahead of time
            GetSharedTexture();
            
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

            var sharedTexture = GetSharedTexture();

            var cmd = CommandBufferPool.Get("Disguise FrameSender");
            
            // Copy to shared texture
            cmd.CopyTexture(texture, sharedTexture);

            SendFrame(cmd, sharedTexture, cameraResponseData);
                
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
                    NativeRenderingPlugin.GetEventID(NativeRenderingPlugin.EventID.SendFrame),
                    dataPtr);
            }
        }

        // We may be temped to use RenderTexture instead of Texture2D for the shared textures.
        // RenderTextures are always stored as typeless texture resources though, which aren't supported
        // by CUDA interop (used by Disguise under the hood):
        // https://docs.nvidia.com/cuda/cuda-runtime-api/group__CUDART__D3D11.html#group__CUDART__D3D11_1g85d07753780643584b8febab0370623b
        // Texture2D apply their GraphicsFormat to their texture resources.
        Texture2D GetSharedTexture()
        {
            return ScratchTexture2DManager.Instance.Get(new Texture2DDescriptor
            {
                Width = m_description.m_width,
                Height = m_description.m_height,
                Format = m_pixelFormat,
                Linear = m_description.m_colorSpace != CameraCaptureDescription.ColorSpace.sRGB
            });
        }
    }
}
