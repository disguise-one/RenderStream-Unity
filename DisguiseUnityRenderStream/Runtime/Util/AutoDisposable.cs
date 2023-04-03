#if UNITY_EDITOR
using UnityEditor;
#else
using UnityEngine;
#endif

namespace Disguise.RenderStream.Utils
{
    abstract class AutoDisposable
    {
        protected AutoDisposable()
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

        protected abstract void Dispose();
    }
}
