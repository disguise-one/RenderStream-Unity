using System;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.Rendering;

namespace Disguise.RenderStream
{
    public class FrameSender
    {
        struct Frame
        {
            public FrameRegion region;
            public RSPixelFormat fmt;
            public AsyncGPUReadbackRequest readback;
            public CameraResponseData responseData;
        }

        NativeRenderingPlugin.PixelFormat ToNativeRenderingPluginFormat(RSPixelFormat rsPixelFormat)
        {
            switch (rsPixelFormat)
            {
                case RSPixelFormat.RS_FMT_BGRA8: return NativeRenderingPlugin.PixelFormat.BGRA8;
                case RSPixelFormat.RS_FMT_BGRX8: return NativeRenderingPlugin.PixelFormat.BGRX8;
                case RSPixelFormat.RS_FMT_RGBA32F: return NativeRenderingPlugin.PixelFormat.RGBA32F;
                case RSPixelFormat.RS_FMT_RGBA16: return NativeRenderingPlugin.PixelFormat.RGBA16;
                case RSPixelFormat.RS_FMT_RGBA8: return NativeRenderingPlugin.PixelFormat.RGBA8;
                case RSPixelFormat.RS_FMT_RGBX8: return NativeRenderingPlugin.PixelFormat.RGBA8;
                default: return NativeRenderingPlugin.PixelFormat.BGRA8;
            }
        }

        private FrameSender() { }
        public FrameSender(string name, Camera cam, StreamDescription stream)
        {
            m_name = name;
            Cam = cam;

            Debug.Log(string.Format("Creating stream {0}", m_name));
            Debug.Log(string.Format("  Channel {0} at {1}x{2}@{3}", stream.channel, stream.width, stream.height, stream.format));

            m_lastFrameCount = -1;
            m_streamHandle = stream.handle;
            m_width = (int)stream.width;
            m_height = (int)stream.height;

            m_frameRegion = new Rect(stream.clipping.left, stream.clipping.top, stream.clipping.right - stream.clipping.left, stream.clipping.bottom - stream.clipping.top);

            RenderTextureDescriptor desc = new RenderTextureDescriptor(m_width, m_height, PluginEntry.ToRenderTextureFormat(stream.format), 24);
            m_sourceTex = new RenderTexture(desc)
            {
                name = m_name + " Texture"
            };
            Cam.targetTexture = m_sourceTex;
            // m_convertedTex = new Texture2D(m_sourceTex.width, m_sourceTex.height, PluginEntry.ToTextureFormat(stream.format), false, false);

            var nativeTex = NativeRenderingPlugin.CreateNativeTexture(m_sourceTex.width, m_sourceTex.height, ToNativeRenderingPluginFormat(stream.format));
            m_convertedTex = Texture2D.CreateExternalTexture(m_sourceTex.width, m_sourceTex.height, PluginEntry.ToTextureFormat(stream.format), false, false, nativeTex);

            Debug.Log(string.Format("Created stream {0} with handle {1}", m_name, m_streamHandle));
        }

        public bool GetCameraData(ref CameraData cameraData)
        {
            return PluginEntry.instance.getFrameCamera(m_streamHandle, ref cameraData) == RS_ERROR.RS_ERROR_SUCCESS;
        }

        public void SendFrame(Texture2D frame)
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
                Debug.LogError(string.Format("Error sending frame: {0}", error));
        }

        public void SendFrame(FrameData frameData, CameraData cameraData)
        {
            if (m_lastFrameCount == Time.frameCount)
                return;

            m_lastFrameCount = Time.frameCount;

            if (m_convertedTex.width != m_sourceTex.width || m_convertedTex.height != m_sourceTex.height)
                m_convertedTex.Reinitialize(m_sourceTex.width, m_sourceTex.height, m_convertedTex.format, false);

            var cameraResponseData = new CameraResponseData { tTracked = frameData.tTracked, camera = cameraData };
            unsafe
            {
                var cameraResponseDataPtr = &cameraResponseData;
                m_responseData = new FrameResponseData{ cameraData = (IntPtr)cameraResponseDataPtr };

// Blocks HDRP streams in r18.2
// #if UNITY_PIPELINE_HDRP
//             Volume volume = Cam.GetComponent<Volume>();
//             if (!volume.profile)
//                 Debug.Log("Missing profile");

//             if (!volume.profile.TryGet<DisguiseCameraCaptureAfterPostProcess>(out m_captureAfterPostProcess))
//             {
//                 Debug.Log("Missing captureAfterPostProcess");
//                 m_captureAfterPostProcess = volume.profile.Add<DisguiseCameraCaptureAfterPostProcess>(true);
//             }
//             m_captureAfterPostProcess.width.value = (Int32)m_width;
//             m_captureAfterPostProcess.height.value = (Int32)m_height;
// #else
                RenderTexture unflipped = RenderTexture.GetTemporary(m_sourceTex.width, m_sourceTex.height, 0, m_sourceTex.format);
                Graphics.Blit(m_sourceTex, unflipped, new Vector2(1.0f, -1.0f), new Vector2(0.0f, 1.0f));
                Graphics.ConvertTexture(unflipped, m_convertedTex);
                RenderTexture.ReleaseTemporary(unflipped);

                SendFrame(m_convertedTex);
// #endif
            }
        }

        public void DestroyStream()
        {
            m_streamHandle = 0;
        }

        public Camera Cam { get; set; }

        private RenderTexture m_sourceTex;
        private FrameResponseData m_responseData;
        private GCHandle cameraHandleReference;

        string m_name;
        Texture2D m_convertedTex;
        int m_lastFrameCount;

        UInt64 m_streamHandle;
        int m_width;
        int m_height;
        Rect m_frameRegion;
        public Rect subRegion => m_frameRegion;

        // Blocks HDRP streams in r18.2 
// #if UNITY_PIPELINE_HDRP
//         private DisguiseCameraCaptureAfterPostProcess m_captureAfterPostProcess;
// #endif
    }
}
