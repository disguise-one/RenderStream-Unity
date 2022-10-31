using System;
using System.Net;
using System.Net.Sockets;
using System.Collections;
using System.Runtime.InteropServices;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Serialization;
using System.Runtime.Remoting;

#if ENABLE_CLUSTER_DISPLAY
using Unity.ClusterDisplay;
#endif

namespace Disguise.RenderStream
{
    using StreamHandle = UInt64;
    
    public class FrameSender
    {
        struct Frame
        {
            public FrameRegion region;
            public RSPixelFormat fmt;
            public AsyncGPUReadbackRequest readback;
            public CameraResponseData responseData;
        }

        private FrameSender() { }
        public FrameSender(string name, Camera cam)
        {
            m_name = name;
            Cam = cam;

            Debug.Log(string.Format("Creating stream {0}", m_name));
            StreamDescription stream = Array.Find(DisguiseRenderStream.streams, s => s.name == name);
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
            m_convertedTex = new Texture2D(m_sourceTex.width, m_sourceTex.height, PluginEntry.ToTextureFormat(stream.format), false, false);

            Debug.Log(string.Format("Created stream {0} with handle {1}", m_name, m_streamHandle));
        }

        public bool GetCameraData(ref CameraData cameraData)
        {
            return PluginEntry.instance.getFrameCamera(m_streamHandle, ref cameraData) == RS_ERROR.RS_ERROR_SUCCESS;
        }

        public void SendFrame(Texture2D frame)
        {
            unsafe
            {
                SenderFrameTypeData data = new SenderFrameTypeData();
                data.dx11_resource = frame.GetNativeTexturePtr();
                RS_ERROR error = PluginEntry.instance.sendFrame(m_streamHandle, SenderFrameType.RS_FRAMETYPE_DX11_TEXTURE, data, m_responseData);
                if (error != RS_ERROR.RS_ERROR_SUCCESS)
                    Debug.LogError(string.Format("Error sending frame: {0}", error));
            }
        }

        public void SendFrame(FrameData frameData, CameraData cameraData)
        {
            if (m_lastFrameCount == Time.frameCount)
                return;

            m_lastFrameCount = Time.frameCount;

            if (m_convertedTex.width != m_sourceTex.width || m_convertedTex.height != m_sourceTex.height)
                m_convertedTex.Reinitialize(m_sourceTex.width, m_sourceTex.height, m_convertedTex.format, false);

            m_cameraResponseData = new CameraResponseData { tTracked = frameData.tTracked, camera = cameraData };

            if (cameraHandleReference.IsAllocated)
                cameraHandleReference.Free();
            cameraHandleReference = GCHandle.Alloc(m_cameraResponseData, GCHandleType.Pinned);

            m_responseData = new FrameResponseData{ cameraData = cameraHandleReference.AddrOfPinnedObject() };

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

            try
            {
                SendFrame(m_convertedTex);
            }
            finally
            {
                if (cameraHandleReference.IsAllocated)
                    cameraHandleReference.Free();
            }
            
// #endif
        }

        public void DestroyStream()
        {
            m_streamHandle = 0;
        }

        public Camera Cam { get; set; }

        private RenderTexture m_sourceTex;
        private FrameResponseData m_responseData;
        private CameraResponseData m_cameraResponseData;
        private GCHandle cameraHandleReference;

        string m_name;
        Texture2D m_convertedTex;
        int m_lastFrameCount;

        StreamHandle m_streamHandle;
        int m_width;
        int m_height;
        Rect m_frameRegion;
        public Rect subRegion
        {
            get
            {
                return m_frameRegion;
            }
        }       
// Blocks HDRP streams in r18.2 
// #if UNITY_PIPELINE_HDRP
//         private DisguiseCameraCaptureAfterPostProcess m_captureAfterPostProcess;
// #endif
    }

