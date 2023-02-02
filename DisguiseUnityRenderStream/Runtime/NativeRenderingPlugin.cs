#if UNITY_STANDALONE_WIN && UNITY_64
#define NATIVE_RENDERING_PLUGIN_AVAILABLE
#endif

using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Disguise.RenderStream.Utils;
using UnityEngine.PlayerLoop;

namespace Disguise.RenderStream
{
    static class NativeRenderingPlugin
    {
        struct FinishFrameRendering { }
        
        public enum PixelFormat
        {
            Invalid,
            BGRA8,
            BGRX8,
            RGBA32F,
            RGBA16,
            RGBA8,
        }
        
        public enum EventID
        {
            InputImage
        }

        public struct InputImageData
        {
            public IntPtr m_rs_getFrameImage;
            public Int64 m_ImageId;
            public IntPtr m_Texture;
        }

        public static EventDataPool<InputImageData> GetFrameImageDataPool { get; } = new EventDataPool<InputImageData>();

        static NativeRenderingPlugin()
        {
            PlayerLoopExtensions.RegisterUpdate<PostLateUpdate.FinishFrameRendering, FinishFrameRendering>(OnFinishFrameRendering);
        }

        static void OnFinishFrameRendering()
        {
            GetFrameImageDataPool.OnFrameEnd();
        }

#if NATIVE_RENDERING_PLUGIN_AVAILABLE
        public static IntPtr GetRenderEventCallback()
        {
            return NativeRenderingPluginNative.GetRenderEventCallback();
        }
        
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
        
        public static IntPtr CreateNativeTexture(string name, int width, int height, PixelFormat pixelFormat)
        {
            if (IsInitialized())
            {
                return NativeRenderingPluginNative.CreateNativeTexture(name, width, height, (int)pixelFormat);
            }
            return IntPtr.Zero;
        }
#else
        public static IntPtr GetRenderEventCallback()
        {
            return IntPtr.Zero;
        }

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
        
        public static IntPtr CreateTexture(string name, int width, int height, int pixelFormat)
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
        public static extern IntPtr GetRenderEventCallback();
        
        [DllImport(PluginName)]
        public static extern bool IsInitialized();
        
        [DllImport(PluginName)]
        public static extern IntPtr GetD3D12Device();
        
        [DllImport(PluginName)]
        public static extern IntPtr GetD3D12CommandQueue();

        [DllImport(PluginName)]
        public static extern IntPtr CreateNativeTexture([MarshalAs(UnmanagedType.LPWStr)] string name, int width, int height, int pixelFormat);
    }
#endif

    class EventDataPool<TData> : IDisposable
    {
        class Item : IDisposable
        {
            public TData Data;
            public GCHandle Handle;
            public int FramesSinceCreated;

            public void Dispose()
            {
                Handle.Free();
            }

            public void Update()
            {
                FramesSinceCreated++;
            }
        }

        // There can be max 1 pipelined render thread frame in progress, so we keep alive for 2 main thread frames
        const int k_NumFramesToKeepAlive = 2;

        readonly List<Item> m_InUse = new List<Item>();

        public void Dispose()
        {
            foreach (var item in m_InUse)
            {
                item.Dispose();
            }
            
            m_InUse.Clear();
        }
        
        public IntPtr Pin(TData data)
        {
            var item = new Item
            {
                Data = data,
                FramesSinceCreated = 0
            };
            item.Handle = GCHandle.Alloc(item.Data, GCHandleType.Pinned);

            m_InUse.Add(item);
            return item.Handle.AddrOfPinnedObject();
        }

        /// <summary>
        /// Call once at the end of the frame.
        /// </summary>
        public void OnFrameEnd()
        {
            for (var i = 0; i < m_InUse.Count; i++)
            {
                var item = m_InUse[i];

                if (ShouldDispose(item))
                {
                    m_InUse.RemoveAt(i);
                    i--;
                    item.Dispose();
                }
                else
                {
                    item.Update();
                }
            }
        }

        bool ShouldDispose(Item item)
        {
            return item.FramesSinceCreated >= k_NumFramesToKeepAlive;
        }
    }
}
