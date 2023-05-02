#if UNITY_STANDALONE_WIN
#define PLUGIN_AVAILABLE
#endif

using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Disguise.RenderStream.Utils;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;

namespace Disguise.RenderStream
{
    [Serializable]
    sealed class PluginEntry
    {
        private class Nested
        {
            // Explicit static constructor to tell C# compiler
            // not to mark type as beforefieldinit
            static Nested() { }

            [RuntimeInitializeOnLoadMethod]
            static void InitializeGraphics()
            {
                instance.InitializeGraphics();
            }

            internal static readonly PluginEntry instance = new PluginEntry();
        }

        public static PluginEntry instance { get { return Nested.instance; } }

        // Should match NativeRenderingPlugin::ToDXFormat in the native plugin's DX12Texture.h
        public static GraphicsFormat ToGraphicsFormat(RSPixelFormat fmt, bool sRGB)
        {
            // All textures are expected in the normalized 0-1 range.
            // CUDA interop expects an alpha channel.
            return fmt switch
            {
                RSPixelFormat.RS_FMT_BGRA8 or RSPixelFormat.RS_FMT_BGRX8 => sRGB ? GraphicsFormat.B8G8R8A8_SRGB : GraphicsFormat.B8G8R8A8_UNorm,
                RSPixelFormat.RS_FMT_RGBA32F => GraphicsFormat.R32G32B32_SFloat, // Has no UNorm format
                RSPixelFormat.RS_FMT_RGBA16 => GraphicsFormat.R16G16B16A16_UNorm,
                RSPixelFormat.RS_FMT_RGBA8 or RSPixelFormat.RS_FMT_RGBX8 => sRGB ? GraphicsFormat.R8G8B8A8_SRGB : GraphicsFormat.R8G8B8A8_UNorm,    
                _ => throw new ArgumentOutOfRangeException()
            };
        }
        
        public static TextureFormat ToTextureFormat(RSPixelFormat fmt)
        {
            return fmt switch
            {
                RSPixelFormat.RS_FMT_BGRA8 or RSPixelFormat.RS_FMT_BGRX8 => TextureFormat.BGRA32,
                RSPixelFormat.RS_FMT_RGBA32F => TextureFormat.RGBAFloat,
                RSPixelFormat.RS_FMT_RGBA16 => TextureFormat.RGBAHalf,
                RSPixelFormat.RS_FMT_RGBA8 or RSPixelFormat.RS_FMT_RGBX8 => TextureFormat.RGBA32,
                _ => throw new ArgumentOutOfRangeException()
            };
        }

        // isolated functions, do not require init prior to use
        unsafe delegate void pRegisterLogFunc(logger_t logger);
        unsafe delegate void pUnregisterLogFunc();

        unsafe delegate RS_ERROR pInitialise(int expectedVersionMajor, int expectedVersionMinor);
        unsafe delegate RS_ERROR pInitialiseGpGpuWithoutInterop(/*ID3D11Device**/ IntPtr device);
        unsafe delegate RS_ERROR pInitialiseGpGpuWithDX11Device(/*ID3D11Device**/ IntPtr device);
        unsafe delegate RS_ERROR pInitialiseGpGpuWithDX11Resource(/*ID3D11Resource**/ IntPtr resource);
        unsafe delegate RS_ERROR pInitialiseGpGpuWithDX12DeviceAndQueue(/*ID3D12Device**/ IntPtr device, /*ID3D12CommandQueue**/ IntPtr queue);
        unsafe delegate RS_ERROR pInitialiseGpGpuWithOpenGlContexts(/*HGLRC**/ IntPtr glContext, /*HDC**/ IntPtr deviceContext);
        unsafe delegate RS_ERROR pInitialiseGpGpuWithVulkanDevice(/*VkDevice**/ IntPtr device);
        unsafe delegate RS_ERROR pShutdown();

        // non-isolated functions, these require init prior to use

        unsafe delegate RS_ERROR pUseDX12SharedHeapFlag(out UseDX12SharedHeapFlag flag);
        unsafe delegate RS_ERROR pSaveSchema(string assetPath, /*Schema**/ IntPtr schema); // Save schema for project file/custom executable at (assetPath)
        unsafe delegate RS_ERROR pLoadSchema(string assetPath, /*Out*/ /*Schema**/ IntPtr schema, /*InOut*/ ref UInt32 nBytes); // Load schema for project file/custom executable at (assetPath) into a buffer of size (nBytes) starting at (schema)

        // workload functions, these require the process to be running inside d3's asset launcher environment

        unsafe delegate RS_ERROR pSetSchema(/*InOut*/ /*Schema**/ IntPtr schema); // Set schema and fill in per-scene hash for use with rs_getFrameParameters

        unsafe delegate RS_ERROR pGetStreams(/*Out*/ /*StreamDescriptions**/ IntPtr streams, /*InOut*/ ref UInt32 nBytes); // Populate streams into a buffer of size (nBytes) starting at (streams)

