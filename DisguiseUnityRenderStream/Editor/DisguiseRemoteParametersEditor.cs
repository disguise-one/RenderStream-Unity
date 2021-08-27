    using UnityEngine;
    using UnityEditor;
    using UnityEditorInternal;
    using Disguise.RenderStream;

    [CustomEditor(typeof(DisguiseRemoteParameters))]
    public class DisguiseRemoteParametersEditor : Editor
    {
        private SerializedProperty _fieldsProp;

        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();

            serializedObject.Update();

            ReorderableListUtility.DoLayoutListWithFoldout(list);

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Select All"))
            {
                for (int i = 0; i < _fieldsProp.arraySize; i++)
			    {
                    var element = _fieldsProp.GetArrayElementAtIndex(i);
                    element.FindPropertyRelative("exposed").boolValue = true;
                }
            }
            if (GUILayout.Button("Select None"))
            {
                for (int i = 0; i < _fieldsProp.arraySize; i++)
                {
                    var element = _fieldsProp.GetArrayElementAtIndex(i);
                    element.FindPropertyRelative("exposed").boolValue = false;
                }
            }
            GUILayout.EndHorizontal();

            serializedObject.ApplyModifiedProperties();
        }

        private void OnEnable()
        {
            _fieldsProp = this.serializedObject.FindProperty("fields");
            list = ReorderableListUtility.CreateAutoLayout(_fieldsProp, true, true, false, false);
        }

        private ReorderableList list;
    }