    [AddComponentMenu("")]
    [RequireComponent(typeof(Camera))]
    public class DisguiseCameraCapture : MonoBehaviour
    {
#if ENABLE_CLUSTER_DISPLAY
        void OnEnable()
        {
            DisguiseRenderStream.RegisterClusterDisplayHooks();
        }
    
#endif
    
        // Start is called before the first frame update
        public IEnumerator Start()
        {
            if (PluginEntry.instance.IsAvailable == false)
            {
                Debug.LogError("DisguiseCameraCapture: RenderStream DLL not available, capture cannot start.");
                enabled = false;
                yield break;
            }

            m_cameraData = new CameraData();

            m_camera = GetComponent<Camera>();
            m_frameSender = new Disguise.RenderStream.FrameSender(gameObject.name, m_camera);
            RenderPipelineManager.endFrameRendering += RenderPipelineManager_endFrameRendering;

#if ENABLE_CLUSTER_DISPLAY
            if (!ClusterDisplayState.GetIsClusterLogicEnabled())
            {
                // TODO: Warn that cluster display is not enabled
                if (Application.isPlaying == false)
                    yield break;
                if (!DisguiseRenderStream.awaiting)
                    yield return StartCoroutine(DisguiseRenderStream.AwaitFrame());
            }
#else
            if (Application.isPlaying == false)
                yield break;
            if (!DisguiseRenderStream.awaiting)
                yield return StartCoroutine(DisguiseRenderStream.AwaitFrame());
#endif
        }

        // Update is called once per frame
        public void Update()
        {
            // set tracking
            m_newFrameData = DisguiseRenderStream.newFrameData && m_frameSender != null && m_frameSender.GetCameraData(ref m_cameraData);
            float cameraAspect = m_camera.aspect;
            Vector2 lensShift = new Vector2(0.0f, 0.0f);
            if (m_newFrameData)
            {
                cameraAspect = m_cameraData.sensorX / m_cameraData.sensorY;
                if (m_cameraData.cameraHandle != 0)  // If no camera, only set aspect
                {
                    transform.localPosition = new Vector3(m_cameraData.x, m_cameraData.y, m_cameraData.z);
                    transform.localRotation = Quaternion.Euler(new Vector3(-m_cameraData.rx, m_cameraData.ry, -m_cameraData.rz));
                    m_camera.nearClipPlane = m_cameraData.nearZ;
                    m_camera.farClipPlane = m_cameraData.farZ;

                    if (m_cameraData.orthoWidth > 0.0f)  // Use an orthographic camera
                    {  
                        m_camera.orthographic = true;
                        m_camera.orthographicSize = 0.5f * m_cameraData.orthoWidth / cameraAspect;
                        transform.localPosition = new Vector3(m_cameraData.x, m_cameraData.y, m_cameraData.z);
                        transform.localRotation = Quaternion.Euler(new Vector3(-m_cameraData.rx, m_cameraData.ry, -m_cameraData.rz));
                    }
                    else  // Perspective projection, use camera lens properties
                    {
                        m_camera.usePhysicalProperties = true;
                        m_camera.sensorSize = new Vector2(m_cameraData.sensorX, m_cameraData.sensorY);
                        m_camera.focalLength = m_cameraData.focalLength;
                        lensShift = new Vector2(-m_cameraData.cx, m_cameraData.cy);
                    }
                }
            }
            else if (m_frameSender != null)
            {
                // By default aspect is resolution aspect. We need to undo the effect of the subregion on this to get the whole image aspect.
                cameraAspect = m_camera.aspect * (m_frameSender.subRegion.height / m_frameSender.subRegion.width);
            }

            // Clip to correct subregion and calculate projection matrix
            if (m_frameSender != null)
            {
                Rect subRegion = m_frameSender.subRegion;
            
                float imageHeight, imageWidth;
                if (m_camera.orthographic)
                {
                    imageHeight = 2.0f * m_camera.orthographicSize;
                    imageWidth = cameraAspect * imageHeight;
                }
                else
                {
                    float fovV = m_camera.fieldOfView * Mathf.Deg2Rad;
                    float fovH = Camera.VerticalToHorizontalFieldOfView(m_camera.fieldOfView, cameraAspect) * Mathf.Deg2Rad;
                    imageWidth = 2.0f * (float)Math.Tan(0.5f * fovH);
                    imageHeight = 2.0f * (float)Math.Tan(0.5f * fovV);
                }

                float l = (-0.5f + subRegion.xMin) * imageWidth;
                float r = (-0.5f + subRegion.xMax) * imageWidth;
                float t = (-0.5f + 1.0f - subRegion.yMin) * imageHeight;
                float b = (-0.5f + 1.0f - subRegion.yMax) * imageHeight;

                Matrix4x4 projectionMatrix;
                if (m_camera.orthographic)
                    projectionMatrix = Matrix4x4.Ortho(l, r, b, t, m_camera.nearClipPlane, m_camera.farClipPlane);
                else
                    projectionMatrix = PerspectiveOffCenter(l * m_camera.nearClipPlane, r * m_camera.nearClipPlane, b * m_camera.nearClipPlane, t * m_camera.nearClipPlane, m_camera.nearClipPlane, m_camera.farClipPlane);

                Matrix4x4 clippingTransform = Matrix4x4.Translate(new Vector3(-lensShift.x / subRegion.width, lensShift.y / subRegion.height, 0.0f));
                m_camera.projectionMatrix = clippingTransform * projectionMatrix;
            }
        }