        unsafe delegate RS_ERROR pAwaitFrameData(int timeoutMs,  [Out] out FrameData data);  // waits for any asset, any stream to request a frame, provides the parameters for that frame.
        unsafe delegate RS_ERROR pSetFollower(int isFollower); // Used to mark this node as relying on alternative mechanisms to distribute FrameData. Users must provide correct CameraResponseData to sendFrame, and call rs_beginFollowerFrame at the start of the frame, where awaitFrame would normally be called.
        unsafe delegate RS_ERROR pBeginFollowerFrame(double tTracked); // Pass the engine-distributed tTracked value in, if you have called rs_setFollower(1) otherwise do not call this function.

        unsafe delegate RS_ERROR pGetFrameParameters(UInt64 schemaHash, /*Out*/ /*void**/ IntPtr outParameterData, UInt64 outParameterDataSize);  // returns the remote parameters for this frame.
        unsafe delegate RS_ERROR pGetFrameImageData(UInt64 schemaHash, /*Out*/ ImageFrameData* outParameterData, UInt64 outParameterDataCount);   // returns the remote image data for this frame.
        unsafe delegate RS_ERROR pGetFrameImage(Int64 imageId, SenderFrameType frameType, SenderFrameTypeData data); // fills in (data) with the remote image
        unsafe delegate RS_ERROR pGetFrameText(UInt64 schemaHash, UInt32 textParamIndex, /*Out*/ /*const char***/ ref IntPtr outTextPtr); // // returns the remote text data (pointer only valid until next rs_awaitFrameData)

        unsafe delegate RS_ERROR pGetFrameCamera(UInt64 streamHandle, /*Out*/ ref CameraData outCameraData);  // returns the CameraData for this stream, or RS_ERROR_NOTFOUND if no camera data is available for this stream on this frame
        unsafe delegate RS_ERROR pSendFrame(UInt64 streamHandle, SenderFrameType frameType, SenderFrameTypeData data,  [In] ref FrameResponseData sendData); // publish a frame buffer which was generated from the associated tracking and timing information.

        unsafe delegate RS_ERROR pReleaseImage(SenderFrameType frameType, SenderFrameTypeData data);

        unsafe delegate RS_ERROR pLogToD3(string str);
        unsafe delegate RS_ERROR pSendProfilingData(/*ProfilingEntry**/ IntPtr entries, int count);
        unsafe delegate RS_ERROR pSetNewStatusMessage(string msg);

        pRegisterLogFunc m_registerLoggingFunc = null;
        pRegisterLogFunc m_registerErrorLoggingFunc = null;
        pRegisterLogFunc m_registerVerboseLoggingFunc = null;

        pUnregisterLogFunc m_unregisterLoggingFunc = null;
        pUnregisterLogFunc m_unregisterErrorLoggingFunc = null;
        pUnregisterLogFunc m_unregisterVerboseLoggingFunc = null;

        pInitialise m_initialise = null;
        pInitialiseGpGpuWithoutInterop m_initialiseGpGpuWithoutInterop = null;
        pInitialiseGpGpuWithDX11Device m_initialiseGpGpuWithDX11Device = null;
        pInitialiseGpGpuWithDX11Resource m_initialiseGpGpuWithDX11Resource = null;
        pInitialiseGpGpuWithDX12DeviceAndQueue m_initialiseGpGpuWithDX12DeviceAndQueue = null;
        pInitialiseGpGpuWithOpenGlContexts m_initialiseGpGpuWithOpenGlContexts = null;
        pInitialiseGpGpuWithVulkanDevice m_initialiseGpGpuWithVulkanDevice = null;

        pShutdown m_shutdown = null;

        pUseDX12SharedHeapFlag m_useDX12SharedHeapFlag = null;
        pSaveSchema m_saveSchema = null;
        pLoadSchema m_loadSchema = null;

        pSetSchema m_setSchema = null;
        pGetStreams m_getStreams = null;

        pAwaitFrameData m_awaitFrameData = null;
        pSetFollower m_setFollower = null;
        pBeginFollowerFrame m_beginFollowerFrame = null;

        pGetFrameParameters m_getFrameParameters = null;
        pGetFrameImageData m_getFrameImageData = null;
        pGetFrameImage m_getFrameImage = null;
        pGetFrameText m_getFrameText = null;

        pGetFrameCamera m_getFrameCamera = null;
        pSendFrame m_sendFrame = null;

        pReleaseImage m_releaseImage = null;

        pLogToD3 m_logToD3 = null;
        pSendProfilingData m_sendProfilingData = null;
        pSetNewStatusMessage m_setNewStatusMessage = null;
        
        GraphicsDeviceType m_GraphicsDeviceType;
        
        public IntPtr rs_getFrameImage_ptr;
        public IntPtr rs_sendFrame_ptr;

        // Static wrapper to support delegate marshalling to native in IL2CPP
        [AOT.MonoPInvokeCallback(typeof(logger_t))]
        static void logInfo(string message)
        {
            Debug.Log(message);
        }

        // Static wrapper to support delegate marshalling to native in IL2CPP
        [AOT.MonoPInvokeCallback(typeof(logger_t))]
        static void logError(string message)
        {
            Debug.LogError(message);
        }

