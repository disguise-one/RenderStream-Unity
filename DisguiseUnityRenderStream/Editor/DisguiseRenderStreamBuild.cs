using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Disguise.RenderStream
{
    class DisguiseRenderStreamBuildProcessor  : UnityEditor.Build.IPostprocessBuildWithReport
    {
        public int callbackOrder { get; set; } = 0;
        
        public void OnPostprocessBuild(BuildReport report)
        {
            var target = report.summary.platform;
            
            if (target != BuildTarget.StandaloneWindows64)
            {
                Debug.LogError("DisguiseRenderStream: RenderStream is only available for 64-bit Windows (x86_64).");
                return;
            }

            if (PluginEntry.instance.IsAvailable == false)
            {
                Debug.LogError("DisguiseRenderStream: RenderStream DLL not available, could not save schema");
                return;
            }

            CheckVsync();
            
            DisguiseRenderStreamSettings settings = DisguiseRenderStreamSettings.GetOrCreateSettings();
            var schema = new ManagedSchema
            {
                channels = Array.Empty<string>()
            };

            var allScenesInBuild = EditorBuildSettings.scenes;
            
            switch (settings.sceneControl)
            {
                case DisguiseRenderStreamSettings.SceneControl.Selection:
                    Debug.Log("Generating scene-selection schema for: " + allScenesInBuild.Length + " scenes");
                    schema.scenes = new ManagedRemoteParameters[allScenesInBuild.Length];
                    if (allScenesInBuild.Length == 0)
                        Debug.LogWarning("No scenes in build settings. Schema will be empty.");
                    break;
                case DisguiseRenderStreamSettings.SceneControl.Manual:
                default:
                    Debug.Log("Generating manual schema");
                    schema.scenes = new ManagedRemoteParameters[1];
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

                // In "Manual" mode (Disguise does not control scene changes), all parameters are listed under a single "Default" scene.
                // In "Selection" mode (Disguise controls scene changes), parameters are listed under their respective scenes. 
                var (sceneIndex, managedName, indexMessage) = settings.sceneControl switch {
                    DisguiseRenderStreamSettings.SceneControl.Manual => (0, "Default", string.Empty),
                    DisguiseRenderStreamSettings.SceneControl.Selection => (scene.buildIndex, scene.name, $"({scene.buildIndex}/{allScenesInBuild.Length})"),
                    _ => throw new ArgumentOutOfRangeException()
                };
                
                Debug.Log($"Processing scene {scene.name} {indexMessage}");
                AddSceneToSchema(schema, sceneIndex, managedName);
            }

            var pathToBuiltProject = report.summary.outputPath;
            RS_ERROR error = PluginEntry.instance.saveSchema(pathToBuiltProject, ref schema);
            if (error != RS_ERROR.RS_ERROR_SUCCESS)
            {
                Debug.LogError(string.Format("DisguiseRenderStream: Failed to save schema {0}", error));
            }
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

        static void CheckVsync()
        {
            if (DisguiseFramerateManager.Enabled && DisguiseFramerateManager.VSyncIsEnabled)
            {
                Debug.LogWarning($"DisguiseRenderStream: {nameof(QualitySettings)}{nameof(QualitySettings.vSyncCount)} is currently enabled. For best performance disable vSync in the project settings.");
            }
        }
    }
}