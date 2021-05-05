using UnityEngine;

using System;
using System.IO;

class DisguiseRenderStreamSettings : ScriptableObject
{
    public enum SceneControl
    {
        Manual,
        Selection
    }

    [SerializeField]
    public SceneControl sceneControl;

#if UNITY_EDITOR
    [UnityEditor.Callbacks.DidReloadScripts]
    private static void OnReloadScripts()
    {
        // Ensure resource is created
        DisguiseRenderStreamSettings.GetOrCreateSettings();
    }
#endif

    public static DisguiseRenderStreamSettings GetOrCreateSettings()
    {
        var settings = Resources.Load<DisguiseRenderStreamSettings>("DisguiseRenderStreamSettings");
        if (settings == null)
        {
            Debug.Log("Using default DisguiseRenderStreamSettings");
            settings = ScriptableObject.CreateInstance<DisguiseRenderStreamSettings>();
            settings.sceneControl = SceneControl.Manual;
#if UNITY_EDITOR
            if (!Directory.Exists("Assets/Resources"))
                Directory.CreateDirectory("Assets/Resources");
            UnityEditor.AssetDatabase.CreateAsset(settings, "Assets/Resources/DisguiseRenderStreamSettings.asset");
            UnityEditor.AssetDatabase.SaveAssets();
#endif
        }
        return settings;
    }
}
