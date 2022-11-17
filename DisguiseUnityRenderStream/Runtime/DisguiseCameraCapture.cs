using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;

namespace Disguise.RenderStream
{
    [AddComponentMenu("")]
    [RequireComponent(typeof(Camera))]
    public class DisguiseCameraCapture : MonoBehaviour
    {
        // Start is called before the first frame update
        public void Start()
        {
            m_RenderStream = DisguiseRenderStream.Instance;
            if (m_RenderStream == null || PluginEntry.instance.IsAvailable == false)
            {
                Debug.LogError("DisguiseCameraCapture: RenderStream DLL not available, capture cannot start.");
                enabled = false;
                return;
            }

            m_cameraData = new CameraData();

            m_camera = GetComponent<Camera>();
            StreamDescription stream = Array.Find(m_RenderStream.Streams, s => s.name == gameObject.name);
            m_frameSender = new FrameSender(gameObject.name, m_camera, stream);
            RenderPipelineManager.endFrameRendering += RenderPipelineManager_endFrameRendering;
        }

        // Update is called once per frame
        public void Update()
        {
            // set tracking
            if (m_RenderStream == null) return;
            
            m_newFrameData = m_RenderStream.HasNewFrameData && m_frameSender != null && m_frameSender.GetCameraData(ref m_cameraData);
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
                    m_frameSender.SendFrame(m_RenderStream.LatestFrameData, m_cameraData);
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
        }

        Camera m_camera;
        public Disguise.RenderStream.FrameSender m_frameSender;

        CameraData m_cameraData;
        bool m_newFrameData = false;
        DisguiseRenderStream m_RenderStream;
    }
}
