#include "PublicAPI.h"

#include <mutex>

#include "Unity/IUnityInterface.h"
#include "Unity/IUnityGraphics.h"

#include "Logger.h"
#include "DX12System.h"
#include "DX12Texture.h"

static IUnityInterfaces* s_UnityInterfaces = nullptr;
static IUnityGraphics* s_Graphics = nullptr;
static UnityGfxRenderer s_RendererType = kUnityGfxRendererNull;

// Local mutex object used to prevent race conditions between the main thread
// and the render thread. This should be locked at the following points:
// - OnRenderEvent (this is the only point called from the render thread)
// - Plugin functions that use the Spout API functions.
std::mutex s_Lock;

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

static void UNITY_INTERFACE_API
OnGraphicsDeviceEvent(UnityGfxDeviceEventType eventType)
{
    switch (eventType)
    {
        case kUnityGfxDeviceEventInitialize:
        {
            if (s_Graphics != nullptr)
            {
                s_RendererType = s_Graphics->GetRenderer();

                if (s_RendererType == kUnityGfxRendererD3D12)
                {
                    s_DX12System = std::make_unique<DX12System>(s_UnityInterfaces);
                }
            }

            break;
        }
        case kUnityGfxDeviceEventShutdown:
        {
            s_RendererType = kUnityGfxRendererNull;
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

void UNITY_INTERFACE_API
OnRenderEvent(int eventId, void* eventData)
{
    std::lock_guard<std::mutex> guard(s_Lock);

    // TODO

    /*auto data = reinterpret_cast<const EventData*>(eventData);

    switch (eventId)
    {

    }

    if (event_id == event_updateSender) data->sender->update(data->texture);
    if (event_id == event_closeSender) delete data->sender;*/
}

extern "C" UnityRenderingEventAndData UNITY_INTERFACE_EXPORT
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
    return s_DX12System->GetDevice();
}

extern "C" UNITY_INTERFACE_EXPORT void*
GetD3D12CommandQueue()
{
    return s_DX12System->GetCommandQueue();
}

extern "C" void UNITY_INTERFACE_EXPORT *
CreateNativeTexture(const char* name, int width, int height, int pixelFormat)
{
    return CreateTexture(name, width, height, static_cast<PixelFormat>(pixelFormat));
}

extern "C" DX12Texture UNITY_INTERFACE_EXPORT *
CreateTexture(const char* name, int width, int height, int pixelFormat)
{
    return new DX12Texture(name, width, height, static_cast<PixelFormat>(pixelFormat));
}

//extern "C" UNITY_INTERFACE_EXPORT void* GetD3D12Device()
//{
//    return s_D3D12Device;
//}
