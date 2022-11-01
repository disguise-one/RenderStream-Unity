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

    static DisguiseRenderStreamSettings s_CachedSettings;
    
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
        if (s_CachedSettings == null)
        {
            s_CachedSettings = Resources.Load<DisguiseRenderStreamSettings>("DisguiseRenderStreamSettings");
        }
        
        if (s_CachedSettings == null)
        {
            Debug.Log("Using default DisguiseRenderStreamSettings");
            s_CachedSettings = ScriptableObject.CreateInstance<DisguiseRenderStreamSettings>();
            s_CachedSettings.sceneControl = SceneControl.Manual;
#if UNITY_EDITOR
            if (!Directory.Exists("Assets/Resources"))
                Directory.CreateDirectory("Assets/Resources");
            UnityEditor.AssetDatabase.CreateAsset(s_CachedSettings, "Assets/Resources/DisguiseRenderStreamSettings.asset");
            UnityEditor.AssetDatabase.SaveAssets();
#endif
        }
        return s_CachedSettings;
    }
}
