using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Disguise.RenderStream
{
    class DisguiseRenderStreamBuildProcessor  : UnityEditor.Build.IPreprocessBuildWithReport
    {
        static ManagedSchema schema;
        public int callbackOrder
        {
            get { return 0; }
        }

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
            if (!UnityEditor.BuildPipeline.isBuildingPlayer)
                return;

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
    }
}