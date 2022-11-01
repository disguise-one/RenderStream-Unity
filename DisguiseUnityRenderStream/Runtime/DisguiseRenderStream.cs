using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Unity.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Disguise.RenderStream
{
    partial class DisguiseRenderStream
    {
        [RuntimeInitializeOnLoadMethod]
        static void OnLoad()
        {
            if (Application.isEditor)
            {
                // No play in editor support currently
                return;
            }

            if (PluginEntry.instance.IsAvailable == false)
            {
                Debug.LogError("DisguiseRenderStream: RenderStream DLL not available");
                return;
            }

            string pathToBuiltProject = System.Diagnostics.Process.GetCurrentProcess().MainModule.FileName;
            RS_ERROR error = PluginEntry.instance.loadSchema(pathToBuiltProject, ref schema);
            if (error == RS_ERROR.RS_ERROR_SUCCESS)
            {
                sceneFields = new SceneFields[schema.scenes.Length];
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
                sceneFields = new SceneFields[schema.scenes.Length];
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
            sceneFields[sceneIndex] = new SceneFields{ numerical = new List<ObjectField>(), images = new List<ObjectField>(), texts = new List<ObjectField>() };
            SceneFields fields = sceneFields[sceneIndex];
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

                if (field.FieldType == typeof(Texture))
                    fields.images.Add(field);
                else if (field.FieldType == typeof(String) || field.FieldType == typeof(String[]))
                    fields.texts.Add(field);
                else
                    fields.numerical.Add(field);
            }
        }

        static void CreateStreams()
        {
            if (PluginEntry.instance.IsAvailable == false)
            {
                Debug.LogError("DisguiseRenderStream: RenderStream DLL not available");
                return;
            }

            do
            {
                RS_ERROR error = PluginEntry.instance.getStreams(ref streams);
                if (error != RS_ERROR.RS_ERROR_SUCCESS)
                {
                    Debug.LogError(string.Format("DisguiseRenderStream: Failed to get streams {0}", error));
                    return;
                }

                if (streams.Length == 0)
                {
                    Debug.Log("Waiting for streams...");
                    Thread.Sleep(1000);
                }
            } while (streams.Length == 0);

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
// Blocks HDRP streams in r18.2
// #if UNITY_PIPELINE_HDRP
//             Volume volume = cameraObject.GetComponent<Volume>();
//             if (volume == null)
//                 volume = cameraObject.AddComponent<Volume>();
//             volume.profile = ScriptableObject.CreateInstance<VolumeProfile>();
//             var captureAfterPostProcess = volume.profile.Add<DisguiseCameraCaptureAfterPostProcess>(true);
//             captureAfterPostProcess.width.value = (Int32)stream.width;
//             captureAfterPostProcess.height.value = (Int32)stream.height;
// #endif
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
    
        static readonly List<Texture2D> k_ScratchTextures = new ();
    
        static void ProcessFrameData(in FrameData receivedFrameData)
        {
            if (receivedFrameData.scene >= schema.scenes.Length) return;
        
            ManagedRemoteParameters spec = schema.scenes[receivedFrameData.scene];
            SceneFields fields = sceneFields[receivedFrameData.scene];
            int nNumericalParameters = 0;
            int nImageParameters = 0;
            int nTextParameters = 0;
            for (int i = 0; i < spec.parameters.Length; ++i)
            {
                if (spec.parameters[i].type == RemoteParameterType.RS_PARAMETER_NUMBER)
                    ++nNumericalParameters;
                else if (spec.parameters[i].type == RemoteParameterType.RS_PARAMETER_IMAGE)
                    ++nImageParameters;
                else if (spec.parameters[i].type == RemoteParameterType.RS_PARAMETER_POSE || spec.parameters[i].type == RemoteParameterType.RS_PARAMETER_TRANSFORM)
                    nNumericalParameters += 16;
                else if (spec.parameters[i].type == RemoteParameterType.RS_PARAMETER_TEXT)
                    ++nTextParameters;
            }

            var parameters = new NativeArray<float>(nNumericalParameters, Allocator.Temp);
            var imageData = new NativeArray<ImageFrameData>(nImageParameters, Allocator.Temp);
            if (PluginEntry.instance.GetFrameParameters(spec.hash, ref parameters) == RS_ERROR.RS_ERROR_SUCCESS && PluginEntry.instance.GetFrameImageData(spec.hash, ref imageData) == RS_ERROR.RS_ERROR_SUCCESS)
            {
                if (fields.numerical != null)
                {
                    int i = 0;
                    foreach (var field in fields.numerical)
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
                        else if (fieldType == typeof(Transform))
                        {
                            Matrix4x4 m = new Matrix4x4();
                            m.SetColumn(0, new Vector4(parameters[i + 0], parameters[i + 1], parameters[i + 2], parameters[i + 3]));
                            m.SetColumn(1, new Vector4(parameters[i + 4], parameters[i + 5], parameters[i + 6], parameters[i + 7]));
                            m.SetColumn(2, new Vector4(parameters[i + 8], parameters[i + 9], parameters[i + 10], parameters[i + 11]));
                            m.SetColumn(3, new Vector4(parameters[i + 12], parameters[i + 13], parameters[i + 14], parameters[i + 15]));
                            Transform transform = field.GetValue() as Transform;
                            transform.localPosition = new Vector3(m[0, 3], m[1, 3], m[2, 3]);
                            transform.localScale = m.lossyScale;
                            transform.localRotation = m.rotation;
                            i += 16;
                        }
                        else if (fieldType == typeof(float))
                        {
                            field.SetValue(parameters[i]);
                            ++i;
                        }
                        else
                        {
                            if (field.info != null)
                                field.SetValue(Convert.ChangeType(parameters[i], fieldType));
                            ++i;
                        }
                    }
                }

                if (fields.images != null)
                {
                    while (k_ScratchTextures.Count < imageData.Length)
                    {
                        int index = k_ScratchTextures.Count;
                        k_ScratchTextures.Add(new Texture2D((int)imageData[index].width, (int)imageData[index].height, PluginEntry.ToTextureFormat(imageData[index].format), false, true));
                    }

                    int i = 0;
                    foreach (var field in fields.images)
                    {
                        if (field.GetValue() is RenderTexture renderTexture)
                        {
                            Texture2D texture = k_ScratchTextures[i];
                            if (texture.width != imageData[i].width || texture.height != imageData[i].height ||
                                texture.format != PluginEntry.ToTextureFormat(imageData[i].format))
                            {
                                k_ScratchTextures[i] = new Texture2D((int)imageData[i].width,
                                    (int)imageData[i].height, PluginEntry.ToTextureFormat(imageData[i].format), false,
                                    true);
                                texture = k_ScratchTextures[i];
                            }

                            if (PluginEntry.instance.getFrameImage(imageData[i].imageId, ref texture) == RS_ERROR.RS_ERROR_SUCCESS)
                            {
                                texture.IncrementUpdateCount();
                                Graphics.Blit(texture, renderTexture, new Vector2(1.0f, -1.0f), new Vector2(0.0f, 1.0f));
                                renderTexture.IncrementUpdateCount();
                            }
                        }

                        ++i;
                    }
                }

                if (fields.texts != null)
                {
                    uint i = 0;
                    foreach (var field in fields.texts)
                    {
                        string text = "";
                        if (PluginEntry.instance.getFrameText(spec.hash, i, ref text) == RS_ERROR.RS_ERROR_SUCCESS)
                        {
                            if (field.FieldType == typeof(String[]))
                                field.SetValue(text.Split(' '));
                            else
                                field.SetValue(text);
                        }
                    }

                    ++i;
                }
            }

            parameters.Dispose();
            imageData.Dispose();
        }
    
        public static IEnumerator AwaitFrame()
        {
            if (awaiting)
                yield break;
            awaiting = true;
            
            var waitForEndOfFrame = new WaitForEndOfFrame();
            
            DisguiseRenderStreamSettings settings = DisguiseRenderStreamSettings.GetOrCreateSettings();
            while (true)
            {
                yield return waitForEndOfFrame;
                RS_ERROR error = PluginEntry.instance.awaitFrameData(500, ref frameData);
                if (error == RS_ERROR.RS_ERROR_QUIT)
                    Application.Quit();
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
                if (newFrameData)
                {
                    ProcessFrameData(frameData);
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

        public struct SceneFields
        {
            public List<ObjectField> numerical;
            public List<ObjectField> images;
            public List<ObjectField> texts;
        }
        static private SceneFields[] sceneFields = new SceneFields[0];
    }
}