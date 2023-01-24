#ifndef D3RENDERSTREAM_H
#define D3RENDERSTREAM_H

#include <stdint.h>


enum RSPixelFormat : uint32_t
{
    RS_FMT_INVALID,

    RS_FMT_BGRA8,
    RS_FMT_BGRX8,

    RS_FMT_RGBA32F,

    RS_FMT_RGBA16,

    RS_FMT_RGBA8,
    RS_FMT_RGBX8,
};

struct ID3D11Device;
struct ID3D11Resource;
struct ID3D12Resource;
struct ID3D12Fence;
typedef unsigned int GLuint;
struct ID3D12Device;
struct ID3D12CommandQueue;
typedef struct VkDevice_T* VkDevice;
typedef struct VkDeviceMemory_T* VkDeviceMemory;
typedef uint64_t VkDeviceSize;
typedef struct VkSemaphore_T* VkSemaphore;
// Forward declare Windows compatible handles.
#define D3_DECLARE_HANDLE(name) \
  struct name##__;                  \
  typedef struct name##__* name
D3_DECLARE_HANDLE(HGLRC);
D3_DECLARE_HANDLE(HDC);

enum RS_ERROR
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
};

// Bitmask flags
enum FRAMEDATA_FLAGS
{
    FRAMEDATA_NO_FLAGS = 0,
    FRAMEDATA_RESET = 1
};

enum REMOTEPARAMETER_FLAGS
{
    REMOTEPARAMETER_NO_FLAGS = 0,
    REMOTEPARAMETER_NO_SEQUENCE = 1,
    REMOTEPARAMETER_READ_ONLY = 2
};

typedef uint64_t StreamHandle;
typedef uint64_t CameraHandle;
typedef void (*logger_t)(const char*);

#pragma pack(push, 4)
typedef struct
{
    float virtualZoomScale;
    uint8_t virtualReprojectionRequired;
    float xRealCamera, yRealCamera, zRealCamera;
    float rxRealCamera, ryRealCamera, rzRealCamera;
} D3TrackingData;  // Tracking data required by d3 but not used to render content

typedef struct
{
    StreamHandle id;
    CameraHandle cameraHandle;
    float x, y, z;
    float rx, ry, rz;
    float focalLength;
    float sensorX, sensorY;
    float cx, cy;
    float nearZ, farZ;
    float orthoWidth;  // If > 0, an orthographic camera should be used
    D3TrackingData d3Tracking;
} CameraData;

typedef struct
{
    double tTracked;
    double localTime;
    double localTimeDelta;
    unsigned int frameRateNumerator;
    unsigned int frameRateDenominator;
    uint32_t flags; // FRAMEDATA_FLAGS
    uint32_t scene;
} FrameData;

typedef struct
{
    double tTracked;
    CameraData camera;
} CameraResponseData;


typedef struct
{
    uint8_t* data;
    uint32_t stride;
} HostMemoryData;

typedef struct
{
    ID3D11Resource* resource;
} Dx11Data;

typedef struct
{
    ID3D12Resource* resource;
} Dx12Data;

typedef struct
{
    GLuint texture;
} OpenGlData;

typedef struct
{
    VkDeviceMemory memory;
    VkDeviceSize size;
    RSPixelFormat format;
    uint32_t width;
    uint32_t height;
    VkSemaphore waitSemaphore;
    uint64_t waitSemaphoreValue;
    VkSemaphore signalSemaphore;
    uint64_t signalSemaphoreValue;
} VulkanDataStructure;

typedef struct
{
    VulkanDataStructure* image;
} VulkanData;

typedef union
{
    HostMemoryData cpu;
    Dx11Data dx11;
    Dx12Data dx12;
    OpenGlData gl;
    VulkanData vk;
} SenderFrameTypeData;

typedef struct
{
    uint32_t xOffset;
    uint32_t yOffset;
    uint32_t width;
    uint32_t height;
} FrameRegion;

// Normalised (0-1) clipping planes for the edges of the camera frustum, to be used to perform off-axis perspective projection, or
// to offset and scale 2D orthographic matrices.
typedef struct
{
    float left;
    float right;
    float top;
    float bottom;
} ProjectionClipping;

typedef struct
{
    StreamHandle handle;
    const char* channel;
    uint64_t mappingId;
    int32_t iViewpoint;
    const char* name;
    uint32_t width;
    uint32_t height;
    RSPixelFormat format;
    ProjectionClipping clipping;
} StreamDescription;

typedef struct
{
    uint32_t nStreams;
    StreamDescription* streams;
} StreamDescriptions;

