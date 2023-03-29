using UnityEngine;

using System;
using System.IO;

class DisguiseRenderStreamSettings : ScriptableObject
{
    public enum SceneControl
    {
        /// <summary>
        /// Restricts the disguise software's control of scenes and instead merges all channels and remote parameters into a single scene.
        /// </summary>
        Manual,
        
        /// <summary>
        /// Allows scenes to be controlled from inside the disguise software; channels are merged into a single list (duplicates removed) and remote parameters are per-scene.
        /// </summary>
        Selection
    }

    /// <summary>
    /// The Disguise software's behavior for controlling scenes.
    /// </summary>
    [Tooltip("Manual: Restricts the disguise software's control of scenes and instead merges all channels and remote parameters into a single scene.\n" +
             "Selection: Allows scenes to be controlled from inside the disguise software; channels are merged into a single list (duplicates removed) and remote parameters are per-scene.")]
    [SerializeField]
    public SceneControl sceneControl = SceneControl.Manual;
    
    /// <summary>
    /// When true, the Unity window will be able to display streams or live textures to the local screen.
    /// The generated schema will include remote parameters to select the texture to display and how to resize it to fit the screen.
    /// </summary>
    [Tooltip("When true, the Unity window will be able to display streams or live textures to the local screen.\n" +
             "The generated schema will include remote parameters to select the texture to display and how to resize it to fit the screen.")]
    [SerializeField]
    public bool exposePresenter = true;

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
