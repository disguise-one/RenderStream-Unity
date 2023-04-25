using System;
using System.Runtime.InteropServices;

namespace Disguise.RenderStream
{
    // d3renderstream/d3renderstream.h
    using StreamHandle = UInt64;
    using CameraHandle = UInt64;
    
    delegate void logger_t(string message);
    
    public enum RS_ERROR : UInt32
    {
        RS_ERROR_SUCCESS = 0,

        // Core is not initialised
        RS_NOT_INITIALISED,

        // Core is already initialised
        RS_ERROR_ALREADYINITIALISED,

        // Given handle is invalid
        RS_ERROR_INVALIDHANDLE,

        // Maximum number of frame senders have been created
        RS_MAXSENDERSREACHED,

        RS_ERROR_BADSTREAMTYPE,

        RS_ERROR_NOTFOUND,

        RS_ERROR_INCORRECTSCHEMA,

        RS_ERROR_INVALID_PARAMETERS,

        RS_ERROR_BUFFER_OVERFLOW,

        RS_ERROR_TIMEOUT,

        RS_ERROR_STREAMS_CHANGED,

        RS_ERROR_INCOMPATIBLE_VERSION,

        RS_ERROR_FAILED_TO_GET_DXDEVICE_FROM_RESOURCE,

        RS_ERROR_FAILED_TO_INITIALISE_GPGPU,

        RS_ERROR_QUIT,

        RS_ERROR_UNSPECIFIED
    }

    public enum RSPixelFormat : UInt32
    {
        RS_FMT_INVALID,

        RS_FMT_BGRA8,
        RS_FMT_BGRX8,

        RS_FMT_RGBA32F,

        RS_FMT_RGBA16,

        RS_FMT_RGBA8,
        RS_FMT_RGBX8,
    }

    public enum SenderFrameType : UInt32
    {
        RS_FRAMETYPE_HOST_MEMORY = 0x00000000,
        RS_FRAMETYPE_DX11_TEXTURE,
        RS_FRAMETYPE_DX12_TEXTURE,
        RS_FRAMETYPE_OPENGL_TEXTURE,
        RS_FRAMETYPE_UNKNOWN
    }

    public enum UseDX12SharedHeapFlag : UInt32
    {
        RS_DX12_USE_SHARED_HEAP_FLAG,
        RS_DX12_DO_NOT_USE_SHARED_HEAP_FLAG
    }

    // Bitmask flags
    public enum FRAMEDATA_FLAGS : UInt32
    {
        FRAMEDATA_NO_FLAGS = 0,
        FRAMEDATA_RESET = 1
    }

    public enum REMOTEPARAMETER_FLAGS : UInt32
    {
        REMOTEPARAMETER_NO_FLAGS = 0,
        REMOTEPARAMETER_NO_SEQUENCE = 1
    }

