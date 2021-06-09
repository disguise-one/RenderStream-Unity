#if UNITY_STANDALONE_WIN 
#define PLUGIN_AVAILABLE
#endif

using System;
using System.Net;
using System.Net.Sockets;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Reflection;

using Microsoft.Win32;

using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Serialization;
using UnityEngine.SceneManagement;
using System.Threading;
using System.Runtime.Remoting;
using Disguise.RenderStream;
using System.Linq;

#if UNITY_EDITOR
class DisguiseRenderStream  : UnityEditor.Build.IPreprocessBuildWithReport
#else
class DisguiseRenderStream
#endif
{
#if UNITY_EDITOR
    public int callbackOrder { get { return 0; } }
    public void OnPreprocessBuild(UnityEditor.Build.Reporting.BuildReport report)
    {
        DisguiseRenderStreamSettings settings = DisguiseRenderStreamSettings.GetOrCreateSettings();            
        schema = new ManagedSchema();
        schema.channels = new string[0];
        switch (settings.sceneControl)
        {
            case DisguiseRenderStreamSettings.SceneControl.Selection:
                Debug.Log("Generating scene-selection schema for: " + SceneManager.sceneCountInBuildSettings + " scenes");
                schema.scenes = new ManagedRemoteParameters[SceneManager.sceneCountInBuildSettings];
                if (SceneManager.sceneCountInBuildSettings == 0)
                    Debug.LogWarning("No scenes in build settings. Schema will be empty.");
                break;
            case DisguiseRenderStreamSettings.SceneControl.Manual:
            default:
                Debug.Log("Generating manual schema");
                schema.scenes = new ManagedRemoteParameters[1];
                break;
        }
    }

    [UnityEditor.Callbacks.PostProcessSceneAttribute(0)]
    static void OnPostProcessScene()
    {
        Scene activeScene = SceneManager.GetActiveScene();
        DisguiseRenderStreamSettings settings = DisguiseRenderStreamSettings.GetOrCreateSettings();            
        switch (settings.sceneControl)
        {
            case DisguiseRenderStreamSettings.SceneControl.Selection:
            {
                if (activeScene.buildIndex < 0 || activeScene.buildIndex >= SceneManager.sceneCountInBuildSettings)
                {
                    Debug.Log("Ignoring scene: " + activeScene.name + " (not in build's scene list)");
                    return;
                }

                Debug.Log("Processing scene: " + activeScene.name + " (" + activeScene.buildIndex + '/' + SceneManager.sceneCountInBuildSettings + ')');

                HashSet<string> channels = new HashSet<string>(schema.channels);
                channels.UnionWith(getTemplateCameras().Select(camera => camera.name));
                schema.channels = channels.ToArray();

                List<ManagedRemoteParameter> parameters = new List<ManagedRemoteParameter>();
                foreach (var parameter in UnityEngine.Object.FindObjectsOfType(typeof(DisguiseRemoteParameters)) as DisguiseRemoteParameters[])
                    parameters.AddRange(parameter.exposedParameters());
                schema.scenes[activeScene.buildIndex] = new ManagedRemoteParameters();
                ManagedRemoteParameters scene = schema.scenes[activeScene.buildIndex];
                scene.name = activeScene.name;
                scene.parameters = parameters.ToArray();
                break;
            }
            case DisguiseRenderStreamSettings.SceneControl.Manual:
            default:
            {
                Debug.Log("Processing scene: " + activeScene.name);

                HashSet<string> channels = new HashSet<string>(schema.channels);
                channels.UnionWith(getTemplateCameras().Select(camera => camera.name));
                schema.channels = channels.ToArray();

                if (schema.scenes[0] == null)
                {
                    schema.scenes[0] = new ManagedRemoteParameters();
                    schema.scenes[0].parameters = new ManagedRemoteParameter[0];
                }
                ManagedRemoteParameters scene = schema.scenes[0];
                scene.name = "Default";
                List<ManagedRemoteParameter> parameters = new List<ManagedRemoteParameter>(scene.parameters);
                foreach (var parameter in UnityEngine.Object.FindObjectsOfType(typeof(DisguiseRemoteParameters)) as DisguiseRemoteParameters[])
                    parameters.AddRange(parameter.exposedParameters());
                scene.parameters = parameters.ToArray();
                break;
            }
        }
    }

    [UnityEditor.Callbacks.PostProcessBuildAttribute(0)]
    static void OnPostProcessBuild(UnityEditor.BuildTarget target, string pathToBuiltProject)
    {
        if (target != UnityEditor.BuildTarget.StandaloneWindows64)
        {
            Debug.LogError("DisguiseRenderStream: RenderStream is only available for 64-bit Windows (x86_64).");
            return;
        }

        if (PluginEntry.instance.IsAvailable == false)
        {
            Debug.LogError("DisguiseRenderStream: RenderStream DLL not available, could not save schema");
            return;
        }

        RS_ERROR error = PluginEntry.instance.saveSchema(pathToBuiltProject, ref schema);
        if (error != RS_ERROR.RS_ERROR_SUCCESS)
        {
            Debug.LogError(string.Format("DisguiseRenderStream: Failed to save schema {0}", error));
        }
    }
#endif

    [RuntimeInitializeOnLoadMethod]
    static void OnLoad()
    {
        if (PluginEntry.instance.IsAvailable == false)
        {
            Debug.LogError("DisguiseRenderStream: RenderStream DLL not available");
            return;
        }

        string pathToBuiltProject = System.Diagnostics.Process.GetCurrentProcess().MainModule.FileName;
        RS_ERROR error = PluginEntry.instance.loadSchema(pathToBuiltProject, ref schema);
        if (error == RS_ERROR.RS_ERROR_SUCCESS)
        {
            sceneFields = new List<ObjectField>[schema.scenes.Length];
            Scene activeScene = SceneManager.GetActiveScene();
            if (activeScene.isLoaded)
                OnSceneLoaded(activeScene, LoadSceneMode.Single);
            SceneManager.sceneLoaded += OnSceneLoaded;
        }
        else
        {
            Debug.LogError(string.Format("DisguiseRenderStream: Failed to load schema {0}", error));
            schema = new ManagedSchema();
            schema.channels = new string[0];
            schema.scenes = new ManagedRemoteParameters[1];
            schema.scenes[0] = new ManagedRemoteParameters();
            schema.scenes[0].name = "Default";
            schema.scenes[0].parameters = new ManagedRemoteParameter[0];
            sceneFields = new List<ObjectField>[schema.scenes.Length];
            CreateStreams();
        }
    }

    static void OnSceneLoaded(Scene loadedScene, LoadSceneMode mode)
    {
        CreateStreams();
        int sceneIndex = 0;
        DisguiseRenderStreamSettings settings = DisguiseRenderStreamSettings.GetOrCreateSettings();
        switch (settings.sceneControl)
        {
            case DisguiseRenderStreamSettings.SceneControl.Selection:
                sceneIndex = loadedScene.buildIndex;
                break;
        }
        DisguiseRemoteParameters[] remoteParameters = UnityEngine.Object.FindObjectsOfType(typeof(DisguiseRemoteParameters)) as DisguiseRemoteParameters[];
        ManagedRemoteParameters scene = schema.scenes[sceneIndex];
        sceneFields[sceneIndex] = new List<ObjectField>();
        List<ObjectField> fields = sceneFields[sceneIndex];
        for (int j = 0; j < scene.parameters.Length;)
        {
            string key = scene.parameters[j].key;
            DisguiseRemoteParameters remoteParams = Array.Find(remoteParameters, rp => key.StartsWith(rp.prefix));
            ObjectField field = new ObjectField();
            field.target = remoteParams.exposedObject;
            field.info = null;
            if (field.info == null && key.EndsWith("_x"))
            {
                string baseKey = key.Substring(0, key.Length - 2);
                field.info = remoteParams.GetMemberInfoFromPropertyPath(baseKey.Substring(remoteParams.prefix.Length + 1));
                Type fieldType = field.FieldType;
                if ((fieldType == typeof(Vector2) || fieldType == typeof(Vector2Int)) &&
                    j + 1 < scene.parameters.Length && scene.parameters[j + 1].key == baseKey + "_y")
                {
                    j += 2;
                }
                else if ((fieldType == typeof(Vector3) || fieldType == typeof(Vector3Int)) &&
                    j + 2 < scene.parameters.Length && scene.parameters[j + 1].key == baseKey + "_y" && scene.parameters[j + 2].key == baseKey + "_z")
                {
                    j += 3;
                }
                else if (fieldType == typeof(Vector4) &&
                    j + 3 < scene.parameters.Length && scene.parameters[j + 1].key == baseKey + "_y" && scene.parameters[j + 2].key == baseKey + "_z" && scene.parameters[j + 3].key == baseKey + "_w")
                {
                    j += 4;
                }
                else
                {
                    field.info = null;
                }
            }
            if (field.info == null && key.EndsWith("_r"))
            {
                string baseKey = key.Substring(0, key.Length - 2);
                field.info = remoteParams.GetMemberInfoFromPropertyPath(baseKey.Substring(remoteParams.prefix.Length + 1));
                Type fieldType = field.FieldType;
                if (fieldType == typeof(Color) &&
                    j + 3 < scene.parameters.Length && scene.parameters[j + 1].key == baseKey + "_g" && scene.parameters[j + 2].key == baseKey + "_b" && scene.parameters[j + 3].key == baseKey + "_a")
                {
                    j += 4;
                }
                else
                {
                    field.info = null;
                }
            }
            if (field.info == null)
            {
                field.info = remoteParams.GetMemberInfoFromPropertyPath(key.Substring(remoteParams.prefix.Length + 1));
                ++j;
            }
            if (field.info == null)
            {
                Debug.LogError("Unhandled remote parameter: " + key);
            }
            fields.Add(field);
        }
    }

    static void CreateStreams()
    {
        if (PluginEntry.instance.IsAvailable == false)
        {
            Debug.LogError("DisguiseRenderStream: RenderStream DLL not available");
            return;
        }

        RS_ERROR error = PluginEntry.instance.getStreams(ref streams);
        if (error != RS_ERROR.RS_ERROR_SUCCESS)
        {
            Debug.LogError(string.Format("DisguiseRenderStream: Failed to get streams {0}", error));
            return;
        }

        Debug.Log(string.Format("Found {0} streams", streams.Length));
        foreach (var camera in cameras)
            UnityEngine.Object.Destroy(camera);
        cameras = new GameObject[streams.Length];

        // cache the template cameras prior to instantiating our instance cameras 
        Camera[] templateCameras = getTemplateCameras();
        const int cullUIOnly = ~(1 << 5);

        for (int i = 0; i < streams.Length; ++i)
        {        
            StreamDescription stream = streams[i];
            Camera channelCamera = DisguiseRenderStream.GetChannelCamera(stream.channel);
            if (channelCamera)
            {
                cameras[i] = UnityEngine.Object.Instantiate(channelCamera.gameObject, channelCamera.gameObject.transform.parent);
                cameras[i].name = stream.name;
            }
            else if (Camera.main)
            {
                cameras[i] = UnityEngine.Object.Instantiate(Camera.main.gameObject, Camera.main.gameObject.transform.parent);
                cameras[i].name = stream.name;
            }
            else
            {
                cameras[i] = new GameObject(stream.name);
                cameras[i].AddComponent<Camera>();
            }
            
            GameObject cameraObject = cameras[i];
            Camera camera = cameraObject.GetComponent<Camera>();
            camera.enabled = true; // ensure the camera component is enable
            camera.cullingMask &= cullUIOnly; // cull the UI so RenderStream and other error messages don't render to RenderStream outputs
            DisguiseCameraCapture capture = cameraObject.GetComponent(typeof(DisguiseCameraCapture)) as DisguiseCameraCapture;
            if (capture == null)
                capture = cameraObject.AddComponent(typeof(DisguiseCameraCapture)) as DisguiseCameraCapture;
            camera.enabled = true;
        }

        // stop template cameras impacting performance
        foreach (var templateCam in templateCameras)
        {
            templateCam.enabled = false; // disable the camera component on the template camera so these cameras won't render and impact performance
            // we don't want to disable the game object otherwise we won't be able to find the object again to instantiate instance cameras if we get a streams changed event
        }

        frameData = new FrameData();
        awaiting = false;
    }

    static public IEnumerator AwaitFrame()
    {
        if (awaiting)
            yield break;
        awaiting = true;
        DisguiseRenderStreamSettings settings = DisguiseRenderStreamSettings.GetOrCreateSettings();
        while (true)
        {
            yield return new WaitForEndOfFrame();
            RS_ERROR error = PluginEntry.instance.awaitFrameData(500, ref frameData);
            if (error == RS_ERROR.RS_ERROR_STREAMS_CHANGED)
                CreateStreams();
            switch (settings.sceneControl)
            {
                case DisguiseRenderStreamSettings.SceneControl.Selection:
                    if (SceneManager.GetActiveScene().buildIndex != frameData.scene)
                    {
                        newFrameData = false;
                        SceneManager.LoadScene((int)frameData.scene);
                        yield break;
                    }
                    break;
            }
            newFrameData = (error == RS_ERROR.RS_ERROR_SUCCESS);
            if (newFrameData && frameData.scene < schema.scenes.Length)
            {
                ManagedRemoteParameters spec = schema.scenes[frameData.scene];
                float[] parameters = new float[spec.parameters.Length];
                if (PluginEntry.instance.getFrameParameters(spec.hash, ref parameters) == RS_ERROR.RS_ERROR_SUCCESS)
                {
                    List<ObjectField> fields = sceneFields[frameData.scene];
                    if (fields != null)
                    {
                        int i = 0;
                        foreach (var field in fields)
                        {
                            Type fieldType = field.FieldType;
                            if (fieldType.IsEnum)
                            {
                                field.SetValue(Enum.ToObject(fieldType, Convert.ToUInt64(parameters[i])));
                                ++i;
                            }
                            else if (fieldType == typeof(Vector2))
                            {
                                Vector2 v = new Vector2(parameters[i + 0], parameters[i + 1]);
                                field.SetValue(v);
                                i += 2;
                            }
                            else if (fieldType == typeof(Vector2Int))
                            {
                                Vector2Int v = new Vector2Int((int)parameters[i + 0], (int)parameters[i + 1]);
                                field.SetValue(v);
                                i += 2;
                            }
                            else if (fieldType == typeof(Vector3))
                            {
                                Vector3 v = new Vector3(parameters[i + 0], parameters[i + 1], parameters[i + 2]);
                                field.SetValue(v);
                                i += 3;
                            }
                            else if (fieldType == typeof(Vector3Int))
                            {
                                Vector3Int v = new Vector3Int((int)parameters[i + 0], (int)parameters[i + 1], (int)parameters[i + 2]);
                                field.SetValue(v);
                                i += 3;
                            }
                            else if (fieldType == typeof(Vector4))
                            {
                                Vector4 v = new Vector4(parameters[i + 0], parameters[i + 1], parameters[i + 2], parameters[i + 3]);
                                field.SetValue(v);
                                i += 4;
                            }
                            else if (fieldType == typeof(Color))
                            {
                                Color v = new Color(parameters[i + 0], parameters[i + 1], parameters[i + 2], parameters[i + 3]);
                                field.SetValue(v);
                                i += 4;
                            }
                            else
                            {
                                if (field.info != null)
                                    field.SetValue(Convert.ChangeType(parameters[i], fieldType));
                                ++i;
                            }
                        }
                    }
                }
            }
        }
    }

    static Camera[] getTemplateCameras()
    {
        return Camera.allCameras;
    }

    static Camera GetChannelCamera(string channel)
    {
        try
        {
            return Array.Find(getTemplateCameras(), camera => camera.name == channel);
        }
        catch (ArgumentNullException)
        {
            return Camera.main;
        }
    }

    static public StreamDescription[] streams = { };
    static public bool awaiting = false;
    static public FrameData frameData;
    static public bool newFrameData = false;

    static private GameObject[] cameras = { };
    static private ManagedSchema schema = new ManagedSchema();
    public class ObjectField
    {
        public object target;
        public MemberInfo info;
        public Type FieldType { 
            get {
                if (info is FieldInfo fieldInfo)
                    return fieldInfo.FieldType;
                else if (info is PropertyInfo propertyInfo)
                    return propertyInfo.PropertyType;
                return typeof(void);
            }
        }
        public void SetValue(object value)
        {
            if (info is FieldInfo fieldInfo)
                fieldInfo.SetValue(target, value);
            else if (info is PropertyInfo propertyInfo)
                propertyInfo.SetValue(target, value);
        }
    }
    static private List<ObjectField>[] sceneFields = new List<ObjectField>[0];
}

