using System;
using System.Reflection;

namespace Disguise.RenderStream
{
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
            var setter = DynamicSetterCache<T>.GetSetter(info);
            setter.Invoke(target, value);
#endif
        }
    }
}