enum RemoteParameterType
{
    RS_PARAMETER_NUMBER,
    RS_PARAMETER_IMAGE,
    RS_PARAMETER_POSE,      // 4x4 TR matrix
    RS_PARAMETER_TRANSFORM, // 4x4 TRS matrix
    RS_PARAMETER_TEXT,
};

enum RemoteParameterDmxType
{
    RS_DMX_DEFAULT,
    RS_DMX_8,
    RS_DMX_16_BE,
};

typedef struct
{
    float min;
    float max;
    float step;
    float defaultValue;
} NumericalDefaults;

typedef struct
{
    const char* defaultValue;
} TextDefaults;

typedef union
{
    NumericalDefaults number;
    TextDefaults text;
} RemoteParameterTypeDefaults;

typedef struct
{
    uint32_t width, height;
    RSPixelFormat format;
    int64_t imageId;
} ImageFrameData;

typedef struct
{
    const char* group;
    const char* displayName;
    const char* key;
    RemoteParameterType type;
    RemoteParameterTypeDefaults defaults;
    uint32_t nOptions;
    const char** options;

    int32_t dmxOffset; // DMX channel offset or auto (-1)
    RemoteParameterDmxType dmxType;
    uint32_t flags; // REMOTEPARAMETER_FLAGS
} RemoteParameter;

typedef struct
{
    const char* name;
    uint32_t nParameters;
    RemoteParameter* parameters;
    uint64_t hash;
} RemoteParameters;

typedef struct
{
    uint32_t nScenes;
    RemoteParameters* scenes;
} Scenes;

typedef struct
{
    uint32_t nChannels;
    const char** channels;
} Channels;

typedef struct
{
    const char* engineName;
    const char* engineVersion;
    const char* info;
    Channels channels;
    Scenes scenes;
} Schema;

typedef struct
{
    const char* name;
    float value;
} ProfilingEntry;

#pragma pack(pop)

#define D3_RENDER_STREAM_API __declspec( dllexport )

#define RENDER_STREAM_VERSION_MAJOR 1
#define RENDER_STREAM_VERSION_MINOR 30

#define RENDER_STREAM_VERSION_STRING stringify(RENDER_STREAM_VERSION_MAJOR) "." stringify(RENDER_STREAM_VERSION_MINOR)

enum SenderFrameType
{
    RS_FRAMETYPE_HOST_MEMORY,
    RS_FRAMETYPE_DX11_TEXTURE,
    RS_FRAMETYPE_DX12_TEXTURE,
    RS_FRAMETYPE_OPENGL_TEXTURE,
    RS_FRAMETYPE_VULKAN_TEXTURE,
    RS_FRAMETYPE_UNKNOWN
};

enum UseDX12SharedHeapFlag
{
    RS_DX12_USE_SHARED_HEAP_FLAG,
    RS_DX12_DO_NOT_USE_SHARED_HEAP_FLAG
};

typedef struct
{
    const CameraResponseData* cameraData;
    uint64_t schemaHash;
    uint32_t parameterDataSize;
    void* parameterData;
    uint32_t textDataCount;
    const char** textData;
} FrameResponseData;

// isolated functions, do not require init prior to use
extern "C" D3_RENDER_STREAM_API void rs_registerLoggingFunc(logger_t logger);
extern "C" D3_RENDER_STREAM_API void rs_registerErrorLoggingFunc(logger_t logger);
extern "C" D3_RENDER_STREAM_API void rs_registerVerboseLoggingFunc(logger_t logger);

extern "C" D3_RENDER_STREAM_API void rs_unregisterLoggingFunc();
extern "C" D3_RENDER_STREAM_API void rs_unregisterErrorLoggingFunc();
extern "C" D3_RENDER_STREAM_API void rs_unregisterVerboseLoggingFunc();

extern "C" D3_RENDER_STREAM_API RS_ERROR rs_initialise(int expectedVersionMajor, int expectedVersionMinor);
extern "C" D3_RENDER_STREAM_API RS_ERROR rs_initialiseGpGpuWithoutInterop(ID3D11Device* device);
extern "C" D3_RENDER_STREAM_API RS_ERROR rs_initialiseGpGpuWithDX11Device(ID3D11Device* device);
extern "C" D3_RENDER_STREAM_API RS_ERROR rs_initialiseGpGpuWithDX11Resource(ID3D11Resource* resource);
extern "C" D3_RENDER_STREAM_API RS_ERROR rs_initialiseGpGpuWithDX12DeviceAndQueue(ID3D12Device* device, ID3D12CommandQueue* queue);
extern "C" D3_RENDER_STREAM_API RS_ERROR rs_initialiseGpGpuWithOpenGlContexts(HGLRC glContext, HDC deviceContext);
extern "C" D3_RENDER_STREAM_API RS_ERROR rs_initialiseGpGpuWithVulkanDevice(VkDevice device);
extern "C" D3_RENDER_STREAM_API RS_ERROR rs_shutdown();