    [StructLayout(LayoutKind.Sequential, Pack = 4)]
	public struct D3TrackingData
	{
		public float virtualZoomScale;
		public byte virtualReprojectionRequired;
		public float xRealCamera, yRealCamera, zRealCamera;
		public float rxRealCamera, ryRealCamera, rzRealCamera;
	}  // Tracking data required by d3 but not used to render content

    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    public struct CameraData
    {
        public StreamHandle streamHandle;
        public CameraHandle cameraHandle;
        public float x, y, z;
        public float rx, ry, rz;
        public float focalLength;
        public float sensorX, sensorY;
        public float cx, cy;
        public float nearZ, farZ;
        public float orthoWidth;  // If > 0, an orthographic camera should be used
		public D3TrackingData d3Tracking;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    public readonly struct FrameData
    {
        public readonly double tTracked;
        public readonly double localTime;
        public readonly double localTimeDelta;
        public readonly UInt32 frameRateNumerator;
        public readonly UInt32 frameRateDenominator;
        public readonly UInt32 flags; // FRAMEDATA_FLAGS
        public readonly UInt32 scene;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    public struct CameraResponseData
    {
        public double tTracked;
        public CameraData camera;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    public struct FrameResponseData
    {
        public /*CameraResponseData**/ IntPtr cameraData;
        public UInt64 schemaHash;
        public UInt64 parameterDataSize;
        public IntPtr parameterData;
        public UInt32 textDataCount;
        public /*const char***/ IntPtr textData;
    }

    [StructLayout(LayoutKind.Explicit)]
    public /*union*/ struct SenderFrameTypeData
    {
        // struct HostMemoryData
        [FieldOffset(0)]
        public /*uint8_t**/ IntPtr host_data;
        [FieldOffset(8)]
        public UInt32 host_stride;
        // struct Dx11Data
        [FieldOffset(0)]
        public /*struct ID3D11Resource**/ IntPtr dx11_resource;
        // struct Dx12Data
        [FieldOffset(0)]
        public /*struct ID3D12Resource**/ IntPtr dx12_resource;
        // struct OpenGlData
        [FieldOffset(0)]
        public /*GLuint**/ UInt32 gl_texture;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    public struct FrameRegion
    {
        public UInt32 xOffset;
        public UInt32 yOffset;
        public UInt32 width;
        public UInt32 height;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    public struct ProjectionClipping
    {
        public float left;
        public float right;
        public float top;
        public float bottom;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    public struct StreamDescription
    {
        public StreamHandle handle;
        [MarshalAs(UnmanagedType.LPStr)]
        public string channel;
        public UInt64 mappingId;
        public Int32 iViewpoint;
        [MarshalAs(UnmanagedType.LPStr)]
        public string name;
        public UInt32 width;
        public UInt32 height;
        public RSPixelFormat format;
        public ProjectionClipping clipping;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    public struct StreamDescriptions
    {
        public UInt32 nStreams;
        public /*StreamDescription**/ IntPtr streams;
    }
    
    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    public struct ProfilingEntry
    {
        public string name;
        public float value;
    }
    
    public enum RemoteParameterType : UInt32
    {
        RS_PARAMETER_NUMBER,
        RS_PARAMETER_IMAGE,
        RS_PARAMETER_POSE,      // 4x4 TR matrix
        RS_PARAMETER_TRANSFORM, // 4x4 TRS matrix
        RS_PARAMETER_TEXT,
    }

    public enum RemoteParameterDmxType : UInt32
    {
        RS_DMX_DEFAULT,
        RS_DMX_8,
        RS_DMX_16_BE,
    }

    [StructLayout(LayoutKind.Explicit)]
    public /*union*/ struct RemoteParameterTypeDefaults
    {
        [FieldOffset(0)]
        public float numerical_min;
        [FieldOffset(4)]
        public float numerical_max;
        [FieldOffset(8)]
        public float numerical_step;
        [FieldOffset(12)]
        public float numerical_defaultValue;
        [FieldOffset(0)]
        public /*const char**/ IntPtr text_defaultValue;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    public struct ImageFrameData
    {
        public UInt32 width;
        public UInt32 height;
        public RSPixelFormat format;
        public Int64 imageId;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    public struct RemoteParameter
    {
        [MarshalAs(UnmanagedType.LPStr)]
        public string group;
        [MarshalAs(UnmanagedType.LPStr)]
        public string displayName;
        [MarshalAs(UnmanagedType.LPStr)]
        public string key;
        public RemoteParameterType type;
        public RemoteParameterTypeDefaults defaults;
        public UInt32 nOptions;
        public /*const char***/ IntPtr options;

        public Int32 dmxOffset; // DMX channel offset or auto (-1)
        public RemoteParameterDmxType dmxType;
        public UInt32 flags; // REMOTEPARAMETER_FLAGS
    }

    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    public struct RemoteParameters
    {
        [MarshalAs(UnmanagedType.LPStr)]
        public string name;
        public UInt32 nParameters;
        public /*RemoteParameter**/ IntPtr parameters;
        public UInt64 hash;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    public struct Scenes
    {
        public UInt32 nScenes;
        public /*RemoteParameters**/ IntPtr scenes;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    public struct Channels
    {
        public UInt32 nChannels;
        public /*const char***/ IntPtr channels;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    public struct Schema
    {
        [MarshalAs(UnmanagedType.LPStr)] 
        public string engineName;
        [MarshalAs(UnmanagedType.LPStr)]
        public string engineVersion;
        [MarshalAs(UnmanagedType.LPStr)]
        public string info;
        public Channels channels;
        public Scenes scenes;
    }

    public class ManagedRemoteParameter
    {
        public string group;
        public string displayName;
        public string key;
        public RemoteParameterType type;
        public float min;
        public float max;
        public float step;
        public object defaultValue;
        public string[] options = { };

        public Int32 dmxOffset;
        public RemoteParameterDmxType dmxType;
    }

    public class ManagedRemoteParameters
    {
        public string name;
        public ManagedRemoteParameter[] parameters = { };
        public UInt64 hash;
    }

    public class ManagedSchema
    {
        public string[] channels = { };
        public ManagedRemoteParameters[] scenes = { };
    }
}