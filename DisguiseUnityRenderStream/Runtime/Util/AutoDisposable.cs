using System;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Disguise.RenderStream.Utils
{
    public class AutoDisposable : IDisposable
    {
        public AutoDisposable()
        {
            Register();
        }

        void Register()
        {
#if UNITY_EDITOR
            EditorApplication.quitting += Dispose;
            AssemblyReloadEvents.beforeAssemblyReload += Dispose;
#else
            Application.quitting += Dispose;
#endif
        }

        public virtual void Dispose()
        {
        
        }
    }
}
