using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Disguise.RenderStream
{
    public static class DisguisePresenterFactory
    {
        const string k_CreateObjectUndoNameFmt = "Create {0}";

        static string GetUndoName(string objName)
        {
            return string.Format(k_CreateObjectUndoNameFmt, objName);
        }
        
        [MenuItem("GameObject/Disguise/Presenter")]
        public static GameObject CreatePresenter()
        {
            var go = CreatePresenterInternal();
            if (go == null)
                return null;

            StageUtility.PlaceGameObjectInCurrentStage(go);
            Undo.RegisterCreatedObjectUndo(go, GetUndoName(go.name));
            
            GameObjectUtility.EnsureUniqueNameForSibling(go);
            
            Selection.activeObject = go;
            return go;
        }

        static GameObject CreatePresenterInternal()
        {
            var assets = AssetDatabase.FindAssets("DisguisePresenter t:prefab");
            if (assets.Length == 1)
            {
                var guid = assets[0];
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var disguisePresenterPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                var instance = PrefabUtility.InstantiatePrefab(disguisePresenterPrefab) as GameObject;

                instance.name = "Disguise Presenter";

                return instance;
            }

            Debug.LogWarning(assets.Length > 1
                ? "Found multiple conflicting DisguisePresenter.prefab assets when trying to add it to the scene."
                : "Couldn't find the DisguisePresenter.prefab asset to add to the scene.");

            return null;
        }
    }
}