        void logToD3(string logString, string stackTrace, LogType type)
        {
            if (m_logToD3 == null)
                return;

            string prefix = "";
            switch(type)
            {
                case LogType.Error:
                    prefix = "!!!!! ";
                    break;
                case LogType.Assert:
                    prefix = "!!!!! ASSERT: ";
                    break;
                case LogType.Warning:
                    prefix = "!!! ";
                    break;
                case LogType.Exception:
                    prefix = "!!!!! Exception: ";
                    break;
            }

            string trace = String.IsNullOrEmpty(stackTrace) ? "" : "\nTrace: " + stackTrace;

            m_logToD3(prefix + logString + trace);
        }

        void setNewStatusMessage(string message)
        {
            m_setNewStatusMessage?.Invoke(message);
        }

        ManagedSchema schemaToManagedSchema(Schema cSchema)
        {
            ManagedSchema schema = new ManagedSchema();
            schema.channels = new string[cSchema.channels.nChannels];
            for (int i = 0; i < cSchema.channels.nChannels; ++i)
            {
                IntPtr channelPtr = Marshal.ReadIntPtr(cSchema.channels.channels, i * Marshal.SizeOf(typeof(IntPtr)));
                schema.channels[i] = Marshal.PtrToStringAnsi(channelPtr);
            }
            schema.scenes = new ManagedRemoteParameters[cSchema.scenes.nScenes];
            for (int i = 0; i < cSchema.scenes.nScenes; ++i)
            {
                schema.scenes[i] = new ManagedRemoteParameters();
                ManagedRemoteParameters managedParameters = schema.scenes[i];
                RemoteParameters parameters = (RemoteParameters)Marshal.PtrToStructure(cSchema.scenes.scenes + i * Marshal.SizeOf(typeof(RemoteParameters)), typeof(RemoteParameters));
                managedParameters.name = parameters.name;
                managedParameters.parameters = new ManagedRemoteParameter[parameters.nParameters];
                for (int j = 0; j < parameters.nParameters; ++j)
                {
                    managedParameters.parameters[j] = new ManagedRemoteParameter();
                    ManagedRemoteParameter managedParameter = managedParameters.parameters[j];
                    RemoteParameter parameter = (RemoteParameter)Marshal.PtrToStructure(parameters.parameters + j * Marshal.SizeOf(typeof(RemoteParameter)), typeof(RemoteParameter));
                    managedParameter.group = parameter.group;
                    managedParameter.displayName = parameter.displayName;
                    managedParameter.key = parameter.key;
                    managedParameter.type = parameter.type;
                    if (parameter.type == RemoteParameterType.RS_PARAMETER_NUMBER)
                    {
                        managedParameter.min = parameter.defaults.numerical_min;
                        managedParameter.max = parameter.defaults.numerical_max;
                        managedParameter.step = parameter.defaults.numerical_step;
                        managedParameter.defaultValue = parameter.defaults.numerical_defaultValue;
                    }
                    else if (parameter.type == RemoteParameterType.RS_PARAMETER_TEXT)
                    {
                        managedParameter.defaultValue = Marshal.PtrToStringAnsi(parameter.defaults.text_defaultValue);
                    }
                    managedParameter.options = new string[parameter.nOptions];
                    for (int k = 0; k < parameter.nOptions; ++k)
                    {
                        IntPtr optionPtr = Marshal.ReadIntPtr(parameter.options, k * Marshal.SizeOf(typeof(IntPtr)));
                        managedParameter.options[k] = Marshal.PtrToStringAnsi(optionPtr);
                    }
                    managedParameter.dmxOffset = parameter.dmxOffset;
                    managedParameter.dmxType = parameter.dmxType;
                }
                managedParameters.hash = parameters.hash;
            }
            return schema;
        }

