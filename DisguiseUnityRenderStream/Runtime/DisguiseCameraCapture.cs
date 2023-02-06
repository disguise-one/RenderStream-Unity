using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace Disguise.RenderStream
{
    [AddComponentMenu("")]
    [RequireComponent(typeof(Camera))]
    public class DisguiseCameraCapture : MonoBehaviour
    {
        Camera m_camera;
        FrameSender m_frameSender;
        CameraCapture m_capture;

        CameraData m_cameraData;
        bool m_newFrameData;
        DisguiseRenderStream m_RenderStream;
        
        void Awake()
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
            var stream = Array.Find(m_RenderStream.Streams, s => s.name == gameObject.name);
            m_frameSender = new FrameSender(gameObject.name, stream);

            m_capture = gameObject.AddComponent<CameraCapture>();
            m_capture.hideFlags |= HideFlags.HideAndDontSave;

            m_capture.description = m_frameSender.description;
            m_capture.onCapture += OnCapture;
        }

        void OnEnable()
        {
            m_capture.enabled = true;
        }

        void OnDisable()
        {
            m_capture.enabled = false;
        }

        void Update()
        {
            if (m_RenderStream == null)
                return;
            
            m_newFrameData = m_RenderStream.HasNewFrameData &&
                             m_frameSender != null &&
                             m_frameSender.GetCameraData(ref m_cameraData);
            
            UpdateCamera(
                m_camera,
                m_newFrameData ? m_cameraData : null,
                m_frameSender?.subRegion
            );
        }
        
        void OnCapture(ScriptableRenderContext context, CameraCapture.Capture capture)
        {
            if (m_newFrameData)
            {
                m_frameSender?.SendFrame(m_RenderStream.LatestFrameData, m_cameraData, capture.cameraTexture);
                m_newFrameData = false;
            }
        }

        static void UpdateCamera(Camera cam, CameraData? cameraData, Rect? cameraSubRegion)
        {
            var transform = cam.transform;
            var cameraAspect = cam.aspect;
            var lensShift = new Vector2(0.0f, 0.0f);
            
            if (cameraData is { } data)
            {
                cameraAspect = data.sensorX / data.sensorY;
                if (data.cameraHandle != 0)  // If no camera, only set aspect
                {
                    transform.localPosition = new Vector3(data.x, data.y, data.z);
                    transform.localRotation = Quaternion.Euler(new Vector3(-data.rx, data.ry, -data.rz));
                    cam.nearClipPlane = data.nearZ;
                    cam.farClipPlane = data.farZ;

                    if (data.orthoWidth > 0.0f)  // Use an orthographic camera
                    {  
                        cam.orthographic = true;
                        cam.orthographicSize = 0.5f * data.orthoWidth / cameraAspect;
                        transform.localPosition = new Vector3(data.x, data.y, data.z);
                        transform.localRotation = Quaternion.Euler(new Vector3(-data.rx, data.ry, -data.rz));
                    }
                    else  // Perspective projection, use camera lens properties
                    {
                        cam.usePhysicalProperties = true;
                        cam.sensorSize = new Vector2(data.sensorX, data.sensorY);
                        cam.focalLength = data.focalLength;
                        lensShift = new Vector2(-data.cx, data.cy);
                    }
                }
            }
            else if (cameraSubRegion.HasValue)
            {
                var subRegion = cameraSubRegion.Value;
                
                // By default aspect is resolution aspect. We need to undo the effect of the subregion on this to get the whole image aspect.
                cameraAspect = cam.aspect * (subRegion.height / subRegion.width);
            }

            // Clip to correct subregion and calculate projection matrix
            if (cameraSubRegion.HasValue)
            {
                var subRegion = cameraSubRegion.Value;
                
                float imageHeight, imageWidth;
                if (cam.orthographic)
                {
                    imageHeight = 2.0f * cam.orthographicSize;
                    imageWidth = cameraAspect * imageHeight;
                }
                else
                {
                    float fovV = cam.fieldOfView * Mathf.Deg2Rad;
                    float fovH = Camera.VerticalToHorizontalFieldOfView(cam.fieldOfView, cameraAspect) * Mathf.Deg2Rad;
                    imageWidth = 2.0f * (float)Math.Tan(0.5f * fovH);
                    imageHeight = 2.0f * (float)Math.Tan(0.5f * fovV);
                }

                float l = (-0.5f + subRegion.xMin) * imageWidth;
                float r = (-0.5f + subRegion.xMax) * imageWidth;
                float t = (-0.5f + 1.0f - subRegion.yMin) * imageHeight;
                float b = (-0.5f + 1.0f - subRegion.yMax) * imageHeight;

                Matrix4x4 projectionMatrix;
                if (cam.orthographic)
                    projectionMatrix = Matrix4x4.Ortho(l, r, b, t, cam.nearClipPlane, cam.farClipPlane);
                else
                    projectionMatrix = PerspectiveOffCenter(l * cam.nearClipPlane, r * cam.nearClipPlane, b * cam.nearClipPlane, t * cam.nearClipPlane, cam.nearClipPlane, cam.farClipPlane);

                Matrix4x4 clippingTransform = Matrix4x4.Translate(new Vector3(-lensShift.x / subRegion.width, lensShift.y / subRegion.height, 0.0f));
                cam.projectionMatrix = clippingTransform * projectionMatrix;
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
    }
}
