#include "PublicAPI.h"

#include "Unity/IUnityInterface.h"
#include "Unity/IUnityGraphics.h"

class IDXGISwapChain;
#include "d3d12.h"
#include "Unity/IUnityGraphicsD3D12.h"

static IUnityInterfaces* s_UnityInterfaces = nullptr;
static IUnityGraphics* s_Graphics = nullptr;
static UnityGfxRenderer s_RendererType = kUnityGfxRendererNull;

static bool s_IsInitialized = false;
static IUnityGraphicsD3D12v5* s_D3D12UnityGraphics = nullptr;
static ID3D12Device* s_D3D12Device = nullptr;
static ID3D12CommandQueue* s_D3D12CommandQueue = nullptr;

// Unity plugin load event
extern "C" void UNITY_INTERFACE_EXPORT UNITY_INTERFACE_API
UnityPluginLoad(IUnityInterfaces * unityInterfaces)
{
    s_UnityInterfaces = unityInterfaces;
    s_Graphics = unityInterfaces->Get<IUnityGraphics>();

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
                    s_D3D12UnityGraphics = s_UnityInterfaces->Get<IUnityGraphicsD3D12v5>();

                    if (s_D3D12UnityGraphics != nullptr)
                    {
                        s_D3D12Device = s_D3D12UnityGraphics->GetDevice();
                        s_D3D12CommandQueue = s_D3D12UnityGraphics->GetCommandQueue();
                        s_IsInitialized = s_D3D12Device != nullptr && s_D3D12CommandQueue != nullptr;
                    }
                }
            }

            break;
        }
        case kUnityGfxDeviceEventShutdown:
        {
            s_RendererType = kUnityGfxRendererNull;

            s_IsInitialized = false;
            s_D3D12UnityGraphics = nullptr;
            s_D3D12Device = nullptr;
            s_D3D12CommandQueue = nullptr;

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

extern "C" UNITY_INTERFACE_EXPORT bool IsInitialized()
{
    return s_IsInitialized;
}

extern "C" UNITY_INTERFACE_EXPORT void* GetD3D12Device()
{
    return s_D3D12Device;
}

extern "C" UNITY_INTERFACE_EXPORT void* GetD3D12CommandQueue()
{
    return s_D3D12CommandQueue;
}
