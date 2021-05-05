using UnityEditor;
using UnityEditorInternal;
using Disguise.RenderStream;

[CustomEditor(typeof(DisguiseRemoteParameters))]
public class DisguiseRemoteParametersEditor : Editor
{
    public override void OnInspectorGUI()
    {
        base.OnInspectorGUI();

        serializedObject.Update();

        ReorderableListUtility.DoLayoutListWithFoldout(list);

        serializedObject.ApplyModifiedProperties();
    }

    private void OnEnable()
    {
        SerializedProperty property = this.serializedObject.FindProperty("fields");
        list = ReorderableListUtility.CreateAutoLayout(property);
    }

    private ReorderableList list;
}