        // From http://docs.unity3d.com/ScriptReference/Camera-projectionMatrix.html
        static Matrix4x4 PerspectiveOffCenter(float left, float right, float bottom, float top, float near, float far)
        {
            float x = 2.0F * near / (right - left);
            float y = 2.0F * near / (top - bottom);
            float a = (right + left) / (right - left);
            float b = (top + bottom) / (top - bottom);
            float c = -(far + near) / (far - near);
            float d = -(2.0F * far * near) / (far - near);
            float e = -1.0F;
            Matrix4x4 m = new Matrix4x4();
            m[0, 0] = x;
            m[0, 1] = 0;
            m[0, 2] = a;
            m[0, 3] = 0;
            m[1, 0] = 0;
            m[1, 1] = y;
            m[1, 2] = b;
            m[1, 3] = 0;
            m[2, 0] = 0;
            m[2, 1] = 0;
            m[2, 2] = c;
            m[2, 3] = d;
            m[3, 0] = 0;
            m[3, 1] = 0;
            m[3, 2] = e;
            m[3, 3] = 0;
            return m;
        }

        public void OnRenderImage(RenderTexture source, RenderTexture destination)
        {
            CheckAndSendFrame();
        }

        private void CheckAndSendFrame()
        {
            if (m_newFrameData)
            {
                if (m_frameSender != null)
                {
                    m_frameSender.SendFrame(DisguiseRenderStream.frameData, m_cameraData);
                }
                m_newFrameData = false;
            }
        }

        private void RenderPipelineManager_endFrameRendering(ScriptableRenderContext context, Camera[] cameras)
        {
            foreach (var cam in cameras)
            {
                if (cam == m_camera)
                    CheckAndSendFrame();
            }
        }

        public void OnDestroy()
        {
        }

        public void OnDisable()
        {
            if (m_frameSender != null)
            {
                m_frameSender.DestroyStream();
            }
            RenderPipelineManager.endFrameRendering -= RenderPipelineManager_endFrameRendering;
        
#if ENABLE_CLUSTER_DISPLAY
            DisguiseRenderStream.UnregisterClusterDisplayEvents();
#endif
        }

        Camera m_camera;
        public Disguise.RenderStream.FrameSender m_frameSender;

        CameraData m_cameraData;
        bool m_newFrameData = false;
    }
}
