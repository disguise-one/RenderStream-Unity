using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Serialization;

using System;
using System.Reflection;
using System.Linq;
using System.Collections.Generic;
using Disguise.RenderStream;

[AddComponentMenu("Disguise RenderStream/Remote Parameters")]
public class DisguiseRemoteParameters : MonoBehaviour
{
    [SerializeField]
    public UnityEngine.Object exposedObject;
    public string prefix;
    [System.Serializable]
    public struct ExposedField
    {
        public ExposedField(bool exposed_, string fieldName_, string groupName_) 
        {
            exposed = exposed_;
            fieldName = fieldName_;
            groupName = groupName_;
        }
        public bool exposed;
        public string fieldName;
        public string groupName;
    }
    [HideInInspector]
    public List<ExposedField> fields;

    void Reset()
    {
        prefix = Guid.NewGuid().ToString();
    }

#if UNITY_EDITOR
    void OnValidate()
    {
        if (fields == null)
            fields = new List<ExposedField>();
        if (exposedObject == null)
        {
            fields.Clear();
            return;
        }

        HashSet<string> displayNames = new HashSet<string>();
        if (exposedObject is Material material)
        {
            if (material.mainTexture is RenderTexture)
                displayNames.Add("Main texture");
        }
        else if (exposedObject is Transform transform)
        {
            displayNames.Add("Transform");
        }
        UnityEditor.SerializedObject so = new UnityEditor.SerializedObject(exposedObject);
        UnityEditor.SerializedProperty property = so.GetIterator();
        if (property.NextVisible(true))
        {
            do
            {
                displayNames.Add(property.displayName);
            }
            while (property.NextVisible(false));
        }
        fields.RemoveAll(field => !displayNames.Contains(field.fieldName));
        foreach (string displayName in displayNames)
        {
            if (!fields.Any(field => field.fieldName == displayName))
                fields.Add(new ExposedField(true, displayName, ""));
        }
    }

    private ManagedRemoteParameter createField(string group, string displayName_, string key_, RemoteParameterType type, string suffix, string undecoratedSuffix, float min, float max, float step, object defaultValue, string[] options)
    {
        string key = key_ + (String.IsNullOrEmpty(undecoratedSuffix) ? "" : "_" + undecoratedSuffix);
        string displayName = exposedObject.name + " " + displayName_ + (String.IsNullOrEmpty(suffix) ? "" : " " + suffix);
        if (string.IsNullOrEmpty(group)) group = "Properties";

        ManagedRemoteParameter parameter = new ManagedRemoteParameter();
        parameter.group = group;
        parameter.displayName = displayName;
        parameter.key = key;
        parameter.type = type;
        parameter.min = min;
        parameter.max = max;
        parameter.step = step;
        parameter.defaultValue = defaultValue;
        parameter.options = options;
        parameter.dmxOffset = -1;
        parameter.dmxType = RemoteParameterDmxType.RS_DMX_16_BE;
        return parameter;
    }

