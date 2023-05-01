using System;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;

namespace Disguise.RenderStream
{
    static class DisguiseTextures
    {
        public static Texture2D CreateTexture(int width, int height, RSPixelFormat format, bool sRGB, string name)
        {
            Texture2D texture = null;
            
            switch (PluginEntry.instance.GraphicsDeviceType)
            {
                case GraphicsDeviceType.Direct3D11:
                    texture = new Texture2D(width, height, PluginEntry.ToGraphicsFormat(format, sRGB), 1, TextureCreationFlags.None);
                    break;
                
                case GraphicsDeviceType.Direct3D12:
                    RS_ERROR error = PluginEntry.instance.useDX12SharedHeapFlag(out var heapFlag);
                    if (error != RS_ERROR.RS_ERROR_SUCCESS)
                        Debug.LogError(string.Format("Error checking shared heap flag: {0}", error));

                    if (heapFlag == UseDX12SharedHeapFlag.RS_DX12_USE_SHARED_HEAP_FLAG)
                    {
                        var nativeTex = NativeRenderingPlugin.CreateNativeTexture(name, width, height, format, sRGB);
                        var graphicsFormat = PluginEntry.ToGraphicsFormat(format, sRGB);
                        var textureFormat = GraphicsFormatUtility.GetTextureFormat(graphicsFormat);
                        texture = Texture2D.CreateExternalTexture(width, height, textureFormat, false, !sRGB, nativeTex);
                        
                        break;
                    }
                    else
                    {
                        texture = new Texture2D(width, height, PluginEntry.ToGraphicsFormat(format, sRGB), 1, TextureCreationFlags.None);
                        break;
                    }
            }

            if (texture != null)
            {
                texture.hideFlags = HideFlags.HideAndDontSave;
                texture.name = name;
            }
            
            return texture;
        }
    }
}
