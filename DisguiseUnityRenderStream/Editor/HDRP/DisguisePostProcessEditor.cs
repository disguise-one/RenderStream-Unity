#if UNITY_PIPELINE_HDRP
using System.Collections;
using System.Collections.Generic;
using System.Reflection.Emit;
using UnityEditor.Rendering;
using UnityEngine;
using UnityEngine.Rendering.HighDefinition;
using UnityEditor;
using UnityEditor.PackageManager;

[VolumeComponentEditor(typeof(DisguiseCameraCaptureAfterPostProcess))]
public sealed class DisguiseCameraCaptureAfterPostProcessEditor : VolumeComponentEditor
{
    static class Labels
    {
        internal static readonly GUIContent Width = new GUIContent("Width");
        internal static readonly GUIContent Height = new GUIContent("Height");
    }

    SerializedDataParameter _width;
    SerializedDataParameter _height;

    public override void OnEnable()
    {
        var o = new PropertyFetcher<DisguiseCameraCaptureAfterPostProcess>(serializedObject);

        _width = Unpack(o.Find(x => x.width));
        _height = Unpack(o.Find(x => x.height));
    }

    public override void OnInspectorGUI()
    {
        EditorGUILayout.LabelField("RenderStream", EditorStyles.miniLabel);
        PropertyField(_width, Labels.Width);
        PropertyField(_height, Labels.Height);
    }
}
#endif // UNITY_PIPELINE_HDRP