        public RS_ERROR saveSchema(string assetPath, ref ManagedSchema schema)
        {
            if (m_saveSchema == null)
                return RS_ERROR.RS_NOT_INITIALISED;

            List<IntPtr> allocations = new List<IntPtr>();
            try
            {
                Schema cSchema = new Schema();
                cSchema.engineName = "Unity Engine";
                cSchema.engineVersion = Application.unityVersion;
                cSchema.info = Application.productName;
                cSchema.channels.nChannels = (UInt32)schema.channels.Length;
                cSchema.channels.channels = Marshal.AllocHGlobal(Marshal.SizeOf(typeof(IntPtr)) * (int)cSchema.channels.nChannels);
                allocations.Add(cSchema.channels.channels);
                for (int i = 0; i < cSchema.channels.nChannels; ++i)
                {
                    IntPtr channelPtr = Marshal.StringToHGlobalAnsi(schema.channels[i]);
                    allocations.Add(channelPtr);
                    Marshal.WriteIntPtr(cSchema.channels.channels, i * Marshal.SizeOf(typeof(IntPtr)), channelPtr);
                }

                cSchema.scenes.nScenes = (UInt32)schema.scenes.Length;
                cSchema.scenes.scenes = Marshal.AllocHGlobal(Marshal.SizeOf(typeof(RemoteParameters)) * (int)cSchema.scenes.nScenes);
                allocations.Add(cSchema.scenes.scenes);
                for (int i = 0; i < cSchema.scenes.nScenes; ++i)
                {
                    ManagedRemoteParameters managedParameters = schema.scenes[i];
                    RemoteParameters parameters = new RemoteParameters();
                    parameters.name = managedParameters.name;
                    parameters.nParameters = (UInt32)managedParameters.parameters.Length;
                    parameters.parameters = Marshal.AllocHGlobal(Marshal.SizeOf(typeof(RemoteParameter)) * (int)parameters.nParameters);
                    allocations.Add(parameters.parameters);
                    for (int j = 0; j < parameters.nParameters; ++j)
                    {
                        ManagedRemoteParameter managedParameter = managedParameters.parameters[j];
                        RemoteParameter parameter = new RemoteParameter();
                        parameter.group = managedParameter.group;
                        parameter.displayName = managedParameter.displayName;
                        parameter.key = managedParameter.key;
                        parameter.type = managedParameter.type;
                        if (parameter.type == RemoteParameterType.RS_PARAMETER_NUMBER)
                        {
                            parameter.defaults.numerical_min = managedParameter.min;
                            parameter.defaults.numerical_max = managedParameter.max;
                            parameter.defaults.numerical_step = managedParameter.step;
                            parameter.defaults.numerical_defaultValue = Convert.ToSingle(managedParameter.defaultValue);
                        }
                        else if (parameter.type == RemoteParameterType.RS_PARAMETER_TEXT)
                        {
                            IntPtr textPtr = Marshal.StringToHGlobalAnsi(Convert.ToString(managedParameter.defaultValue));
                            allocations.Add(textPtr);
                            parameter.defaults.text_defaultValue = textPtr;
                        }
                        parameter.nOptions = (UInt32)managedParameter.options.Length;
                        parameter.options = Marshal.AllocHGlobal(Marshal.SizeOf(typeof(IntPtr)) * (int)parameter.nOptions);
                        allocations.Add(parameter.options);
                        for (int k = 0; k < parameter.nOptions; ++k)
                        {
                            IntPtr optionPtr = Marshal.StringToHGlobalAnsi(managedParameter.options[k]);
                            allocations.Add(optionPtr);
                            Marshal.WriteIntPtr(parameter.options, k * Marshal.SizeOf(typeof(IntPtr)), optionPtr);
                        }
                        parameter.dmxOffset = managedParameter.dmxOffset;
                        parameter.dmxType = managedParameter.dmxType;
                        Marshal.StructureToPtr(parameter, parameters.parameters + j * Marshal.SizeOf(typeof(RemoteParameter)), false);
                    }
                    Marshal.StructureToPtr(parameters, cSchema.scenes.scenes + i * Marshal.SizeOf(typeof(RemoteParameters)), false);
                }

                IntPtr schemaPtr = Marshal.AllocHGlobal(Marshal.SizeOf(typeof(Schema)));
                allocations.Add(schemaPtr);
                Marshal.StructureToPtr(cSchema, schemaPtr, false);
                RS_ERROR error = m_saveSchema(assetPath, schemaPtr);
                if (error == RS_ERROR.RS_ERROR_SUCCESS)
                {
                    cSchema = (Schema)Marshal.PtrToStructure(schemaPtr, typeof(Schema));
                    schema = schemaToManagedSchema(cSchema);
                }
                return error;
            }
            finally
            {
                foreach (IntPtr ptr in allocations)
                    Marshal.FreeHGlobal(ptr);
            }
            //return RS_ERROR.RS_ERROR_UNSPECIFIED;
        }

        public RS_ERROR LoadSchema(string assetPath, out ManagedSchema schema)
        {
            schema = new ManagedSchema();
            if (m_loadSchema == null)
                return RS_ERROR.RS_NOT_INITIALISED;

            IntPtr descMem = IntPtr.Zero;
            UInt32 nBytes = 0;
            m_loadSchema(assetPath, descMem, ref nBytes);

            const int MAX_TRIES = 3;
            int iterations = 0;

            RS_ERROR res = RS_ERROR.RS_ERROR_BUFFER_OVERFLOW;
            try
            {
                do
                {
                    Marshal.FreeHGlobal(descMem);
                    descMem = Marshal.AllocHGlobal((int)nBytes);
                    res = m_loadSchema(assetPath, descMem, ref nBytes);
                    if (res == RS_ERROR.RS_ERROR_SUCCESS)
                    {
                        Schema cSchema = (Schema)Marshal.PtrToStructure(descMem, typeof(Schema));
                        schema = schemaToManagedSchema(cSchema);
                    }

                    ++iterations;
                } while (res == RS_ERROR.RS_ERROR_BUFFER_OVERFLOW && iterations < MAX_TRIES);
            }
            finally
            {
                Marshal.FreeHGlobal(descMem);
            }
            return res;
        }

