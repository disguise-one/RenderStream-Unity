#pragma once
#include "d3d12.h"

#include "Disguise/d3renderstream.h"

#include "Logger.h"
#include "DX12System.h"

namespace NativeRenderingPlugin
{
    DXGI_FORMAT ToDXFormat(RSPixelFormat pixelFormat, bool sRGB)
    {
        switch (pixelFormat)
        {
        case RSPixelFormat::RS_FMT_BGRA8:
        case RSPixelFormat::RS_FMT_BGRX8:
            return sRGB ? DXGI_FORMAT_B8G8R8A8_UNORM_SRGB : DXGI_FORMAT_B8G8R8A8_UNORM;

        case RSPixelFormat::RS_FMT_RGBA32F:
            return DXGI_FORMAT_R32G32B32A32_FLOAT;

        case RSPixelFormat::RS_FMT_RGBA16:
            return DXGI_FORMAT_R16G16B16A16_UNORM;

        case RSPixelFormat::RS_FMT_RGBA8:
        case RSPixelFormat::RS_FMT_RGBX8:
            return sRGB ? DXGI_FORMAT_R8G8B8A8_UNORM_SRGB : DXGI_FORMAT_R8G8B8A8_UNORM;

        case RSPixelFormat::RS_FMT_INVALID:
        default:
            return DXGI_FORMAT_UNKNOWN;
        }
    }

    const D3D12_HEAP_PROPERTIES D3D12_DEFAULT_HEAP_PROPS = {
        D3D12_HEAP_TYPE_DEFAULT, D3D12_CPU_PAGE_PROPERTY_UNKNOWN, D3D12_MEMORY_POOL_UNKNOWN, 0, 0
    };

    ID3D12Resource* CreateTexture(const LPCWSTR name, int width, int height, RSPixelFormat pixelFormat, bool sRGB)
    {
        const DXGI_FORMAT dxFormat = ToDXFormat(pixelFormat, sRGB);
        if (dxFormat == DXGI_FORMAT_UNKNOWN)
        {
            s_Logger->LogError("Unsupported PixelFormat: ", pixelFormat);
            return nullptr;
        }

        D3D12_RESOURCE_DESC desc{};
        desc.Dimension = D3D12_RESOURCE_DIMENSION_TEXTURE2D;
        desc.Alignment = 0;
        desc.Width = width;
        desc.Height = height;
        desc.DepthOrArraySize = 1;
        desc.MipLevels = 1;
        desc.Format = dxFormat;
        desc.SampleDesc.Count = 1;
        desc.SampleDesc.Quality = 0;
        desc.Layout = D3D12_TEXTURE_LAYOUT_UNKNOWN;
        desc.Flags = D3D12_RESOURCE_FLAG_ALLOW_SIMULTANEOUS_ACCESS | D3D12_RESOURCE_FLAG_ALLOW_RENDER_TARGET;

        const D3D12_HEAP_FLAGS flags = D3D12_HEAP_FLAG_SHARED;
        const D3D12_RESOURCE_STATES initialState = D3D12_RESOURCE_STATE_COPY_DEST;

        ID3D12Resource* resource = nullptr;
        HRESULT result = s_DX12System->GetDevice()->CreateCommittedResource(
            &D3D12_DEFAULT_HEAP_PROPS, flags, &desc, initialState, nullptr, IID_PPV_ARGS(&resource));

        if (result != S_OK)
        {
            s_Logger->LogError("CreateTexture: CreateCommittedResource failed: ", result);
            return nullptr;
        }

        if (name != nullptr)
        {
            resource->SetName(name);
        }

        return resource;
    }
}
