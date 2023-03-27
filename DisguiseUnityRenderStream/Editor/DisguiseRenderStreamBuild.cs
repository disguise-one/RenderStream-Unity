using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEditor.UnityLinker;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Disguise.RenderStream
{
    /// <summary>
    /// Build callback order:
    /// 1. IPreprocessBuildWithReport
    /// 2. IUnityLinkerProcessor (only when managed code stripping is enabled, for <see cref="ReflectedMemberPreserver"/>)
    /// 3. IPostprocessBuildWithReport
    /// </summary>
    class DisguiseRenderStreamBuild :
        IPreprocessBuildWithReport,
        IUnityLinkerProcessor,
        IPostprocessBuildWithReport
    {
        public int callbackOrder { get; set; } = 0;

        bool m_NeededVSyncFix;
        bool m_HasGeneratedSchema;
        ManagedSchema m_Schema;
        int m_NumScenesInBuild;
        
        void IPreprocessBuildWithReport.OnPreprocessBuild(BuildReport report)
        {
            m_NeededVSyncFix = DisguiseFramerateManager.VSyncIsEnabled;
            m_HasGeneratedSchema = false;
            
            if (m_NeededVSyncFix)
            {
                QualitySettings.vSyncCount = 0;
            }
            
            AddAlwaysIncludedShader(BlitExtended.ShaderName);
            AddAlwaysIncludedShader(DepthCopy.ShaderName);
            
            var target = report.summary.platform;
            
            if (target != BuildTarget.StandaloneWindows64)
            {
                throw new BuildFailedException("DisguiseRenderStream: RenderStream is only available for 64-bit Windows (x86_64).");
            }

            if (PluginEntry.instance.IsAvailable == false)
            {
                throw new BuildFailedException("DisguiseRenderStream: RenderStream DLL not available, could not save schema");
            }
        }
        
        string IUnityLinkerProcessor.GenerateAdditionalLinkXmlFile(BuildReport report, UnityLinkerBuildPipelineData data)
        {
            var preserver = new ReflectedMemberPreserver();
            
            GenerateSchema(report, scene =>
            {
                var remoteParameters = Object.FindObjectsByType<DisguiseRemoteParameters>(FindObjectsSortMode.InstanceID);

                foreach (var remoteParameter in remoteParameters)
                {
                    foreach (var exposedParameter in remoteParameter.exposedParameters())
                    {
                        var memberInfo = remoteParameter.GetMemberInfoFromManagedParameter(exposedParameter);
                        preserver.Preserve(memberInfo);
                    }
                }
                
                ProcessSceneForSchema(scene);
            });
            
            m_HasGeneratedSchema = true;

            return preserver.GenerateAdditionalLinkXmlFile();
        }
        
        void IPostprocessBuildWithReport.OnPostprocessBuild(BuildReport report)
        {
            if (m_NeededVSyncFix)
            {
                Debug.LogWarning($"DisguiseRenderStream: {nameof(QualitySettings)}.{nameof(QualitySettings.vSyncCount)} has been disabled for best output performance with Disguise");
            }
            
            if (m_HasGeneratedSchema)
                return;
            
            GenerateSchema(report, ProcessSceneForSchema);
        }

        void GenerateSchema(BuildReport report, Action<Scene> processScene)
        {
            var settings = DisguiseRenderStreamSettings.GetOrCreateSettings();
            m_Schema = new ManagedSchema
            {
                channels = Array.Empty<string>()
            };

            var allScenesInBuild = EditorBuildSettings.scenes;
            m_NumScenesInBuild = allScenesInBuild.Length;
            
            switch (settings.sceneControl)
            {
                case DisguiseRenderStreamSettings.SceneControl.Selection:
                    Debug.Log("Generating scene-selection schema for: " + allScenesInBuild.Length + " scenes");
                    m_Schema.scenes = new ManagedRemoteParameters[allScenesInBuild.Length];
                    if (allScenesInBuild.Length == 0)
                        Debug.LogWarning("No scenes in build settings. Schema will be empty.");
                    break;
                case DisguiseRenderStreamSettings.SceneControl.Manual:
                default:
                    Debug.Log("Generating manual schema");
                    m_Schema.scenes = new ManagedRemoteParameters[1];
                    break;
            }
            
            foreach (var buildScene in allScenesInBuild)
            {
                if (!buildScene.enabled)
                {
                    continue;
                }

                var scene = SceneManager.GetSceneByPath(buildScene.path);
                if (!scene.IsValid() || !scene.isLoaded)
                {
                    scene = EditorSceneManager.OpenScene(buildScene.path, OpenSceneMode.Single);
                }

                if (!scene.IsValid() || !scene.isLoaded)
                {
                    continue;
                }

                processScene.Invoke(scene);
            }

            var pathToBuiltProject = report.summary.outputPath;
            RS_ERROR error = PluginEntry.instance.saveSchema(pathToBuiltProject, ref m_Schema);
            if (error != RS_ERROR.RS_ERROR_SUCCESS)
            {
                throw new BuildFailedException(string.Format("DisguiseRenderStream: Failed to save schema {0}", error));
            }
        }

        void ProcessSceneForSchema(Scene scene)
        {
            DisguiseRenderStreamSettings settings = DisguiseRenderStreamSettings.GetOrCreateSettings();
            
            // In "Manual" mode (Disguise does not control scene changes), all parameters are listed under a single "Default" scene.
            // In "Selection" mode (Disguise controls scene changes), parameters are listed under their respective scenes. 
            var (sceneIndex, managedName, indexMessage) = settings.sceneControl switch {
                DisguiseRenderStreamSettings.SceneControl.Manual => (0, "Default", string.Empty),
                DisguiseRenderStreamSettings.SceneControl.Selection => (scene.buildIndex, scene.name, $"({scene.buildIndex}/{m_NumScenesInBuild})"),
                _ => throw new ArgumentOutOfRangeException()
            };
                
            Debug.Log($"Processing scene {scene.name} {indexMessage}");
            AddSceneToSchema(m_Schema, sceneIndex, managedName);
        }
        
        static void AddSceneToSchema(ManagedSchema schema, int sceneIndex, string name)
        {
            var channels = new HashSet<string>(schema.channels);
            channels.UnionWith(Camera.allCameras.Select(camera => camera.name));
            schema.channels = channels.ToArray();
            schema.scenes[sceneIndex] ??= new ManagedRemoteParameters
            {
                name = name,
                parameters = Array.Empty<ManagedRemoteParameter>()
            };
            var currentScene = schema.scenes[sceneIndex];

            var parameters = currentScene.parameters
                .Concat(Object.FindObjectsByType<DisguiseRemoteParameters>(FindObjectsSortMode.InstanceID)
                .SelectMany(p => p.exposedParameters()));
            
            currentScene.parameters = parameters.ToArray();
        }
        
        /// <summary>
        /// Ensure the proper runtime availability of the shader name.
        /// </summary>
        /// <remarks>
        /// Based on logic exposed here:
        /// https://forum.unity.com/threads/modify-always-included-shaders-with-pre-processor.509479/
        /// </remarks>
        /// <param name="shaderName">The name of the shader to validate.</param>
        static void AddAlwaysIncludedShader(string shaderName)
        {
            var shader = Shader.Find(shaderName);
            if (shader == null)
                return;

            var graphicsSettingsObj = AssetDatabase.LoadAssetAtPath<GraphicsSettings>("ProjectSettings/GraphicsSettings.asset");
            var serializedObject = new SerializedObject(graphicsSettingsObj);
            var arrayProp = serializedObject.FindProperty("m_AlwaysIncludedShaders");
            var hasShader = false;
            for (int i = 0; i < arrayProp.arraySize; ++i)
            {
                var arrayElem = arrayProp.GetArrayElementAtIndex(i);
                if (shader == arrayElem.objectReferenceValue)
                {
                    hasShader = true;
                    break;
                }
            }

            if (!hasShader)
            {
                var arrayIndex = arrayProp.arraySize;
                arrayProp.InsertArrayElementAtIndex(arrayIndex);
                var arrayElem = arrayProp.GetArrayElementAtIndex(arrayIndex);
                arrayElem.objectReferenceValue = shader;

                serializedObject.ApplyModifiedProperties();

                AssetDatabase.SaveAssets();
            }
        }
    }
}