    public List<ManagedRemoteParameter> exposedParameters()
    {
        List<ManagedRemoteParameter> parameters = new List<ManagedRemoteParameter>();
        if (exposedObject == null)
            return parameters;
        if (exposedObject is Material material)
        {
            if (material.mainTexture is RenderTexture)
            {
                if (fields.Any(field => field.exposed && field.fieldName == "Main texture"))
                {
                    string group = fields.FirstOrDefault(field => field.fieldName == "Main texture").groupName;
                    parameters.Add(createField(group, "Main texture", prefix + " " + "mainTexture", RemoteParameterType.RS_PARAMETER_IMAGE, "", "", 0, 255, 1, 0, new string[0]));
                }
            }
        }
        else if (exposedObject is Transform transform)
        {
            if (System.Object.ReferenceEquals(transform.transform, transform))
            {
                if (fields.Any(field => field.exposed && field.fieldName == "Transform"))
                {
                    string group = fields.FirstOrDefault(field => field.fieldName == "Transform").groupName;
                    parameters.Add(createField(group, "Transform", prefix + " " + "transform", RemoteParameterType.RS_PARAMETER_TRANSFORM, "", "", 0, 255, 1, 0, new string[0]));
                }
            }
        }
        UnityEditor.SerializedObject so = new UnityEditor.SerializedObject(exposedObject);
        UnityEditor.SerializedProperty property = so.GetIterator();
        if (property.NextVisible(true))
        {
            Stack<string> nesting = new Stack<string>();
            int depth = property.depth;
            string previousName = "";
            do
            {
                for (; depth < property.depth; ++depth)
                    nesting.Push(previousName);
                for (; depth > property.depth; --depth)
                    nesting.Pop();
                string propertyPath = property.propertyPath;
                MemberInfo info = GetMemberInfoFromPropertyPath(propertyPath);
                if (info == null && propertyPath.StartsWith("m_"))
                {
                    string modifiedPropertyPath = char.ToLower(propertyPath[2]) + propertyPath.Substring(3);
                    info = GetMemberInfoFromPropertyPath(modifiedPropertyPath);
                    if (info != null)
                        propertyPath = modifiedPropertyPath;
                }
                HeaderAttribute header = info != null ? info.GetCustomAttributes(typeof(HeaderAttribute), true).FirstOrDefault() as HeaderAttribute : null;
                RangeAttribute range = info != null ? info.GetCustomAttributes(typeof(RangeAttribute), true).FirstOrDefault() as RangeAttribute : null;
                MinAttribute min = info != null ? info.GetCustomAttributes(typeof(MinAttribute), true).FirstOrDefault() as MinAttribute : null;
                if (header != null)
                    nesting.Push(header.header);
                string group = String.Join(" ", new Stack<String>(nesting).ToArray());
                if (fields.Any(field => field.exposed && field.fieldName == property.displayName && !string.IsNullOrEmpty(field.groupName)))
                {
                    group = fields.FirstOrDefault(field => field.fieldName == property.displayName).groupName;
                }
                if (fields.Any(field => !field.exposed && field.fieldName == property.displayName))
                {
                    //Debug.Log("Unexposed property: " + property.displayName);
                }
                else if (!property.editable)
                {
                    Debug.Log("Uneditable property: " + property.displayName);
                }
                else if (info == null)
                {
                    Debug.Log("Unreflected property: " + property.propertyPath);
                }
                else if (property.propertyType == UnityEditor.SerializedPropertyType.Float)
                {
                    parameters.Add(createField(group, property.displayName, prefix + " " + propertyPath, RemoteParameterType.RS_PARAMETER_NUMBER, "", "", 
                                               min != null ? min.min : (range != null ? range.min : -1.0f), range != null ? range.max : +1.0f, 0.001f, property.floatValue, new string[0]));
                }
                else if (property.propertyType == UnityEditor.SerializedPropertyType.Integer)
                {
                    parameters.Add(createField(group, property.displayName, prefix + " " + propertyPath, RemoteParameterType.RS_PARAMETER_NUMBER, "", "", 
                                               min != null ? min.min : (range != null ? range.min : -1000), range != null ? range.max : +1000, 1, property.intValue, new string[0]));
                }
                else if (property.propertyType == UnityEditor.SerializedPropertyType.Boolean)
                {
                    parameters.Add(createField(group, property.displayName, prefix + " " + propertyPath, RemoteParameterType.RS_PARAMETER_NUMBER, "", "", 
                                               0, 1, 1, property.boolValue ? 1 : 0, new string[] { "Off", "On" }));
                }
                else if (property.propertyType == UnityEditor.SerializedPropertyType.Enum && property.enumNames.Length > 1)
                {
                    parameters.Add(createField(group, property.displayName, prefix + " " + propertyPath, RemoteParameterType.RS_PARAMETER_NUMBER, "", "", 
                                               0, property.enumNames.Length - 1, 1, property.enumValueIndex, property.enumDisplayNames));
                }
                else if (property.propertyType == UnityEditor.SerializedPropertyType.Vector2)
                {
                    Vector2 v = property.vector2Value;
                    parameters.Add(createField(group, property.displayName, prefix + " " + propertyPath, RemoteParameterType.RS_PARAMETER_NUMBER, "x", "x", 
                                               min != null ? min.min : (range != null ? range.min : -1.0f), range != null ? range.max : +1.0f, 0.001f, v.x, new string[0]));
                    parameters.Add(createField(group, property.displayName, prefix + " " + propertyPath, RemoteParameterType.RS_PARAMETER_NUMBER, "y", "y", 
                                               min != null ? min.min : (range != null ? range.min : -1.0f), range != null ? range.max : +1.0f, 0.001f, v.y, new string[0]));
                }
                else if (property.propertyType == UnityEditor.SerializedPropertyType.Vector2Int)
                {
                    Vector2Int v = property.vector2IntValue;
                    parameters.Add(createField(group, property.displayName, prefix + " " + propertyPath, RemoteParameterType.RS_PARAMETER_NUMBER, "x", "x", 
                                               min != null ? min.min : (range != null ? range.min : -1000), range != null ? range.max : +1000, 0.001f, v.x, new string[0]));
                    parameters.Add(createField(group, property.displayName, prefix + " " + propertyPath, RemoteParameterType.RS_PARAMETER_NUMBER, "y", "y", 
                                               min != null ? min.min : (range != null ? range.min : -1000), range != null ? range.max : +1000, 0.001f, v.y, new string[0]));
                }
                else if (property.propertyType == UnityEditor.SerializedPropertyType.Vector3)
                {
                    Vector3 v = property.vector3Value;
                    parameters.Add(createField(group, property.displayName, prefix + " " + propertyPath, RemoteParameterType.RS_PARAMETER_NUMBER, "x", "x", 
                                               min != null ? min.min : (range != null ? range.min : -1.0f), range != null ? range.max : +1.0f, 0.001f, v.x, new string[0]));
                    parameters.Add(createField(group, property.displayName, prefix + " " + propertyPath, RemoteParameterType.RS_PARAMETER_NUMBER, "y", "y", 
                                               min != null ? min.min : (range != null ? range.min : -1.0f), range != null ? range.max : +1.0f, 0.001f, v.y, new string[0]));
                    parameters.Add(createField(group, property.displayName, prefix + " " + propertyPath, RemoteParameterType.RS_PARAMETER_NUMBER, "z", "z", 
                                               min != null ? min.min : (range != null ? range.min : -1.0f), range != null ? range.max : +1.0f, 0.001f, v.z, new string[0]));
                }
                else if (property.propertyType == UnityEditor.SerializedPropertyType.Vector3Int)
                {
                    Vector3Int v = property.vector3IntValue;
                    parameters.Add(createField(group, property.displayName, prefix + " " + propertyPath, RemoteParameterType.RS_PARAMETER_NUMBER, "x", "x", 
                                               min != null ? min.min : (range != null ? range.min : -1000), range != null ? range.max : +1000, 0.001f, v.x, new string[0]));
                    parameters.Add(createField(group, property.displayName, prefix + " " + propertyPath, RemoteParameterType.RS_PARAMETER_NUMBER, "y", "y", 
                                               min != null ? min.min : (range != null ? range.min : -1000), range != null ? range.max : +1000, 0.001f, v.y, new string[0]));
                    parameters.Add(createField(group, property.displayName, prefix + " " + propertyPath, RemoteParameterType.RS_PARAMETER_NUMBER, "z", "z", 
                                               min != null ? min.min : (range != null ? range.min : -1000), range != null ? range.max : +1000, 0.001f, v.z, new string[0]));
                }
                else if (property.propertyType == UnityEditor.SerializedPropertyType.Vector4)
                {
                    Vector4 v = property.vector4Value;
                    parameters.Add(createField(group, property.displayName, prefix + " " + propertyPath, RemoteParameterType.RS_PARAMETER_NUMBER, "x", "x", 
                                               min != null ? min.min : (range != null ? range.min : -1.0f), range != null ? range.max : +1.0f, 0.001f, v.x, new string[0]));
                    parameters.Add(createField(group, property.displayName, prefix + " " + propertyPath, RemoteParameterType.RS_PARAMETER_NUMBER, "y", "y", 
                                               min != null ? min.min : (range != null ? range.min : -1.0f), range != null ? range.max : +1.0f, 0.001f, v.y, new string[0]));
                    parameters.Add(createField(group, property.displayName, prefix + " " + propertyPath, RemoteParameterType.RS_PARAMETER_NUMBER, "z", "z", 
                                               min != null ? min.min : (range != null ? range.min : -1.0f), range != null ? range.max : +1.0f, 0.001f, v.z, new string[0]));
                    parameters.Add(createField(group, property.displayName, prefix + " " + propertyPath, RemoteParameterType.RS_PARAMETER_NUMBER, "w", "w", 
                                               min != null ? min.min : (range != null ? range.min : -1.0f), range != null ? range.max : +1.0f, 0.001f, v.w, new string[0]));
                }
                else if (property.propertyType == UnityEditor.SerializedPropertyType.Color)
                {
                    Color v = property.colorValue;
                    parameters.Add(createField(group, property.displayName, prefix + " " + propertyPath, RemoteParameterType.RS_PARAMETER_NUMBER, "r", "r", 
                                               min != null ? min.min : (range != null ? range.min : 0.0f), range != null ? range.max : 1.0f, 0.001f, v.r, new string[0]));
                    parameters.Add(createField(group, property.displayName, prefix + " " + propertyPath, RemoteParameterType.RS_PARAMETER_NUMBER, "g", "g", 
                                               min != null ? min.min : (range != null ? range.min : 0.0f), range != null ? range.max : 1.0f, 0.001f, v.g, new string[0]));
                    parameters.Add(createField(group, property.displayName, prefix + " " + propertyPath, RemoteParameterType.RS_PARAMETER_NUMBER, "b", "b", 
                                               min != null ? min.min : (range != null ? range.min : 0.0f), range != null ? range.max : 1.0f, 0.001f, v.b, new string[0]));
                    parameters.Add(createField(group, property.displayName, prefix + " " + propertyPath, RemoteParameterType.RS_PARAMETER_NUMBER, "a", "a", 
                                               min != null ? min.min : (range != null ? range.min : 0.0f), range != null ? range.max : 1.0f, 0.001f, v.a, new string[0]));
                }
                else if (property.propertyType == UnityEditor.SerializedPropertyType.ObjectReference)
                {
                    if (property.objectReferenceValue is RenderTexture texture)
                    {
                        parameters.Add(createField("", property.displayName, prefix + " " + propertyPath, RemoteParameterType.RS_PARAMETER_IMAGE, "", "", 0, 255, 1, 0, new string[0]));
                    }
                    else if (property.objectReferenceValue is Transform transform)
                    {
                        parameters.Add(createField("", property.displayName, prefix + " " + propertyPath, RemoteParameterType.RS_PARAMETER_TRANSFORM, "", "", 0, 255, 1, 0, new string[0]));
                    }
                    else
                    {
                        Debug.Log("Unsupported exposed object: " + property.displayName);
                    }
                }
                else if (property.propertyType == UnityEditor.SerializedPropertyType.ObjectReference)
                {
                    if (property.objectReferenceValue is RenderTexture texture)
                    {
                        parameters.Add(createField("", property.displayName, prefix + " " + propertyPath, RemoteParameterType.RS_PARAMETER_IMAGE, "", "", 0, 255, 1, 0, new string[0]));
                    }
                    else if (property.objectReferenceValue is Transform transform)
                    {
                        parameters.Add(createField("", property.displayName, prefix + " " + propertyPath, RemoteParameterType.RS_PARAMETER_TRANSFORM, "", "", 0, 255, 1, 0, new string[0]));
                    }
                    else
                    {
                        Debug.Log("Unsupported exposed object: " + property.displayName);
                    }
                }
                else if (property.propertyType == UnityEditor.SerializedPropertyType.String)
                {
                    parameters.Add(createField(group, property.displayName, prefix + " " + propertyPath, RemoteParameterType.RS_PARAMETER_TEXT, "", "", 0, 0, 0, property.stringValue, new string[0]));
                }
                else
                {
                    Debug.Log("Unsupported exposed property: " + property.displayName);
                }
                previousName = property.displayName;
                if (header != null)
                    nesting.Pop();
            }
            while(property.NextVisible(false));
        }
        return parameters;
    }
#endif

    public MemberInfo GetMemberInfoFromPropertyPath(string propertyPath)
    {
        if (exposedObject == null)
            return null;
        MemberInfo info = null;
        for (Type currentType = exposedObject.GetType(); info == null && currentType != null; currentType = currentType.BaseType)
        {
            FieldInfo fieldInfo = currentType.GetField(propertyPath, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (fieldInfo != null && (!fieldInfo.IsInitOnly || fieldInfo.FieldType.IsSubclassOf(typeof(UnityEngine.Object))))
            {
                info = (MemberInfo)fieldInfo;
                return info;
            }

        }
        for (Type currentType = exposedObject.GetType(); info == null && currentType != null; currentType = currentType.BaseType)
        {
            PropertyInfo propertyInfo = currentType.GetProperty(propertyPath, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (propertyInfo != null && propertyInfo.CanRead && (propertyInfo.CanWrite || propertyInfo.PropertyType.IsSubclassOf(typeof(UnityEngine.Object))))
            {
                info = (MemberInfo)propertyInfo;
                return info;
            }
        }
        return info;
    }
}
