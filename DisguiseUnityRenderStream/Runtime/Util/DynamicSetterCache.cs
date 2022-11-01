using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;

namespace Disguise.RenderStream
{
    /// <summary>
    /// Contains a dynamically-generated collection of delegates to set a property or field
    /// of a given type.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <remarks>
    /// This class can be used to avoid boxing operations for calling property or field
    /// setters when the parameter type is unknown at compile-time, resulting in better
    /// performance.
    /// </remarks>
    static class DynamicSetterCache<T>
    {
        static readonly Dictionary<MemberInfo, Action<object, T>> k_Cache = new();

        public static Action<object, T> GetSetter(MemberInfo member)
        {
            if (!k_Cache.TryGetValue(member, out var setter))
            {
                var newMethod = new DynamicMethod($"{typeof(T).FullName}.argument_set_{member.Name}",
                    returnType: null,
                    parameterTypes: new[]
                    {
                        typeof(object),
                        typeof(T)
                    });
                var gen = newMethod.GetILGenerator();
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
}