        public RS_ERROR getStreams(out StreamDescription[] streams)
        {
            streams = null;
            
            if (m_getStreams == null)
                return RS_ERROR.RS_NOT_INITIALISED;

            IntPtr descMem = IntPtr.Zero;
            UInt32 nBytes = 0;
            m_getStreams(descMem, ref nBytes);

            const int MAX_TRIES = 3;
            int iterations = 0;

            RS_ERROR res = RS_ERROR.RS_ERROR_BUFFER_OVERFLOW;
            try
            {
                do
                {
                    Marshal.FreeHGlobal(descMem);
                    descMem = Marshal.AllocHGlobal((int)nBytes);
                    res = m_getStreams(descMem, ref nBytes);
                    if (res == RS_ERROR.RS_ERROR_SUCCESS)
                    {
                        StreamDescriptions desc = (StreamDescriptions)Marshal.PtrToStructure(descMem, typeof(StreamDescriptions));
                        streams = new StreamDescription[desc.nStreams];
                        for (int i = 0; i < desc.nStreams; ++i)
                        {
                            IntPtr current = desc.streams + i * Marshal.SizeOf(typeof(StreamDescription));
                            streams[i] = (StreamDescription)Marshal.PtrToStructure(current, typeof(StreamDescription));
                        }
                    }

                    ++iterations;
                } while (res == RS_ERROR.RS_ERROR_BUFFER_OVERFLOW && iterations < MAX_TRIES);
            }
            finally
            {
                Marshal.FreeHGlobal(descMem);
            }
            return res;
        }

        public RS_ERROR sendFrame(UInt64 streamHandle, SenderFrameType frameType, SenderFrameTypeData data, ref FrameResponseData sendData)
        {
            if (m_sendFrame == null)
                return RS_ERROR.RS_NOT_INITIALISED;
            RS_ERROR error = m_sendFrame(streamHandle, frameType, data, ref sendData);
            return error;
        }

        public RS_ERROR awaitFrameData(int timeoutMs, out FrameData data)
        {
            if (m_awaitFrameData == null)
            {
                data = default;
                return RS_ERROR.RS_NOT_INITIALISED;
            }

            return m_awaitFrameData(timeoutMs, out data);
            //return RS_ERROR.RS_ERROR_UNSPECIFIED;
        }

        public RS_ERROR setFollower(int isFollower)
        {
            if (m_setFollower == null)
                return RS_ERROR.RS_NOT_INITIALISED;

            try
            {
                RS_ERROR error = m_setFollower(isFollower);
                return error;
            }
            finally
            {
            }
        }

        public RS_ERROR beginFollowerFrame(double tTracked)
        {
            if (m_beginFollowerFrame == null)
                return RS_ERROR.RS_NOT_INITIALISED;

            try
            {
                RS_ERROR error = m_beginFollowerFrame(tTracked);
                return error;
            }
            finally
            {
            }
        }
        
        public RS_ERROR GetFrameParameters(UInt64 schemaHash, Span<float> outParameterData)
        {
            if (m_getFrameParameters == null)
                return RS_ERROR.RS_NOT_INITIALISED;

            unsafe
            {
                fixed(float* outDataPtr = outParameterData)
                {
                    RS_ERROR error = m_getFrameParameters(schemaHash,
                        (IntPtr)outDataPtr,
                        (ulong)outParameterData.Length * sizeof(float));
                    return error;
                }
            }
        }
        
        public RS_ERROR GetFrameImageData(UInt64 schemaHash, Span<ImageFrameData> outParameterData)
        {
            if (m_getFrameImageData == null)
                return RS_ERROR.RS_NOT_INITIALISED;

            unsafe
            {
                fixed (ImageFrameData* outDataPtr = outParameterData)
                {
                    RS_ERROR error = m_getFrameImageData(schemaHash,
                        outDataPtr,
                        (UInt64)outParameterData.Length);
                    return error;
                }
            }
        }

        public RS_ERROR getFrameImage(Int64 imageId, ref Texture2D texture)
        {
            if (m_getFrameImage == null)
                return RS_ERROR.RS_NOT_INITIALISED;

            RS_ERROR error = RS_ERROR.RS_ERROR_SUCCESS;

            try
            {
                SenderFrameTypeData data = new SenderFrameTypeData();

                switch (GraphicsDeviceType)
                {
                    case GraphicsDeviceType.Direct3D11:
                        data.dx11_resource = texture.GetNativeTexturePtr();
                        error = m_getFrameImage(imageId, SenderFrameType.RS_FRAMETYPE_DX11_TEXTURE, data);
                        break;
                    
                    case GraphicsDeviceType.Direct3D12:
                        data.dx12_resource = texture.GetNativeTexturePtr();
                        error = m_getFrameImage(imageId, SenderFrameType.RS_FRAMETYPE_DX12_TEXTURE, data);
                        break;
                }
                
                return error;
            }
            finally
            {
            }
            //return RS_ERROR.RS_ERROR_UNSPECIFIED;
        }

        public RS_ERROR getFrameText(UInt64 schemaHash, UInt32 textParamIndex, ref string text)
        {
            if (m_getFrameText == null)
                return RS_ERROR.RS_NOT_INITIALISED;

            try
            {
                IntPtr textPtr = IntPtr.Zero;
                RS_ERROR error = m_getFrameText(schemaHash, textParamIndex, ref textPtr);
                if (error == RS_ERROR.RS_ERROR_SUCCESS)
                    text = Marshal.PtrToStringAnsi(textPtr);
                return error;
            }
            finally
            {
            }
            //return RS_ERROR.RS_ERROR_UNSPECIFIED;
        }

