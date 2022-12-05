#pragma once
#include "d3d12.h"

#include "Logger.h"
#include "DX12System.h"
#include "Utility.h"

enum PixelFormat
{
    Invalid,
    BGRA8,
    BGRX8,
    RGBA32F,
    RGBA16,
    RGBA8,
    RGBX8,
};

bool ToDXFormat(PixelFormat pixelFormat, DXGI_FORMAT& dxFormat)
{
    switch (pixelFormat)
    {
    case BGRA8:
        dxFormat = DXGI_FORMAT_B8G8R8A8_UNORM;
        return true;
        break;

    case BGRX8:
        dxFormat = DXGI_FORMAT_B8G8R8A8_UNORM;
        return true;
        break;

    case RGBA32F:
        dxFormat = DXGI_FORMAT_R32G32B32A32_FLOAT;
        return true;
        break;

    case RGBA16:
        dxFormat = DXGI_FORMAT_R32G32B32A32_FLOAT;
        return true;
        break;

    case RGBA8:
        dxFormat = DXGI_FORMAT_R8G8B8A8_UNORM;
        return true;
        break;

    case RGBX8:
        dxFormat = DXGI_FORMAT_R8G8B8A8_UNORM;
        return true;
        break;

    case Invalid:
    default:
        return false;
        break;
    }
}

const D3D12_HEAP_PROPERTIES D3D12_DEFAULT_HEAP_PROPS = {
    D3D12_HEAP_TYPE_DEFAULT, D3D12_CPU_PAGE_PROPERTY_UNKNOWN, D3D12_MEMORY_POOL_UNKNOWN, 0, 0
};

ID3D12Resource* CreateTexture(const char* name, int width, int height, PixelFormat pixelFormat)
{
    DXGI_FORMAT dxFormat;
    if (!ToDXFormat(pixelFormat, dxFormat))
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
        resource->SetName(StrToWstr(name).c_str());
    }

    return resource;
}
