#if UNITY_STANDALONE_WIN && UNITY_64
#define NATIVE_RENDERING_PLUGIN_AVAILABLE
#endif

using System;
using System.Runtime.InteropServices;
using Disguise.RenderStream.Utils;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
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
            InputImage,
            SendFrame
        }

        public struct InputImageData
        {
            public IntPtr m_rs_getFrameImage;
            public Int64 m_ImageId;
            public IntPtr m_Texture;
        }
        
        public struct SendFrameData
        {
            public IntPtr m_rs_sendFrame;
            public ulong m_StreamHandle;
            public IntPtr m_Texture;
            public CameraResponseData m_CameraResponseData;
        }

        public static EventDataPool<InputImageData> InputImageDataPool { get; } = new EventDataPool<InputImageData>();
        public static EventDataPool<SendFrameData> SendFrameDataPool { get; } = new EventDataPool<SendFrameData>();

        static int? s_BaseEventID;

        static NativeRenderingPlugin()
        {
            PlayerLoopExtensions.RegisterUpdate<PostLateUpdate.FinishFrameRendering, FinishFrameRendering>(OnFinishFrameRendering);
        }
        
        public static int GetEventID(EventID evt)
        {
            s_BaseEventID ??= GetBaseEventID();
            return (int)evt + s_BaseEventID.Value;
        }

        static void OnFinishFrameRendering()
        {
            InputImageDataPool.OnFrameEnd();
            SendFrameDataPool.OnFrameEnd();
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
        
        static int GetBaseEventID()
        {
            return NativeRenderingPluginNative.GetBaseEventID();
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
        
        public static IntPtr CreateNativeTexture(string name, int width, int height, RSPixelFormat pixelFormat, bool sRGB)
        {
            if (IsInitialized())
            {
                return NativeRenderingPluginNative.CreateNativeTexture(name, width, height, pixelFormat, sRGB);
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

        static int GetBaseEventID()
        {
            return 0;
        }
        
        public static IntPtr GetD3D12Device()
        {
            return IntPtr.Zero;
        }
        
        public static IntPtr GetD3D12CommandQueue()
        {
            return IntPtr.Zero;
        }
        
        public static IntPtr CreateNativeTexture(string name, int width, int height, RSPixelFormat pixelFormat, bool sRGB)
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
        public static extern int GetBaseEventID();
        
        [DllImport(PluginName)]
        public static extern IntPtr GetD3D12Device();
        
        [DllImport(PluginName)]
        public static extern IntPtr GetD3D12CommandQueue();

        [DllImport(PluginName)]
        public static extern IntPtr CreateNativeTexture([MarshalAs(UnmanagedType.LPWStr)] string name, int width, int height, RSPixelFormat pixelFormat, bool sRGB);
    }
#endif
    
    class EventDataPool<TData> : AutoDisposable where TData : unmanaged
    {
        class Record
        {
            public bool InUse => m_InUse;
            public int FramesSinceCreated => m_FramesSinceCreated;
            
            bool m_InUse;
            int m_FramesSinceCreated;

            public Record()
            {
                MarkUnused();
            }

            public void MarkUsed()
            {
                m_InUse = true;
                m_FramesSinceCreated = 0;
            }

            public void MarkUnused()
            {
                m_InUse = false;
                m_FramesSinceCreated = 0;
            }
    
            public void Update()
            {
                m_FramesSinceCreated++;
            }
        }
    
        // There can be max 1 pipelined render thread frame in progress, so we keep alive for 2 main thread frames
        const int k_NumFramesToKeepAlive = 2;
    
        const int k_Capacity = 64;
        NativeArray<TData> m_Data = new(k_Capacity, Allocator.Persistent);
        readonly Record[] m_Records = new Record[k_Capacity];
    
        public EventDataPool()
        {
            for (int i = 0; i < m_Records.Length; i++)
            {
                m_Records[i] = new Record();
            }
        }
        
        protected override void Dispose()
        {
            m_Data.Dispose();
        }
    
        // Copy the data into unmanaged memory
        public bool TryPreserve(TData data, out IntPtr pointer)
        {
            var freeIndex = -1;
            for (var i = 0; i < m_Records.Length; i++)
            {
                if (!m_Records[i].InUse)
                {
                    freeIndex = i;
                    m_Records[i].MarkUsed();
                    break;
                }
            }

            if (freeIndex >= 0)
            {
                m_Data[freeIndex] = data;
                unsafe
                {
                    TData* ptr = (TData*)m_Data.GetUnsafePtr();
                    pointer = (IntPtr)(ptr + freeIndex);
                    return true;
                }
            }

            pointer = IntPtr.Zero;
            return false;
        }
    
        /// <summary>
        /// Call once at the end of the frame.
        /// </summary>
        public void OnFrameEnd()
        {
            for (var i = m_Records.Length - 1; i >= 0; i--)
            {
                var record = m_Records[i];
    
                if (ShouldDispose(record))
                {
                    record.MarkUnused();
                }
                else
                {
                    record.Update();
                }
            }
        }
    
        static bool ShouldDispose(Record record)
        {
            return record.InUse && record.FramesSinceCreated >= k_NumFramesToKeepAlive;
        }
    }
}
