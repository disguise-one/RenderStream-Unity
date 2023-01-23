using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace Disguise.RenderStream
{
    public class FrameSender
    {
        internal CameraCapture.CameraCaptureDescription description => m_description;
        public Rect subRegion => m_frameRegion;
        
        string m_name;
        Texture2D m_convertedTex;
        int m_lastFrameCount;
        FrameResponseData m_responseData;

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

            m_convertedTex = DisguiseTextures.CreateTexture(m_description.m_width, m_description.m_height, m_pixelFormat, stream.name + " Converted Texture");
            if (m_convertedTex == null)
            {
                Debug.LogError("Failed to create texture for Disguise");
            }
            
            Debug.Log($"Created stream {m_name} with handle {m_streamHandle}");
        }

        public bool GetCameraData(ref CameraData cameraData)
        {
            return PluginEntry.instance.getFrameCamera(m_streamHandle, ref cameraData) == RS_ERROR.RS_ERROR_SUCCESS;
        }

        public void SendFrame(FrameData frameData, CameraData cameraData, RenderTexture texture)
        {
            if (m_lastFrameCount == Time.frameCount)
                return;

            m_lastFrameCount = Time.frameCount;
            
            var cameraResponseData = new CameraResponseData { tTracked = frameData.tTracked, camera = cameraData };
            unsafe
            {
                var cameraResponseDataPtr = &cameraResponseData;
                m_responseData = new FrameResponseData { cameraData = (IntPtr)cameraResponseDataPtr };
            }

            if (m_convertedTex.width != texture.width || m_convertedTex.height != texture.height)
            {
                m_convertedTex = DisguiseTextures.ResizeTexture(m_convertedTex, m_description.m_width, m_description.m_height, m_pixelFormat);
                if (m_convertedTex == null)
                {
                    Debug.LogError("Failed to resize texture for Disguise");
                }
            }

            RenderTexture unflipped = RenderTexture.GetTemporary(texture.width, texture.height, 0, texture.format);
            Graphics.Blit(texture, unflipped, new Vector2(1.0f, -1.0f), new Vector2(0.0f, 1.0f));
            Graphics.ConvertTexture(unflipped, m_convertedTex);
            RenderTexture.ReleaseTemporary(unflipped);
            
            SendFrame(m_convertedTex);
        }
        
        void SendFrame(Texture2D frame)
        {
            SenderFrameTypeData data = new SenderFrameTypeData();
            RS_ERROR error = RS_ERROR.RS_ERROR_SUCCESS;

            switch (PluginEntry.instance.GraphicsDeviceType)
            {
                case GraphicsDeviceType.Direct3D11:
                    data.dx11_resource = frame.GetNativeTexturePtr();
                    error = PluginEntry.instance.sendFrame(m_streamHandle, SenderFrameType.RS_FRAMETYPE_DX11_TEXTURE, data, ref m_responseData);
                    break;
                
                case GraphicsDeviceType.Direct3D12:
                    data.dx12_resource = frame.GetNativeTexturePtr();
                    error = PluginEntry.instance.sendFrame(m_streamHandle, SenderFrameType.RS_FRAMETYPE_DX12_TEXTURE, data, ref m_responseData);
                    break;
            }
            
            if (error != RS_ERROR.RS_ERROR_SUCCESS)
                Debug.LogError($"Error sending frame: {error}");
        }
    }
}