// non-isolated functions, these require init prior to use

extern "C" D3_RENDER_STREAM_API RS_ERROR rs_useDX12SharedHeapFlag(UseDX12SharedHeapFlag * flag);
extern "C" D3_RENDER_STREAM_API RS_ERROR rs_saveSchema(const char* assetPath, Schema* schema); // Save schema for project file/custom executable at (assetPath)
extern "C" D3_RENDER_STREAM_API RS_ERROR rs_loadSchema(const char* assetPath, /*Out*/Schema* schema, /*InOut*/uint32_t* nBytes); // Load schema for project file/custom executable at (assetPath) into a buffer of size (nBytes) starting at (schema)

// workload functions, these require the process to be running inside d3's asset launcher environment

extern "C" D3_RENDER_STREAM_API RS_ERROR rs_setSchema(/*InOut*/Schema* schema); // Set schema and fill in per-scene hash for use with rs_getFrameParameters

extern "C" D3_RENDER_STREAM_API RS_ERROR rs_getStreams(/*Out*/StreamDescriptions* streams, /*InOut*/uint32_t* nBytes); // Populate streams into a buffer of size (nBytes) starting at (streams)

extern "C" D3_RENDER_STREAM_API RS_ERROR rs_awaitFrameData(int timeoutMs, /*Out*/FrameData * data);  // waits for any asset, any stream to request a frame, provides the parameters for that frame.
extern "C" D3_RENDER_STREAM_API RS_ERROR rs_setFollower(int isFollower); // Used to mark this node as relying on alternative mechanisms to distribute FrameData. Users must provide correct CameraResponseData to sendFrame, and call rs_beginFollowerFrame at the start of the frame, where awaitFrame would normally be called.
extern "C" D3_RENDER_STREAM_API RS_ERROR rs_beginFollowerFrame(double tTracked); // Pass the engine-distributed tTracked value in, if you have called rs_setFollower(1) otherwise do not call this function.

extern "C" D3_RENDER_STREAM_API RS_ERROR rs_getFrameParameters(uint64_t schemaHash, /*Out*/void* outParameterData, uint64_t outParameterDataSize);  // returns the remote parameters for this frame.
extern "C" D3_RENDER_STREAM_API RS_ERROR rs_getFrameImageData(uint64_t schemaHash, /*Out*/ImageFrameData* outParameterData, uint64_t outParameterDataCount);  // returns the remote image data for this frame.
extern "C" D3_RENDER_STREAM_API RS_ERROR rs_getFrameImage(int64_t imageId, SenderFrameType frameType, /*InOut*/SenderFrameTypeData data); // fills in (data) with the remote image
extern "C" D3_RENDER_STREAM_API RS_ERROR rs_getFrameText(uint64_t schemaHash, uint32_t textParamIndex, /*Out*/const char** outTextPtr); // // returns the remote text data (pointer only valid until next rs_awaitFrameData)

extern "C" D3_RENDER_STREAM_API RS_ERROR rs_getFrameCamera(StreamHandle streamHandle, /*Out*/CameraData* outCameraData);  // returns the CameraData for this stream, or RS_ERROR_NOTFOUND if no camera data is available for this stream on this frame
extern "C" D3_RENDER_STREAM_API RS_ERROR rs_sendFrame(StreamHandle streamHandle, SenderFrameType frameType, SenderFrameTypeData data, const FrameResponseData* frameData); // publish a frame which was generated from the associated tracking and timing information.

extern "C" D3_RENDER_STREAM_API RS_ERROR rs_releaseImage(SenderFrameType frameType, SenderFrameTypeData data); // release any references to image (e.g. before deletion)

extern "C" D3_RENDER_STREAM_API RS_ERROR rs_logToD3(const char* str); // Transmit log message over network. New line automatically appended
extern "C" D3_RENDER_STREAM_API RS_ERROR rs_sendProfilingData(ProfilingEntry * entries, int count);
extern "C" D3_RENDER_STREAM_API RS_ERROR rs_setNewStatusMessage(const char* msg);

#endif
