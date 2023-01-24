#include "PublicAPI.h"

#include "Unity/IUnityInterface.h"
#include "Unity/IUnityGraphics.h"

#include "Events.h"
#include "Logger.h"
#include "DX12System.h"
#include "DX12Texture.h"

using namespace NativeRenderingPlugin;

static IUnityInterfaces* s_UnityInterfaces = nullptr;
static IUnityGraphics* s_Graphics = nullptr;

// Unity plugin load event
extern "C" void UNITY_INTERFACE_EXPORT UNITY_INTERFACE_API
UnityPluginLoad(IUnityInterfaces * unityInterfaces)
{
    s_UnityInterfaces = unityInterfaces;
    s_Graphics = unityInterfaces->Get<IUnityGraphics>();
    s_Logger = std::make_unique<Logger>(unityInterfaces);

    if (s_Graphics != nullptr)
    {
        s_Graphics->RegisterDeviceEventCallback(OnGraphicsDeviceEvent);
    }

    // Run OnGraphicsDeviceEvent(initialize) manually on plugin load
    // to not miss the event in case the graphics device is already initialized
    OnGraphicsDeviceEvent(kUnityGfxDeviceEventInitialize);
}

// Unity plugin unload event
extern "C" void UNITY_INTERFACE_EXPORT UNITY_INTERFACE_API
UnityPluginUnload()
{
    if (s_Graphics != nullptr)
    {
        s_Graphics->UnregisterDeviceEventCallback(OnGraphicsDeviceEvent);
    }
}

// Always called on the main thread, even by IUnityGraphics 
static void UNITY_INTERFACE_API
OnGraphicsDeviceEvent(UnityGfxDeviceEventType eventType)
{
    switch (eventType)
    {
        case kUnityGfxDeviceEventInitialize:
        {
            if (s_Graphics != nullptr && s_Graphics->GetRenderer() == kUnityGfxRendererD3D12)
            {
                s_DX12System = std::make_unique<DX12System>(s_UnityInterfaces);
            }

            break;
        }
        case kUnityGfxDeviceEventShutdown:
        {
            s_DX12System.reset();

            break;
        }
        case kUnityGfxDeviceEventBeforeReset:
        {
            break;
        }
        case kUnityGfxDeviceEventAfterReset:
        {
            break;
        }
    };
}

// Render event (via IssuePluginEvent) callback
static void UNITY_INTERFACE_API
OnRenderEvent(int eventID, void* eventData)
{
    if (eventID == (int)NativeRenderingPlugin::EventID::GET_FRAME_IMAGE)
    {
        auto data = reinterpret_cast<const NativeRenderingPlugin::GetFrameImageData*>(eventData);
        auto result = data->Execute();
        if (result != RS_ERROR_SUCCESS)
        {
            s_Logger->LogError("EventID::GET_FRAME_IMAGE error", result);
        }
    }
    else
    {
        s_Logger->LogError("Unsupported event ID", eventID);
    }
}

extern "C" UnityRenderingEventAndData UNITY_INTERFACE_EXPORT UNITY_INTERFACE_API
GetRenderEventCallback()
{
    return OnRenderEvent;
}

extern "C" bool UNITY_INTERFACE_EXPORT
IsInitialized()
{
    return s_DX12System != nullptr && s_DX12System->IsInitialized();
}

extern "C" UNITY_INTERFACE_EXPORT void*
GetD3D12Device()
{
    if (!IsInitialized())
    {
        s_Logger->LogError("GetD3D12Device: called before successful initialization.");
        return nullptr;
    }

    return s_DX12System->GetDevice();
}

extern "C" UNITY_INTERFACE_EXPORT void*
GetD3D12CommandQueue()
{
    if (!IsInitialized())
    {
        s_Logger->LogError("GetD3D12CommandQueue: called before successful initialization.");
        return nullptr;
    }

    return s_DX12System->GetCommandQueue();
}

extern "C" UNITY_INTERFACE_EXPORT void*
CreateNativeTexture(const LPCWSTR name, int width, int height, int pixelFormat)
{
    if (!IsInitialized())
    {
        s_Logger->LogError("CreateNativeTexture: called before successful initialization.");
        return nullptr;
    }

    return CreateTexture(name, width, height, static_cast<PixelFormat>(pixelFormat));
}
