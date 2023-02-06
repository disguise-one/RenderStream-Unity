#pragma once
#include <d3d11.h>
#include <d3d12.h>
#include <wrl/client.h> // For ComPtr

#include "Disguise/d3renderstream.h"

#include "Logger.h"

namespace NativeRenderingPlugin
{
    // Render thread event IDs
    // Should match EventID (NativeRenderingPlugin.cs)
    enum class EventID
    {
        INPUT_IMAGE,
    };

    typedef RS_ERROR(*t_rs_getFrameImage)(int64_t imageId, SenderFrameType frameType, SenderFrameTypeData data);

    // EventID::INPUT_IMAGE data structure
    // Should match InputImageData (NativeRenderingPlugin.cs)
    struct InputImageData
    {
        t_rs_getFrameImage m_rs_getFrameImage;  // Function pointer into Disguise DLL
        int64_t m_ImageID;
        IUnknown* m_Texture;                    // ID3D11Texture or ID3D12Resource

        RS_ERROR Execute() const
        {
            if (m_Texture == nullptr)
            {
                s_Logger->LogError("InputImageData null texture pointer");
                return RS_ERROR::RS_ERROR_INVALID_PARAMETERS;
            }

            SenderFrameType senderType = RS_FRAMETYPE_UNKNOWN;
            SenderFrameTypeData senderData = {};

            Microsoft::WRL::ComPtr<ID3D11Resource> dx11Resource;
            Microsoft::WRL::ComPtr<ID3D12Resource> dx12Resource;

            if (m_Texture->QueryInterface(IID_ID3D11Resource, &dx11Resource) == S_OK)
            {
                senderType = RS_FRAMETYPE_DX11_TEXTURE;
                senderData.dx11.resource = dx11Resource.Get();
            }
            else if (m_Texture->QueryInterface(IID_ID3D12Resource, &dx12Resource) == S_OK)
            {
                senderType = RS_FRAMETYPE_DX12_TEXTURE;
                senderData.dx12.resource = dx12Resource.Get();
            }
            else
            {
                s_Logger->LogError("InputImageData unknown texture type");
                return RS_ERROR::RS_ERROR_INVALID_PARAMETERS;
            }

            return m_rs_getFrameImage(m_ImageID, senderType, senderData);
        }
    };
}
