using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace Disguise.RenderStream
{
    static class DisguiseTextures
    {
        // Should match PluginEntry.ToTextureFormat
        static NativeRenderingPlugin.PixelFormat ToNativeRenderingPluginFormat(RSPixelFormat rsPixelFormat)
        {
            switch (rsPixelFormat)
            {
                case RSPixelFormat.RS_FMT_BGRA8: return NativeRenderingPlugin.PixelFormat.BGRA8;
                case RSPixelFormat.RS_FMT_BGRX8: return NativeRenderingPlugin.PixelFormat.BGRX8;
                case RSPixelFormat.RS_FMT_RGBA32F: return NativeRenderingPlugin.PixelFormat.RGBA32F;
                case RSPixelFormat.RS_FMT_RGBA16: return NativeRenderingPlugin.PixelFormat.RGBA16;
                case RSPixelFormat.RS_FMT_RGBA8: return NativeRenderingPlugin.PixelFormat.RGBA8;
                case RSPixelFormat.RS_FMT_RGBX8: return NativeRenderingPlugin.PixelFormat.RGBA8;
                default: throw new ArgumentOutOfRangeException();
            }
        }
        
        public static Texture2D CreateTexture(int width, int height, RSPixelFormat format, string name)
        {
            Texture2D texture = null;
            
            switch (PluginEntry.instance.GraphicsDeviceType)
            {
                case GraphicsDeviceType.Direct3D11:
                    texture = new Texture2D(width, height, PluginEntry.ToTextureFormat(format), false, false);
                    break;
                
                case GraphicsDeviceType.Direct3D12:
                    RS_ERROR error = PluginEntry.instance.useDX12SharedHeapFlag(out var heapFlag);
                    if (error != RS_ERROR.RS_ERROR_SUCCESS)
                        Debug.LogError(string.Format("Error checking shared heap flag: {0}", error));

                    if (heapFlag == UseDX12SharedHeapFlag.RS_DX12_USE_SHARED_HEAP_FLAG)
                    {
                        var nativeTex = NativeRenderingPlugin.CreateNativeTexture(name, width, height, ToNativeRenderingPluginFormat(format));
                        texture = Texture2D.CreateExternalTexture(width, height, PluginEntry.ToTextureFormat(format), false, false, nativeTex);
                        break;
                    }
                    else
                    {
                        texture = new Texture2D(width, height, PluginEntry.ToTextureFormat(format), false, false);
                        break;
                    }
            }

            if (texture != null)
            {
                texture.name = name;
            }
            
            return texture;
        }
        
        public static Texture2D ResizeTexture(Texture2D texture, int newWidth, int newHeight, RSPixelFormat format)
        {
            switch (PluginEntry.instance.GraphicsDeviceType)
            {
                case GraphicsDeviceType.Direct3D11:
                    texture.Reinitialize(newWidth, newHeight, texture.format, false);
                    return texture;
                
                case GraphicsDeviceType.Direct3D12:
                    RS_ERROR error = PluginEntry.instance.useDX12SharedHeapFlag(out var heapFlag);
                    if (error != RS_ERROR.RS_ERROR_SUCCESS)
                        Debug.LogError(string.Format("Error checking shared heap flag: {0}", error));

                    if (heapFlag == UseDX12SharedHeapFlag.RS_DX12_USE_SHARED_HEAP_FLAG)
                    {
                        var nativeTex = NativeRenderingPlugin.CreateNativeTexture(texture.name, newWidth, newHeight, ToNativeRenderingPluginFormat(format));
                        return Texture2D.CreateExternalTexture(newWidth, newHeight, PluginEntry.ToTextureFormat(format), false, false, nativeTex);
                    }
                    else
                    {
                        texture.Reinitialize(newWidth, newHeight, texture.format, false);
                        return texture;
                    }
            }

            return null;
        }
    }
}