        public RS_ERROR getFrameCamera(UInt64 streamHandle, ref CameraData outCameraData)
        {
            if (m_getFrameCamera == null)
                return RS_ERROR.RS_NOT_INITIALISED;

            return m_getFrameCamera(streamHandle, ref outCameraData);
            //return RS_ERROR.RS_ERROR_UNSPECIFIED;
        }

        public RS_ERROR useDX12SharedHeapFlag(out UseDX12SharedHeapFlag flag)
        {
            return m_useDX12SharedHeapFlag(out flag);
        }

        public GraphicsDeviceType GraphicsDeviceType => m_GraphicsDeviceType;
        
        bool GraphicsDeviceTypeIsSupported => m_GraphicsDeviceType is GraphicsDeviceType.Direct3D11 or GraphicsDeviceType.Direct3D12;

#if PLUGIN_AVAILABLE

        [DllImport("kernel32.dll")]
        static extern IntPtr LoadLibraryEx(string lpLibFileName, IntPtr fileHandle, int flags);

        [DllImport("kernel32.dll")]
        static extern IntPtr GetProcAddress(IntPtr hModule, string lpProcName);

        [DllImport("kernel32.dll", CharSet = CharSet.Ansi)]
        static extern bool FreeLibrary(IntPtr hModule);

        private void free()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.quitting -= free;
            UnityEditor.AssemblyReloadEvents.beforeAssemblyReload -= free;
#else
            Application.quitting -= free;
#endif

            if (functionsLoaded)
            {
                if (m_logToD3 != null)
                    Application.logMessageReceivedThreaded -= logToD3;

                if (m_unregisterErrorLoggingFunc != null)
                    m_unregisterErrorLoggingFunc();
                if (m_unregisterLoggingFunc != null)
                    m_unregisterLoggingFunc();

                RS_ERROR error = m_shutdown();
                if (error != RS_ERROR.RS_ERROR_SUCCESS)
                    Debug.LogError(string.Format("Failed to shutdown: {0}", error));
                functionsLoaded = false;
                Debug.Log("Shut down RenderStream");
            }

            if (d3RenderStreamDLL != IntPtr.Zero)
            {
                FreeLibrary(d3RenderStreamDLL);
                d3RenderStreamDLL = IntPtr.Zero;
                Debug.Log("Unloaded RenderStream");
            }

            if (handleReference.IsAllocated)
                handleReference.Free();
        }

        public bool IsAvailable
        {
            get
            {
                return functionsLoaded && GraphicsDeviceTypeIsSupported;
            }
        }
#else
        private void free() {}
        public bool IsAvailable { get { return false; } }
#endif

        const int LOAD_IGNORE_CODE_AUTHZ_LEVEL = 0x00000010;
        const int LOAD_LIBRARY_SEARCH_DLL_LOAD_DIR = 0x00000100;
        const int LOAD_LIBRARY_SEARCH_APPLICATION_DIR = 0x00000200;
        const int LOAD_LIBRARY_SEARCH_USER_DIRS = 0x00000400;
        const int LOAD_LIBRARY_SEARCH_SYSTEM32 = 0x00000800;

        const string _dllName = "d3renderstream";

        const int RENDER_STREAM_VERSION_MAJOR = 1;
        const int RENDER_STREAM_VERSION_MINOR = 30;

        bool functionsLoaded = false;
        IntPtr d3RenderStreamDLL = IntPtr.Zero;
        GCHandle handleReference; // Everything is run under coroutines with odd lifetimes, so store a reference to GCHandle

        string name;

        // https://answers.unity.com/questions/16804/retrieving-project-name.html?childToView=478633#answer-478633
        public string GetProjectName()
        {
            string[] s = Application.dataPath.Split('/');
            if (s.Length >= 2)
            {
                string projectName = s[s.Length - 2];
                return projectName;
            }
            return "UNKNOWN UNITY PROJECT";
        }

        private bool LoadFn<T>(ref T fn, string fnName) where T : Delegate
        {
            fn = DelegateBuilder<T>(d3RenderStreamDLL, fnName);
            if (fn == null)
            {
                Debug.LogError(string.Format("Failed load function \"{0}\" from {1}.dll", fnName, _dllName));
                return false;
            }
            return true;
        }

