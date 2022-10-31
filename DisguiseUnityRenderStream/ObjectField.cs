using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;

namespace Disguise.RenderStream
{
    /// <summary>
    /// Contains a dynamically-generated collection of methods to set a property or field
    /// of a given type.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <remarks>
    /// This class can be used to avoid boxing operations for calling property or field
    /// setters when the parameter type is defined statically.
    /// </remarks>
    static class DynamicSetterCache<T>
    {
        static readonly Dictionary<MemberInfo, Action<object, T>> k_Cache = new();

        public static Action<object, T> GetSetter(MemberInfo member)
        {
            var newMethod = new DynamicMethod($"{typeof(T).FullName}.argument_set_{member.Name}",
                returnType: null,
                parameterTypes: new[]
                {
                    typeof(object),
                    typeof(T)
                });
            var gen = newMethod.GetILGenerator();
            
            if (!k_Cache.TryGetValue(member, out var setter))
            {
                switch (member)
                {
                    case FieldInfo field:
                    {
                        gen.Emit(OpCodes.Ldarg_0);
                        gen.Emit(OpCodes.Ldarg_1);
                        gen.Emit(OpCodes.Stfld, field);
                        gen.Emit(OpCodes.Ret);

                        break;
                    }
                    case PropertyInfo property:
                    {
                        gen.Emit(OpCodes.Ldarg_0);
                        gen.Emit(OpCodes.Ldarg_1);
                        gen.Emit(OpCodes.Call, property.GetSetMethod());
                        gen.Emit(OpCodes.Ret);

                        break;
                    }
                    default:
                        throw new ArgumentOutOfRangeException(nameof(member), member, null);
                }

                setter = (Action<object, T>)newMethod.CreateDelegate(typeof(Action<object, T>));
                k_Cache.Add(member, setter);
            }

            return setter;
        }
    }

    class ObjectField
    {
        public object target;
        public MemberInfo info;

        public Type FieldType =>
            info switch
            {
                FieldInfo fieldInfo => fieldInfo.FieldType,
                PropertyInfo propertyInfo => propertyInfo.PropertyType,
                _ => typeof(void)
            };

        public void SetValue(object value)
        {
            switch (info)
            {
                case FieldInfo fieldInfo:
                    fieldInfo.SetValue(target, value);
                    break;
                case PropertyInfo propertyInfo:
                    propertyInfo.SetValue(target, value);
                    break;
            }
        }

        public object GetValue()
        {
            return info switch
            {
                FieldInfo fieldInfo => fieldInfo.GetValue(target),
                PropertyInfo propertyInfo => propertyInfo.GetValue(target),
                _ => null
            };
        }

        public void SetValue<T>(T value)
        {
#if ENABLE_IL2CPP
            return SetValue((object)value);
#else
            var getter = DynamicSetterCache<T>.GetSetter(info);
            getter.Invoke(target, value);
#endif
        }
    }
}