[AddComponentMenu("")]
[RequireComponent(typeof(Camera))]
public class DisguiseCameraCapture : MonoBehaviour
{
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

        if (Application.isPlaying == false)
            yield break;

        if (!DisguiseRenderStream.awaiting)
            yield return StartCoroutine(DisguiseRenderStream.AwaitFrame());
    }

    // Update is called once per frame
    public void Update()
    {
        // set tracking
        m_newFrameData = DisguiseRenderStream.newFrameData && m_frameSender != null && m_frameSender.GetCameraData(ref m_cameraData);
        float cameraAspect = m_camera.aspect;
        Vector2 lensShift = new Vector2(0.0f, 0.0f);
        if (m_newFrameData && m_cameraData.cameraHandle != 0)
        {

            cameraAspect = m_cameraData.sensorX / m_cameraData.sensorY;
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
                m_frameSender.SendFrame(DisguiseRenderStream.frameData, m_cameraData);
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
    Disguise.RenderStream.FrameSender m_frameSender;

    CameraData m_cameraData;
    bool m_newFrameData = false;
}

namespace Disguise.RenderStream
{
    // d3renderstream/d3renderstream.h
    using StreamHandle = UInt64;
    using CameraHandle = UInt64;
    delegate void logger_t(string message);

    public enum RSPixelFormat : UInt32
    {
        RS_FMT_INVALID,

        RS_FMT_BGRA8,
        RS_FMT_BGRX8,

        //RS_FMT_RGB10X2,
        //RS_FMT_BGR10X2,

        //RS_FMT_RGBA16F,
        //RS_FMT_BGRA16F,

        RS_FMT_RGBA32F,
    }

    public enum SenderFrameType : UInt32
    {
        RS_FRAMETYPE_HOST_MEMORY = 0x00000000,
        RS_FRAMETYPE_DX11_TEXTURE,
        RS_FRAMETYPE_DX12_TEXTURE
    }

    public enum RS_ERROR : UInt32
    {
        RS_ERROR_SUCCESS = 0,

        // Core is not initialised
        RS_NOT_INITIALISED,

        // Core is already initialised
        RS_ERROR_ALREADYINITIALISED,

        // Given handle is invalid
        RS_ERROR_INVALIDHANDLE,

        // Maximum number of frame senders have been created
        RS_MAXSENDERSREACHED,

        RS_ERROR_BADSTREAMTYPE,

        RS_ERROR_NOTFOUND,

        RS_ERROR_INCORRECTSCHEMA,

        RS_ERROR_INVALID_PARAMETERS,

        RS_ERROR_BUFFER_OVERFLOW,

        RS_ERROR_TIMEOUT,

        RS_ERROR_STREAMS_CHANGED,

        RS_ERROR_INCOMPATIBLE_VERSION,

        RS_ERROR_UNSPECIFIED
    }
	
	[StructLayout(LayoutKind.Sequential, Pack = 4)]
	public struct D3TrackingData
	{
		public float virtualZoomScale;
		public byte virtualReprojectionRequired;
		public float xRealCamera, yRealCamera, zRealCamera;
		public float rxRealCamera, ryRealCamera, rzRealCamera;
	}  // Tracking data required by d3 but not used to render content

    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    public struct CameraData
    {
        public StreamHandle streamHandle;
        public CameraHandle cameraHandle;
        public float x, y, z;
        public float rx, ry, rz;
        public float focalLength;
        public float sensorX, sensorY;
        public float cx, cy;
        public float nearZ, farZ;
        public float orthoWidth;
		public D3TrackingData d3Tracking;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    public struct FrameData
    {
        public double tTracked;
        public double localTime;
        public double localTimeDelta;
        public UInt32 frameRateNumerator;
        public UInt32 frameRateDenominator;
        public UInt32 flags;
        public UInt32 scene;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    public struct CameraResponseData
    {
        public double tTracked;
        public CameraData camera;
    }

    [StructLayout(LayoutKind.Explicit)]
    public /*union*/ struct SenderFrameTypeData
    {
        // struct HostMemoryData
        [FieldOffset(0)]
        public /*uint8_t**/ IntPtr host_data;
        [FieldOffset(8)]
        public UInt32 host_stride;
        // struct Dx11Data
        [FieldOffset(0)]
        public /*struct ID3D11Resource**/ IntPtr dx11_resource;
        // struct Dx12Data
        [FieldOffset(0)]
        public /*struct ID3D12Resource**/ IntPtr dx12_resource;
        [FieldOffset(8)]
        public /*struct ID3D12Fence**/ IntPtr dx12_fence;
        [FieldOffset(16)]
        public Int32 dx12_fenceValue;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    public struct FrameRegion
    {
        public UInt32 xOffset;
        public UInt32 yOffset;
        public UInt32 width;
        public UInt32 height;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    public struct ProjectionClipping
    {
        public float left;
        public float right;
        public float top;
        public float bottom;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    public struct StreamDescription
    {
        public StreamHandle handle;
        [MarshalAs(UnmanagedType.LPStr)]
        public string channel;
        [MarshalAs(UnmanagedType.LPStr)]
        public string name;
        public UInt32 width;
        public UInt32 height;
        public RSPixelFormat format;
        public ProjectionClipping clipping;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    public struct StreamDescriptions
    {
        public UInt32 nStreams;
        public /*StreamDescription**/ IntPtr streams;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    public struct RemoteParameter
    {
        [MarshalAs(UnmanagedType.LPStr)]
        public string group;
        [MarshalAs(UnmanagedType.LPStr)]
        public string displayName;
        [MarshalAs(UnmanagedType.LPStr)]
        public string key;
        public float min;
        public float max;
        public float step;
        public float defaultValue;
        public UInt32 nOptions;
        public /*const char***/ IntPtr options;

        public Int32 dmxOffset;
        public UInt32 dmxType;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    public struct RemoteParameters
    {
        [MarshalAs(UnmanagedType.LPStr)]
        public string name;
        public UInt32 nParameters;
        public /*RemoteParameter**/ IntPtr parameters;
        public UInt64 hash;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    public struct Scenes
    {
        public UInt32 nScenes;
        public /*RemoteParameters**/ IntPtr scenes;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    public struct Channels
    {
        public UInt32 nChannels;
        public /*const char***/ IntPtr channels;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    public struct Schema
    {
        public Channels channels;
        public Scenes scenes;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    public struct ProfilingEntry
    {
        public string name;
        public float value;
    }


    public class ManagedRemoteParameter
    {
        public string group;
        public string displayName;
        public string key;
        public float min;
        public float max;
        public float step;
        public float defaultValue;
        public string[] options = { };

        public Int32 dmxOffset;
        public UInt32 dmxType;
    }

    public class ManagedRemoteParameters
    {
        public string name;
        public ManagedRemoteParameter[] parameters = { };
        public UInt64 hash;
    }

    public class ManagedSchema
    {
        public string[] channels = { };
        public ManagedRemoteParameters[] scenes = { };
    }

    [Serializable]
    public sealed class PluginEntry
    {
        private class Nested
        {
            // Explicit static constructor to tell C# compiler
            // not to mark type as beforefieldinit
            static Nested() { }

            internal static readonly PluginEntry instance = new PluginEntry();
        }

        public static PluginEntry instance { get { return Nested.instance; } }

        // isolated functions, do not require init prior to use
        unsafe delegate void pRegisterLoggingFunc(logger_t logger);
        unsafe delegate void pRegisterErrorLoggingFunc(logger_t logger);
        unsafe delegate void pRegisterVerboseLoggingFunc(logger_t logger);

        unsafe delegate void pUnregisterLoggingFunc();
        unsafe delegate void pUnregisterErrorLoggingFunc();
        unsafe delegate void pUnregisterVerboseLoggingFunc();

        unsafe delegate RS_ERROR pInitialise(int expectedVersionMajor, int expectedVersionMinor);
        unsafe delegate RS_ERROR pShutdown();

        // non-isolated functions, these require init prior to use
        unsafe delegate RS_ERROR pSaveSchema(string assetPath, /*Schema**/ IntPtr schema); // Save schema for project file/custom executable at (assetPath)
        unsafe delegate RS_ERROR pLoadSchema(string assetPath, /*Out*/ /*Schema**/ IntPtr schema, /*InOut*/ ref UInt32 nBytes); // Load schema for project file/custom executable at (assetPath) into a buffer of size (nBytes) starting at (schema)

        // workload functions, these require the process to be running inside d3's asset launcher environment
        unsafe delegate RS_ERROR pSetSchema(/*InOut*/ /*Schema**/ IntPtr schema); // Set schema and fill in per-scene hash for use with rs_getFrameParameters

        unsafe delegate RS_ERROR pGetStreams(/*Out*/ /*StreamDescriptions**/ IntPtr streams, /*InOut*/ ref UInt32 nBytes); // Populate streams into a buffer of size (nBytes) starting at (streams)

        unsafe delegate RS_ERROR pAwaitFrameData(int timeoutMs, /*Out*/ /*FrameData**/ IntPtr data);  // waits for any asset, any stream to request a frame, provides the parameters for that frame.
        unsafe delegate RS_ERROR pSetFollower(int isFollower); // Used to mark this node as relying on alternative mechanisms to distribute FrameData. Users must provide correct CameraResponseData to sendFrame, and call rs_beginFollowerFrame at the start of the frame, where awaitFrame would normally be called.
        unsafe delegate RS_ERROR pBeginFollowerFrame(double tTracked); // Pass the engine-distributed tTracked value in, if you have called rs_setFollower(1) otherwise do not call this function.

        unsafe delegate RS_ERROR pSendFrame(StreamHandle streamHandle, SenderFrameType frameType, SenderFrameTypeData data, /*const CameraResponseData**/ IntPtr sendData); // publish a frame buffer which was generated from the associated tracking and timing information.
        unsafe delegate RS_ERROR pGetFrameParameters(UInt64 schemaHash, /*Out*/ /*void**/ IntPtr outParameterData, UInt64 outParameterDataSize);  // returns the remote parameters for this frame.
        unsafe delegate RS_ERROR pGetFrameCamera(StreamHandle streamHandle, /*Out*/ /*CameraData**/ IntPtr outCameraData);  // returns the CameraData for this stream, or RS_ERROR_NOTFOUND if no camera data is available for this stream on this frame

        unsafe delegate RS_ERROR pLogToD3(string str);
        unsafe delegate RS_ERROR pSendProfilingData(/*ProfilingEntry**/ IntPtr entries, int count);
        unsafe delegate RS_ERROR pSetNewStatusMessage(string msg);

        pRegisterLoggingFunc m_registerLoggingFunc = null;
        pRegisterErrorLoggingFunc m_registerErrorLoggingFunc = null;
        pRegisterVerboseLoggingFunc m_registerVerboseLoggingFunc = null;

        pUnregisterLoggingFunc m_unregisterLoggingFunc = null;
        pUnregisterErrorLoggingFunc m_unregisterErrorLoggingFunc = null;
        pUnregisterVerboseLoggingFunc m_unregisterVerboseLoggingFunc = null;

        pInitialise m_initialise = null;
        pShutdown m_shutdown = null;

        pSaveSchema m_saveSchema = null;
        pLoadSchema m_loadSchema = null;

        pSetSchema m_setSchema = null;

        pGetStreams m_getStreams = null;

        pAwaitFrameData m_awaitFrameData = null;
        pSetFollower m_setFollower = null;
        pBeginFollowerFrame m_beginFollowerFrame = null;

        pSendFrame m_sendFrame = null;
        pGetFrameParameters m_getFrameParameters = null;
        pGetFrameCamera m_getFrameCamera = null;

        pLogToD3 m_logToD3 = null;
        pSendProfilingData m_sendProfilingData = null;
        pSetNewStatusMessage m_setNewStatusMessage = null;

        void logInfo(string message)
        {
            Debug.Log(message);
        }

        void logError(string message)
        {
            Debug.LogError(message);
        }

        void logToD3(string logString, string stackTrace, LogType type)
        {
            if (m_logToD3 == null)
                return;

            string prefix = "";
            switch(type)
            {
                case LogType.Error:
                    prefix = "!!!!! ";
                    break;
                case LogType.Assert:
                    prefix = "!!!!! ASSERT: ";
                    break;
                case LogType.Warning:
                    prefix = "!!! ";
                    break;
                case LogType.Exception:
                    prefix = "!!!!! Exception: ";
                    break;
            }

            string trace = String.IsNullOrEmpty(stackTrace) ? "" : "\nTrace: " + stackTrace;

            m_logToD3(prefix + logString + trace + '\n');
        }

        void setNewStatusMessage(string message)
        {
            m_setNewStatusMessage?.Invoke(message);
        }

        ManagedSchema schemaToManagedSchema(Schema cSchema)
        {
            ManagedSchema schema = new ManagedSchema();
            schema.channels = new string[cSchema.channels.nChannels];
            for (int i = 0; i < cSchema.channels.nChannels; ++i)
            {
                IntPtr channelPtr = Marshal.ReadIntPtr(cSchema.channels.channels, i * Marshal.SizeOf(typeof(IntPtr)));
                schema.channels[i] = Marshal.PtrToStringAnsi(channelPtr);
            }
            schema.scenes = new ManagedRemoteParameters[cSchema.scenes.nScenes];
            for (int i = 0; i < cSchema.scenes.nScenes; ++i)
            {
                schema.scenes[i] = new ManagedRemoteParameters();
                ManagedRemoteParameters managedParameters = schema.scenes[i];
                RemoteParameters parameters = (RemoteParameters)Marshal.PtrToStructure(cSchema.scenes.scenes + i * Marshal.SizeOf(typeof(RemoteParameters)), typeof(RemoteParameters));
                managedParameters.name = parameters.name;
                managedParameters.parameters = new ManagedRemoteParameter[parameters.nParameters];
                for (int j = 0; j < parameters.nParameters; ++j)
                {
                    managedParameters.parameters[j] = new ManagedRemoteParameter();
                    ManagedRemoteParameter managedParameter = managedParameters.parameters[j];
                    RemoteParameter parameter = (RemoteParameter)Marshal.PtrToStructure(parameters.parameters + j * Marshal.SizeOf(typeof(RemoteParameter)), typeof(RemoteParameter));
                    managedParameter.group = parameter.group;
                    managedParameter.displayName = parameter.displayName;
                    managedParameter.key = parameter.key;
                    managedParameter.min = parameter.min;
                    managedParameter.max = parameter.max;
                    managedParameter.step = parameter.step;
                    managedParameter.defaultValue = parameter.defaultValue;
                    managedParameter.options = new string[parameter.nOptions];
                    for (int k = 0; k < parameter.nOptions; ++k)
                    {
                        IntPtr optionPtr = Marshal.ReadIntPtr(parameter.options, k * Marshal.SizeOf(typeof(IntPtr)));
                        managedParameter.options[i] = Marshal.PtrToStringAnsi(optionPtr);
                    }
                    managedParameter.dmxOffset = parameter.dmxOffset;
                    managedParameter.dmxType = parameter.dmxType;
                }
                managedParameters.hash = parameters.hash;
            }
            return schema;
        }

        public RS_ERROR saveSchema(string assetPath, ref ManagedSchema schema)
        {
            if (m_saveSchema == null)
                return RS_ERROR.RS_NOT_INITIALISED;

            List<IntPtr> allocations = new List<IntPtr>();
            try
            {
                Schema cSchema = new Schema();
                cSchema.channels.nChannels = (UInt32)schema.channels.Length;
                cSchema.channels.channels = Marshal.AllocHGlobal(Marshal.SizeOf(typeof(IntPtr)) * (int)cSchema.channels.nChannels);
                allocations.Add(cSchema.channels.channels);
                for (int i = 0; i < cSchema.channels.nChannels; ++i)
                {
                    IntPtr channelPtr = Marshal.StringToHGlobalAnsi(schema.channels[i]);
                    allocations.Add(channelPtr);
                    Marshal.WriteIntPtr(cSchema.channels.channels, i * Marshal.SizeOf(typeof(IntPtr)), channelPtr);
                }

                cSchema.scenes.nScenes = (UInt32)schema.scenes.Length;
                cSchema.scenes.scenes = Marshal.AllocHGlobal(Marshal.SizeOf(typeof(RemoteParameters)) * (int)cSchema.scenes.nScenes);
                allocations.Add(cSchema.scenes.scenes);
                for (int i = 0; i < cSchema.scenes.nScenes; ++i)
                {
                    ManagedRemoteParameters managedParameters = schema.scenes[i];
                    RemoteParameters parameters = new RemoteParameters();
                    parameters.name = managedParameters.name;
                    parameters.nParameters = (UInt32)managedParameters.parameters.Length;
                    parameters.parameters = Marshal.AllocHGlobal(Marshal.SizeOf(typeof(RemoteParameter)) * (int)parameters.nParameters);
                    allocations.Add(parameters.parameters);
                    for (int j = 0; j < parameters.nParameters; ++j)
                    {
                        ManagedRemoteParameter managedParameter = managedParameters.parameters[j];
                        RemoteParameter parameter = new RemoteParameter();
                        parameter.group = managedParameter.group;
                        parameter.displayName = managedParameter.displayName;
                        parameter.key = managedParameter.key;
                        parameter.min = managedParameter.min;
                        parameter.max = managedParameter.max;
                        parameter.step = managedParameter.step;
                        parameter.defaultValue = managedParameter.defaultValue;
                        parameter.nOptions = (UInt32)managedParameter.options.Length;
                        parameter.options = Marshal.AllocHGlobal(Marshal.SizeOf(typeof(IntPtr)) * (int)parameter.nOptions);
                        allocations.Add(parameter.options);
                        for (int k = 0; k < parameter.nOptions; ++k)
                        {
                            IntPtr optionPtr = Marshal.StringToHGlobalAnsi(managedParameter.options[k]);
                            allocations.Add(optionPtr);
                            Marshal.WriteIntPtr(parameter.options, k * Marshal.SizeOf(typeof(IntPtr)), optionPtr);
                        }
                        parameter.dmxOffset = managedParameter.dmxOffset;
                        parameter.dmxType = managedParameter.dmxType;
                        Marshal.StructureToPtr(parameter, parameters.parameters + j * Marshal.SizeOf(typeof(RemoteParameter)), false);
                    }
                    Marshal.StructureToPtr(parameters, cSchema.scenes.scenes + i * Marshal.SizeOf(typeof(RemoteParameters)), false);
                }

                IntPtr schemaPtr = Marshal.AllocHGlobal(Marshal.SizeOf(typeof(Schema)));
                allocations.Add(schemaPtr);
                Marshal.StructureToPtr(cSchema, schemaPtr, false);
                RS_ERROR error = m_saveSchema(assetPath, schemaPtr);
                if (error == RS_ERROR.RS_ERROR_SUCCESS)
                {
                    cSchema = (Schema)Marshal.PtrToStructure(schemaPtr, typeof(Schema));
                    schema = schemaToManagedSchema(cSchema);
                }
                return error;
            }
            finally
            {
                foreach (IntPtr ptr in allocations)
                    Marshal.FreeHGlobal(ptr);
            }
            //return RS_ERROR.RS_ERROR_UNSPECIFIED;
        }

        public RS_ERROR loadSchema(string assetPath, ref ManagedSchema schema)
        {
            if (m_loadSchema == null)
                return RS_ERROR.RS_NOT_INITIALISED;

            IntPtr descMem = IntPtr.Zero;
            UInt32 nBytes = 0;
            m_loadSchema(assetPath, descMem, ref nBytes);

            const int MAX_TRIES = 3;
            int iterations = 0;

            RS_ERROR res = RS_ERROR.RS_ERROR_BUFFER_OVERFLOW;
            try
            {
                do
                {
                    Marshal.FreeHGlobal(descMem);
                    descMem = Marshal.AllocHGlobal((int)nBytes);
                    res = m_loadSchema(assetPath, descMem, ref nBytes);
                    if (res == RS_ERROR.RS_ERROR_SUCCESS)
                    {
                        Schema cSchema = (Schema)Marshal.PtrToStructure(descMem, typeof(Schema));
                        schema = schemaToManagedSchema(cSchema);
                    }

                    ++iterations;
                } while (res == RS_ERROR.RS_ERROR_BUFFER_OVERFLOW && iterations < MAX_TRIES);
            }
            finally
            {
                Marshal.FreeHGlobal(descMem);
            }
            return res;
        }

        public RS_ERROR getStreams(ref StreamDescription[] streams)
        {
            if (m_getStreams == null)
                return RS_ERROR.RS_NOT_INITIALISED;

            IntPtr descMem = IntPtr.Zero;
            UInt32 nBytes = 0;
            m_getStreams(descMem, ref nBytes);

            const int MAX_TRIES = 3;
            int iterations = 0;

            RS_ERROR res = RS_ERROR.RS_ERROR_BUFFER_OVERFLOW;
            try
            {
                do
                {
                    Marshal.FreeHGlobal(descMem);
                    descMem = Marshal.AllocHGlobal((int)nBytes);
                    res = m_getStreams(descMem, ref nBytes);
                    if (res == RS_ERROR.RS_ERROR_SUCCESS)
                    {
                        StreamDescriptions desc = (StreamDescriptions)Marshal.PtrToStructure(descMem, typeof(StreamDescriptions));
                        streams = new StreamDescription[desc.nStreams];
                        for (int i = 0; i < desc.nStreams; ++i)
                        {
                            IntPtr current = desc.streams + i * Marshal.SizeOf(typeof(StreamDescription));
                            streams[i] = (StreamDescription)Marshal.PtrToStructure(current, typeof(StreamDescription));
                        }
                    }

                    ++iterations;
                } while (res == RS_ERROR.RS_ERROR_BUFFER_OVERFLOW && iterations < MAX_TRIES);
            }
            finally
            {
                Marshal.FreeHGlobal(descMem);
            }
            return res;
        }

        public RS_ERROR sendFrame(StreamHandle streamHandle, SenderFrameType frameType, SenderFrameTypeData data, CameraResponseData sendData)
        {
            if (m_sendFrame == null)
                return RS_ERROR.RS_NOT_INITIALISED;

            if (handleReference.IsAllocated)
                handleReference.Free();
            handleReference = GCHandle.Alloc(sendData, GCHandleType.Pinned);
            try
            {
                RS_ERROR error = m_sendFrame(streamHandle, frameType, data, handleReference.AddrOfPinnedObject());
                return error;
            }
            finally
            {
                if (handleReference.IsAllocated)
                    handleReference.Free();
            }
            //return RS_ERROR.RS_ERROR_UNSPECIFIED;
        }

        public RS_ERROR awaitFrameData(int timeoutMs, ref FrameData data)
        {
            if (m_awaitFrameData == null)
                return RS_ERROR.RS_NOT_INITIALISED;

            if (handleReference.IsAllocated)
                handleReference.Free();
            handleReference = GCHandle.Alloc(data, GCHandleType.Pinned);
            try
            {
                RS_ERROR error = m_awaitFrameData(timeoutMs, handleReference.AddrOfPinnedObject());
                if (error == RS_ERROR.RS_ERROR_SUCCESS)
                {
                    data = (FrameData)Marshal.PtrToStructure(handleReference.AddrOfPinnedObject(), typeof(FrameData));
                }
                return error;
            }
            finally
            {
                if (handleReference.IsAllocated)
                    handleReference.Free();
            }
            //return RS_ERROR.RS_ERROR_UNSPECIFIED;
        }

        public RS_ERROR setFollower(int isFollower)
        {
            if (m_setFollower == null)
                return RS_ERROR.RS_NOT_INITIALISED;

            try
            {
                RS_ERROR error = m_setFollower(isFollower);
                return error;
            }
            finally
            {
            }
        }

        public RS_ERROR beginFollowerFrame(double tTracked)
        {
            if (m_beginFollowerFrame == null)
                return RS_ERROR.RS_NOT_INITIALISED;

            try
            {
                RS_ERROR error = beginFollowerFrame(tTracked);
                return error;
            }
            finally
            {
            }
        }

        public RS_ERROR getFrameParameters(UInt64 schemaHash, ref float[] outParameterData)
        {
            if (m_getFrameParameters == null)
                return RS_ERROR.RS_NOT_INITIALISED;

            if (handleReference.IsAllocated)
                handleReference.Free();
            handleReference = GCHandle.Alloc(outParameterData, GCHandleType.Pinned);
            try
            {
                RS_ERROR error = m_getFrameParameters(schemaHash, handleReference.AddrOfPinnedObject(), (UInt64)outParameterData.Length * sizeof(float));
                if (error == RS_ERROR.RS_ERROR_SUCCESS)
                {
                    Marshal.Copy(handleReference.AddrOfPinnedObject(), outParameterData, 0, outParameterData.Length);
                }
                return error;
            }
            finally
            {
                if (handleReference.IsAllocated)
                    handleReference.Free();
            }
            //return RS_ERROR.RS_ERROR_UNSPECIFIED;
        }

        public RS_ERROR getFrameCamera(StreamHandle streamHandle, ref CameraData outCameraData)
        {
            if (m_getFrameCamera == null)
                return RS_ERROR.RS_NOT_INITIALISED;

            if (handleReference.IsAllocated)
                handleReference.Free();
            handleReference = GCHandle.Alloc(outCameraData, GCHandleType.Pinned);
            try
            {
                RS_ERROR error = m_getFrameCamera(streamHandle, handleReference.AddrOfPinnedObject());
                if (error == RS_ERROR.RS_ERROR_SUCCESS)
                {
                    outCameraData = (CameraData)Marshal.PtrToStructure(handleReference.AddrOfPinnedObject(), typeof(CameraData));
                }
                return error;
            }
            finally
            {
                if (handleReference.IsAllocated)
                    handleReference.Free();
            }
            //return RS_ERROR.RS_ERROR_UNSPECIFIED;
        }

#if PLUGIN_AVAILABLE

        [DllImport("kernel32.dll")]
        static extern IntPtr LoadLibraryEx(string lpLibFileName, IntPtr fileHandle, int flags);

        [DllImport("kernel32.dll")]
        static extern IntPtr GetProcAddress(IntPtr hModule, string lpProcName);

        [DllImport("kernel32.dll", CharSet = CharSet.Ansi)]
        static extern bool FreeLibrary(IntPtr hModule);

        private void free()
        {
            RS_ERROR error;
            error = m_shutdown();
            if (error != RS_ERROR.RS_ERROR_SUCCESS)
                Debug.LogError(string.Format("Failed to shutdown: {0}", error));

            if (d3RenderStreamDLL != IntPtr.Zero)
                FreeLibrary(d3RenderStreamDLL);
        }

        public bool IsAvailable
        {
            get
            {
                UnityEngine.Rendering.GraphicsDeviceType gapi = UnityEngine.SystemInfo.graphicsDeviceType;
                return functionsLoaded && (gapi == UnityEngine.Rendering.GraphicsDeviceType.Direct3D11);
            }
        }
#else
        private void free() {}
        public bool IsAvailable { get { return false; } }
#endif

        const int LOAD_IGNORE_CODE_AUTHZ_LEVEL = 0x00000010;
        const int LOAD_LIBRARY_SEARCH_DLL_LOAD_DIR = 0x00000100;
        const int LOAD_LIBRARY_SEARCH_APPLICATION_DIR = 0x00000200;
        const int LOAD_LIBRARY_SEARCH_USER_DIRS = 0x00000400;
        const int LOAD_LIBRARY_SEARCH_SYSTEM32 = 0x00000800;

        const string _dllName = "d3renderstream";

        const int RENDER_STREAM_VERSION_MAJOR = 1;
        const int RENDER_STREAM_VERSION_MINOR = 25;

        bool functionsLoaded = false;
        IntPtr d3RenderStreamDLL = IntPtr.Zero;
        GCHandle handleReference; // Everything is run under coroutines with odd lifetimes, so store a reference to GCHandle

        string name;

        // https://answers.unity.com/questions/16804/retrieving-project-name.html?childToView=478633#answer-478633
        public string GetProjectName()
        {
            string[] s = Application.dataPath.Split('/');
            if (s.Length >= 2)
            {
                string projectName = s[s.Length - 2];
                return projectName;
            }
            return "UNKNOWN UNITY PROJECT";
        }

        private PluginEntry()
        {
#if PLUGIN_AVAILABLE
            RegistryKey d3Key = Registry.CurrentUser.OpenSubKey("Software");
            if (d3Key != null)
            {
                d3Key = d3Key.OpenSubKey("d3 Technologies");
                if (d3Key != null)
                {
                    d3Key = d3Key.OpenSubKey("d3 Production Suite");
                }
            }

            if (d3Key == null)
            {
                Debug.LogError(string.Format("Failed to find path to {0}.dll. d3 Not installed?", _dllName));
                return;
            }

            string d3ExePath = d3Key.GetValue("exe path").ToString();
            d3ExePath = d3ExePath.Replace(@"\\", @"\");
            int endSeparator = d3ExePath.LastIndexOf(Path.DirectorySeparatorChar);
            if (endSeparator != d3ExePath.Length - 1)
                d3ExePath = d3ExePath.Substring(0, endSeparator + 1);

            string libPath = d3ExePath + _dllName + ".dll";
            d3RenderStreamDLL = LoadWin32Library(libPath);
            if (d3RenderStreamDLL == IntPtr.Zero)
            {
                Debug.LogError(string.Format("Failed to load {0}.dll from {1}", _dllName, d3ExePath));
                return;
            }

            m_registerLoggingFunc = DelegateBuilder<pRegisterLoggingFunc>(d3RenderStreamDLL, "rs_registerLoggingFunc");
            m_registerErrorLoggingFunc = DelegateBuilder<pRegisterErrorLoggingFunc>(d3RenderStreamDLL, "rs_registerErrorLoggingFunc");
            m_registerVerboseLoggingFunc = DelegateBuilder<pRegisterVerboseLoggingFunc>(d3RenderStreamDLL, "rs_registerVerboseLoggingFunc");

            m_unregisterLoggingFunc = DelegateBuilder<pUnregisterLoggingFunc>(d3RenderStreamDLL, "rs_unregisterLoggingFunc");
            m_unregisterErrorLoggingFunc = DelegateBuilder<pUnregisterErrorLoggingFunc>(d3RenderStreamDLL, "rs_unregisterErrorLoggingFunc");
            m_unregisterVerboseLoggingFunc = DelegateBuilder<pUnregisterVerboseLoggingFunc>(d3RenderStreamDLL, "rs_unregisterVerboseLoggingFunc");

            m_initialise = DelegateBuilder<pInitialise>(d3RenderStreamDLL, "rs_initialise");
            m_shutdown = DelegateBuilder<pShutdown>(d3RenderStreamDLL, "rs_shutdown");

            m_saveSchema = DelegateBuilder<pSaveSchema>(d3RenderStreamDLL, "rs_saveSchema");
            m_loadSchema = DelegateBuilder<pLoadSchema>(d3RenderStreamDLL, "rs_loadSchema");

            m_setSchema = DelegateBuilder<pSetSchema>(d3RenderStreamDLL, "rs_setSchema");

            m_getStreams = DelegateBuilder<pGetStreams>(d3RenderStreamDLL, "rs_getStreams");

            m_awaitFrameData = DelegateBuilder<pAwaitFrameData>(d3RenderStreamDLL, "rs_awaitFrameData");
            m_setFollower = DelegateBuilder<pSetFollower>(d3RenderStreamDLL, "rs_setFollower");
            m_beginFollowerFrame = DelegateBuilder<pBeginFollowerFrame>(d3RenderStreamDLL, "rs_beginFollowerFrame");
            
            m_sendFrame = DelegateBuilder<pSendFrame>(d3RenderStreamDLL, "rs_sendFrame");
            m_getFrameParameters = DelegateBuilder<pGetFrameParameters>(d3RenderStreamDLL, "rs_getFrameParameters");
            m_getFrameCamera = DelegateBuilder<pGetFrameCamera>(d3RenderStreamDLL, "rs_getFrameCamera");

            m_logToD3 = DelegateBuilder<pLogToD3>(d3RenderStreamDLL, "rs_logToD3");
            m_sendProfilingData = DelegateBuilder<pSendProfilingData>(d3RenderStreamDLL, "rs_sendProfilingData");
            m_setNewStatusMessage = DelegateBuilder<pSetNewStatusMessage>(d3RenderStreamDLL, "rs_setNewStatusMessage");

            if (m_initialise == null || m_shutdown == null || 
                m_saveSchema == null || m_loadSchema == null || 
                m_setSchema == null || m_getStreams == null || 
                m_sendFrame == null || m_awaitFrameData == null ||
                m_setFollower == null || m_beginFollowerFrame == null ||
                m_getFrameParameters == null || m_getFrameCamera == null ||
                m_logToD3 == null || m_sendProfilingData == null || m_setNewStatusMessage == null)
            {
                Debug.LogError(string.Format("One or more functions failed load from {0}.dll", _dllName));
                return;
            }

            functionsLoaded = true;

            if (m_registerLoggingFunc != null)
                m_registerLoggingFunc(logInfo);
            if (m_registerErrorLoggingFunc != null)
                m_registerErrorLoggingFunc(logError);

            if (m_logToD3 != null)
                 Application.logMessageReceivedThreaded += logToD3;

            RS_ERROR error = m_initialise(RENDER_STREAM_VERSION_MAJOR, RENDER_STREAM_VERSION_MINOR);
            if (error == RS_ERROR.RS_ERROR_INCOMPATIBLE_VERSION)
                Debug.LogError(string.Format("Unsupported RenderStream library, expected version {0}.{1}", RENDER_STREAM_VERSION_MAJOR, RENDER_STREAM_VERSION_MINOR));
            else if (error != RS_ERROR.RS_ERROR_SUCCESS)
                Debug.LogError(string.Format("Failed to initialise: {0}", error));

            name = GetProjectName();
#else
            Debug.LogError(string.Format("{0}.dll is only available on Windows", _dllName));
#endif
        }

        ~PluginEntry()
        {
            if (m_logToD3 != null)
                Application.logMessageReceivedThreaded -= logToD3;

            if (m_unregisterErrorLoggingFunc != null)
                m_unregisterErrorLoggingFunc();
            if (m_unregisterLoggingFunc != null)
                m_unregisterLoggingFunc();

            if (handleReference.IsAllocated)
                handleReference.Free();
            free();
        }

        static IntPtr LoadWin32Library(string dllFilePath)
        {
            System.IntPtr moduleHandle = IntPtr.Zero ;
#if PLUGIN_AVAILABLE
            moduleHandle = LoadLibraryEx(dllFilePath, IntPtr.Zero, LOAD_IGNORE_CODE_AUTHZ_LEVEL | LOAD_LIBRARY_SEARCH_DLL_LOAD_DIR | LOAD_LIBRARY_SEARCH_APPLICATION_DIR | LOAD_LIBRARY_SEARCH_SYSTEM32 | LOAD_LIBRARY_SEARCH_USER_DIRS);
            if (moduleHandle == IntPtr.Zero)
            {
                // I'm gettin last dll error
                int errorCode = Marshal.GetLastWin32Error();
                Debug.LogError(string.Format("There was an error during dll loading : {0}, error - {1}", dllFilePath, errorCode));
            }
#endif
            return moduleHandle;
        }

        static T DelegateBuilder<T>(IntPtr loadedDLL, string functionName) where T : Delegate
        {
            IntPtr pAddressOfFunctionToCall = IntPtr.Zero;
#if PLUGIN_AVAILABLE
            pAddressOfFunctionToCall = GetProcAddress(loadedDLL, functionName);
            if (pAddressOfFunctionToCall == IntPtr.Zero)
            {
                return null;
            }
#endif
            T functionDelegate = Marshal.GetDelegateForFunctionPointer(pAddressOfFunctionToCall, typeof(T)) as T;
            return functionDelegate;
        }
    }

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

            RenderTextureDescriptor desc = new RenderTextureDescriptor(m_width, m_height, RenderTextureFormat.ARGBFloat, 24);
            m_sourceTex = new RenderTexture(desc)
            {
                name = m_name + " Texture"
            };
            Cam.targetTexture = m_sourceTex;
            m_convertedTex = new Texture2D(m_sourceTex.width, m_sourceTex.height, TextureFormat.RGBAFloat, false, false);

            Debug.Log(string.Format("Created stream {0} with handle {1}", m_name, m_streamHandle));
        }

        public bool GetCameraData(ref CameraData cameraData)
        {
            return PluginEntry.instance.getFrameCamera(m_streamHandle, ref cameraData) == RS_ERROR.RS_ERROR_SUCCESS;
        }

        public void SendFrame(FrameData frameData, CameraData cameraData)
        {
            if (m_lastFrameCount == Time.frameCount)
                return;

            m_lastFrameCount = Time.frameCount;

            if (m_convertedTex.width != m_sourceTex.width || m_convertedTex.height != m_sourceTex.height)
                m_convertedTex.Resize(m_sourceTex.width, m_sourceTex.height, TextureFormat.RGBAFloat, false);

            RenderTexture unflipped = RenderTexture.GetTemporary(m_sourceTex.width, m_sourceTex.height, 0, m_sourceTex.format);
            Graphics.Blit(m_sourceTex, unflipped, new Vector2(1.0f, -1.0f), new Vector2(0.0f, 1.0f));
            Graphics.CopyTexture(unflipped, m_convertedTex);
            RenderTexture.ReleaseTemporary(unflipped);

            CameraResponseData responseData = new CameraResponseData();
            responseData.tTracked = frameData.tTracked;
            responseData.camera = cameraData;

            unsafe
            {
                SenderFrameTypeData data = new SenderFrameTypeData();                
                data.dx11_resource = m_convertedTex.GetNativeTexturePtr();
                RS_ERROR error = PluginEntry.instance.sendFrame(m_streamHandle, SenderFrameType.RS_FRAMETYPE_DX11_TEXTURE, data, responseData);
                if (error != RS_ERROR.RS_ERROR_SUCCESS)
                    Debug.LogError(string.Format("Error sending frame: {0}", error));
            }
        }

        public void DestroyStream()
        {
            m_streamHandle = 0;
        }

        public Camera Cam { get; set; }

        private RenderTexture m_sourceTex;

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
    }

}