        private PluginEntry()
        {
#if PLUGIN_AVAILABLE
            m_GraphicsDeviceType = SystemInfo.graphicsDeviceType;
            if (!GraphicsDeviceTypeIsSupported)
            {
                throw new InvalidOperationException($"Unsupported GraphicsDeviceType: {PluginEntry.instance.GraphicsDeviceType}. Only Direct3D11 and Direct3D12 are supported.");
            }
            
            var result = RegistryWrapper.ReadRegKey(
                RegistryWrapper.HKEY_CURRENT_USER,
                @"Software\d3 Technologies\d3 Production Suite",
                "exe path", 
                out var d3ExePath);

            if (result == RegistryWrapper.ReadRegKeyResult.OpenFailed)
            {
                Debug.LogError(string.Format("Failed to find path to {0}.dll. d3 Not installed?", _dllName));
                return;
            }
            
            if (result != RegistryWrapper.ReadRegKeyResult.Success)
            {
                Debug.LogError(string.Format("Failed to find path to {0}.dll. Error: {1}", _dllName, result));
                return;
            }

            d3ExePath = d3ExePath.Replace(@"\\", @"\");
            int endSeparator = d3ExePath.LastIndexOf(Path.DirectorySeparatorChar);
            if (endSeparator != d3ExePath.Length - 1)
                d3ExePath = d3ExePath.Substring(0, endSeparator + 1);

            string libPath = d3ExePath + _dllName + ".dll";
            d3RenderStreamDLL = LoadWin32Library(libPath);
            if (d3RenderStreamDLL == IntPtr.Zero)
            {
                Debug.LogError(string.Format("Failed to load {0}.dll from {1}", _dllName, d3ExePath));
                return;
            }
            
            functionsLoaded = true;

            functionsLoaded &= LoadFn(ref m_registerLoggingFunc, "rs_registerLoggingFunc");
            functionsLoaded &= LoadFn(ref m_registerErrorLoggingFunc, "rs_registerErrorLoggingFunc");
            functionsLoaded &= LoadFn(ref m_registerVerboseLoggingFunc, "rs_registerVerboseLoggingFunc");

            functionsLoaded &= LoadFn(ref m_unregisterLoggingFunc, "rs_unregisterLoggingFunc");
            functionsLoaded &= LoadFn(ref m_unregisterErrorLoggingFunc, "rs_unregisterErrorLoggingFunc");
            functionsLoaded &= LoadFn(ref m_unregisterVerboseLoggingFunc, "rs_unregisterVerboseLoggingFunc");

            functionsLoaded &= LoadFn(ref m_initialise, "rs_initialise");
            functionsLoaded &= LoadFn(ref m_initialiseGpGpuWithoutInterop, "rs_initialiseGpGpuWithoutInterop");
            functionsLoaded &= LoadFn(ref m_initialiseGpGpuWithDX11Device, "rs_initialiseGpGpuWithDX11Device");
            functionsLoaded &= LoadFn(ref m_initialiseGpGpuWithDX11Resource, "rs_initialiseGpGpuWithDX11Resource");
            functionsLoaded &= LoadFn(ref m_initialiseGpGpuWithDX12DeviceAndQueue, "rs_initialiseGpGpuWithDX12DeviceAndQueue");
            functionsLoaded &= LoadFn(ref m_initialiseGpGpuWithOpenGlContexts, "rs_initialiseGpGpuWithOpenGlContexts");
            functionsLoaded &= LoadFn(ref m_initialiseGpGpuWithVulkanDevice, "rs_initialiseGpGpuWithVulkanDevice");
            functionsLoaded &= LoadFn(ref m_shutdown, "rs_shutdown");

            functionsLoaded &= LoadFn(ref m_useDX12SharedHeapFlag, "rs_useDX12SharedHeapFlag");
            functionsLoaded &= LoadFn(ref m_saveSchema, "rs_saveSchema");
            functionsLoaded &= LoadFn(ref m_loadSchema, "rs_loadSchema");

            functionsLoaded &= LoadFn(ref m_setSchema, "rs_setSchema");

            functionsLoaded &= LoadFn(ref m_getStreams, "rs_getStreams");

            functionsLoaded &= LoadFn(ref m_awaitFrameData, "rs_awaitFrameData");
            functionsLoaded &= LoadFn(ref m_setFollower, "rs_setFollower");
            functionsLoaded &= LoadFn(ref m_beginFollowerFrame, "rs_beginFollowerFrame");

            functionsLoaded &= LoadFn(ref m_getFrameParameters, "rs_getFrameParameters");
            functionsLoaded &= LoadFn(ref m_getFrameImageData, "rs_getFrameImageData");
            functionsLoaded &= LoadFn(ref m_getFrameImage, "rs_getFrameImage");
            functionsLoaded &= LoadFn(ref m_getFrameText, "rs_getFrameText");

            functionsLoaded &= LoadFn(ref m_getFrameCamera, "rs_getFrameCamera");
            functionsLoaded &= LoadFn(ref m_sendFrame, "rs_sendFrame");

            functionsLoaded &= LoadFn(ref m_releaseImage, "rs_releaseImage");

            functionsLoaded &= LoadFn(ref m_logToD3, "rs_logToD3");
            functionsLoaded &= LoadFn(ref m_sendProfilingData, "rs_sendProfilingData");
            functionsLoaded &= LoadFn(ref m_setNewStatusMessage, "rs_setNewStatusMessage");
            
            rs_getFrameImage_ptr = GetProcAddress(d3RenderStreamDLL, "rs_getFrameImage");
            Debug.Assert(rs_getFrameImage_ptr != IntPtr.Zero, "Failed to get rs_getFrameImage function pointer");

            rs_sendFrame_ptr = GetProcAddress(d3RenderStreamDLL, "rs_sendFrame");
            Debug.Assert(rs_sendFrame_ptr != IntPtr.Zero, "Failed to get rs_sendFrame_ptr function pointer");
            
            if (!functionsLoaded)
            {
                Debug.LogError(string.Format("One or more functions failed load from {0}.dll", _dllName));
                return;
            }

            // There is an issue with these logging callbacks sometimes throwing inside of the dll which can cause all kinds of problems
            // exception consistentency is questionable, often the same exception can be seen at the same point in time
            // however periodically a minor difference may occur where the exception is not thrown where expected or even at all

            if (m_registerLoggingFunc != null)
                m_registerLoggingFunc(logInfo);
            if (m_registerErrorLoggingFunc != null)
                m_registerErrorLoggingFunc(logError);

            if (m_logToD3 != null)
                Application.logMessageReceivedThreaded += logToD3;

            RS_ERROR error = m_initialise(RENDER_STREAM_VERSION_MAJOR, RENDER_STREAM_VERSION_MINOR);
            if (error == RS_ERROR.RS_ERROR_INCOMPATIBLE_VERSION)
                Debug.LogError(string.Format("Unsupported RenderStream library, expected version {0}.{1}", RENDER_STREAM_VERSION_MAJOR, RENDER_STREAM_VERSION_MINOR));
            else if (error != RS_ERROR.RS_ERROR_SUCCESS)
                Debug.LogError(string.Format("Failed to initialise: {0}", error));

            Debug.Log("Loaded RenderStream");

#if UNITY_EDITOR
            UnityEditor.EditorApplication.quitting += free;
            UnityEditor.AssemblyReloadEvents.beforeAssemblyReload += free;
#else
            Application.quitting += free;
#endif

            name = GetProjectName();
#else
            Debug.LogError(string.Format("{0}.dll is only available on Windows", _dllName));
#endif
        }

        ~PluginEntry()
        {
            free();
        }

        internal void InitializeGraphics()
        {
            switch (GraphicsDeviceType)
            {
                case GraphicsDeviceType.Direct3D11:
                    Texture2D texture = new Texture2D(1, 1);
                    var error = m_initialiseGpGpuWithDX11Resource(texture.GetNativeTexturePtr());
                    if (error != RS_ERROR.RS_ERROR_SUCCESS)
                        Debug.LogError(string.Format("Failed to initialise GPU interop: {0}", error));
                    break;
                    
                case GraphicsDeviceType.Direct3D12:

                    if (!NativeRenderingPlugin.IsInitialized())
                        Debug.LogError("Failed to initialise NativeRenderingPlugin (for DX12 support)");
                        
                    var device = NativeRenderingPlugin.GetD3D12Device();
                    var commandQueue = NativeRenderingPlugin.GetD3D12CommandQueue();
                        
                    if (device == IntPtr.Zero)
                        Debug.LogError("Failed to initialise DX12 device");
                        
                    if (commandQueue == IntPtr.Zero)
                        Debug.LogError(string.Format("Failed to initialise DX12 command queue"));
                        
                    error = m_initialiseGpGpuWithDX12DeviceAndQueue(device, commandQueue);
                    if (error != RS_ERROR.RS_ERROR_SUCCESS)
                        Debug.LogError(string.Format("Failed to initialise GPU interop: {0}", error));
                        
                    break;
            }
        }

        static IntPtr LoadWin32Library(string dllFilePath)
        {
            System.IntPtr moduleHandle = IntPtr.Zero ;
#if PLUGIN_AVAILABLE
            moduleHandle = LoadLibraryEx(dllFilePath, IntPtr.Zero, LOAD_IGNORE_CODE_AUTHZ_LEVEL | LOAD_LIBRARY_SEARCH_DLL_LOAD_DIR | LOAD_LIBRARY_SEARCH_APPLICATION_DIR | LOAD_LIBRARY_SEARCH_SYSTEM32 | LOAD_LIBRARY_SEARCH_USER_DIRS);
            if (moduleHandle == IntPtr.Zero)
            {
                // I'm gettin last dll error
                int errorCode = Marshal.GetLastWin32Error();
                Debug.LogError(string.Format("There was an error during dll loading : {0}, error - {1}", dllFilePath, errorCode));
            }
#endif
            return moduleHandle;
        }

        static T DelegateBuilder<T>(IntPtr loadedDLL, string functionName) where T : Delegate
        {
            IntPtr pAddressOfFunctionToCall = IntPtr.Zero;
#if PLUGIN_AVAILABLE
            pAddressOfFunctionToCall = GetProcAddress(loadedDLL, functionName);
            if (pAddressOfFunctionToCall == IntPtr.Zero)
            {
                return null;
            }
#endif
            T functionDelegate = Marshal.GetDelegateForFunctionPointer(pAddressOfFunctionToCall, typeof(T)) as T;
            return functionDelegate;
        }
    }
    
#if !UNITY_2022_2_OR_NEWER
    static class PluginExtensions
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe Span<T> AsSpan<T>(this NativeArray<T> arr) where T : unmanaged =>
            new(arr.GetUnsafePtr(), arr.Length);
    }
#endif
}
