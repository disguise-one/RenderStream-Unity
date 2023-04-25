using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Disguise.RenderStream
{
    [CustomEditor(typeof(DisguiseRenderStreamSettings))]
    class DisguiseRenderStreamSettingsEditor : Editor
    {
        public static class Style
        {
            public const string DisguiseSettingsContainer = "disguise-settings-container";
            public const string DisguiseSettingsTitle = "disguise-settings-title";
        }
        
        [SerializeField]
        VisualTreeAsset m_Layout;
        
        [SerializeField]
        StyleSheet m_Style;
        
        public override VisualElement CreateInspectorGUI()
        {
            var root = new VisualElement();
            
            m_Layout.CloneTree(root);
            root.styleSheets.Add(m_Style);
            
            root.Bind(serializedObject);

            return root;
        }
    }
}
