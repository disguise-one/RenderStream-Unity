#if UNITY_STANDALONE_WIN && UNITY_64
#define NATIVE_RENDERING_PLUGIN_AVAILABLE
#endif

using System;
using System.Runtime.InteropServices;

namespace Disguise.RenderStream
{
    static class NativeRenderingPlugin
    {
#if NATIVE_RENDERING_PLUGIN_AVAILABLE
        public static bool IsInitialized()
        {
            return NativeRenderingPluginNative.IsInitialized();
        }
        
        public static IntPtr GetD3D12Device()
        {
            if (IsInitialized())
            {
                return NativeRenderingPluginNative.GetD3D12Device();
            }
            return IntPtr.Zero;
        }
        
        public static IntPtr GetD3D12CommandQueue()
        {
            if (IsInitialized())
            {
                return NativeRenderingPluginNative.GetD3D12CommandQueue();
            }
            return IntPtr.Zero;
        }
#else
        public static bool IsInitialized()
        {
            return false;
        }
        
        public static IntPtr GetD3D12Device()
        {
            return IntPtr.Zero;
        }
        
        public static IntPtr GetD3D12CommandQueue()
        {
            return IntPtr.Zero;
        }
#endif
    }
    
#if NATIVE_RENDERING_PLUGIN_AVAILABLE
    static class NativeRenderingPluginNative
    {
        const string PluginName = "NativeRenderingPlugin";
        
        [DllImport(PluginName)]
        public static extern bool IsInitialized();
        
        [DllImport(PluginName)]
        public static extern IntPtr GetD3D12Device();
        
        [DllImport(PluginName)]
        public static extern IntPtr GetD3D12CommandQueue();
    }
#endif
}
