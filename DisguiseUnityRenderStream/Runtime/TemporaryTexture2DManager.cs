using System;
using System.Collections.Generic;
using Disguise.RenderStream.Utils;
using UnityEngine;
using UnityEngine.PlayerLoop;
using UnityEngine.Pool;

namespace Disguise.RenderStream
{
    public struct Texture2DDescriptor : IEquatable<Texture2DDescriptor>
    {
        public int Width;
        public int Height;
        public RSPixelFormat Format;
        public bool Linear;

        /// <summary>
        /// Disguise uses a black 1x1 placeholder texture.
        /// It's bound initially and during input parameter swapping.
        /// We treat it as a persistent texture.
        /// </summary>
        public bool IsPlaceholderTexture => Width == 1 && Height == 1;
        
        public override bool Equals(object obj)
        {
            return obj is Texture2DDescriptor other && Equals(other);
        }
        
        public override int GetHashCode()
        {
            return HashCode.Combine(
                Width,
                Height,
                (int)Format,
                Linear
            );
        }

        public bool Equals(Texture2DDescriptor other)
        {
            return
                Width == other.Width &&
                Height == other.Height &&
                Format == other.Format &&
                Linear == other.Linear;
        }
            
        public static bool operator ==(Texture2DDescriptor lhs, Texture2DDescriptor rhs) => lhs.Equals(rhs);

        public static bool operator !=(Texture2DDescriptor lhs, Texture2DDescriptor rhs) => !(lhs == rhs);

        public override string ToString()
        {
            return $"{Width}x{Height} Format {Format} {(Linear ? "Linear" : "SRGB")}";
        }
    }

    /// <summary>
    /// Provides texture re-use across a frame and texture lifetime management.
    /// Similar to <see cref="RenderTexture.GetTemporary(RenderTextureDescriptor)"/>.
    ///
    /// <remarks>Lifetime doesn't grow during frames where no textures from the pool were used.</remarks>
    /// </summary>
    public class TemporaryTexture2DManager
    {
        struct FinishFrameRendering { }
        
        class Item
        {
            public readonly Texture2D Texture;
            public int NumFramesSinceAccess;

            public Item(Texture2D texture)
            {
                Texture = texture;
                NumFramesSinceAccess = 0;
            }

            public void MarkAccess()
            {
                NumFramesSinceAccess = 0;
            }
            
            public void Update()
            {
                NumFramesSinceAccess++;
            }
        }
        
        static TemporaryTexture2DManager s_Instance;

        public static TemporaryTexture2DManager Instance
        {
            get
            {
                if (s_Instance == null)
                    s_Instance = new TemporaryTexture2DManager();
                return s_Instance;
            }
        }
        
        // Same delay as in RenderTexture.GetTemporary()
        const int k_FramesToWaitBeforeReleasing = 15;

        /// <summary>
        /// Useful for tracing Disguise texture parameters.
        /// </summary>
        public bool DebugTrace { get; set; }

        readonly Dictionary<Texture2DDescriptor, Item> m_Items = new Dictionary<Texture2DDescriptor, Item>();
        bool m_WasAccessedThisFrame;

        TemporaryTexture2DManager()
        {
            PlayerLoopExtensions.RegisterUpdate<PostLateUpdate.FinishFrameRendering, FinishFrameRendering>(OnFinishFrameRendering);
        }

        public Texture2D Get(Texture2DDescriptor descriptor)
        {
            m_WasAccessedThisFrame = true;
            
            if (m_Items.TryGetValue(descriptor, out var item))
            {
                Debug.Assert(item.Texture != null);
                
                item.MarkAccess();
                return item.Texture;
            }
            else
            {
                var texture = CreateTexture(descriptor);
                var newItem = new Item(texture);
                m_Items.Add(descriptor, newItem);
                return texture;
            }
        }

        void OnFinishFrameRendering()
        {
            var texturesToRelease = ListPool<Texture2DDescriptor>.Get();

            foreach (var (key, item) in m_Items)
            {
                if (ShouldRelease(key, item))
                {
                    texturesToRelease.Add(key);
                }
                else if (m_WasAccessedThisFrame)
                {
                    item.Update();
                }
            }

            foreach (var textureToRelease in texturesToRelease)
            {
                Release(textureToRelease);
            }

            ListPool<Texture2DDescriptor>.Release(texturesToRelease);
            m_WasAccessedThisFrame = false;
        }

        bool ShouldRelease(Texture2DDescriptor descriptor, Item item)
        {
            return !descriptor.IsPlaceholderTexture && item.NumFramesSinceAccess >= k_FramesToWaitBeforeReleasing;
        }

        void Release(Texture2DDescriptor descriptor)
        {
            if (m_Items.Remove(descriptor, out var item))
            {
                DestroyTexture(item.Texture);

                if (DebugTrace)
                {
                    DebugLog($"Released texture: {descriptor}");
                }
            }
            else
            {
                Debug.Assert(false);
            }
        }

        Texture2D CreateTexture(Texture2DDescriptor descriptor)
        {
            if (DebugTrace)
            {
                DebugLog($"Created texture: {descriptor}");
            }

            return DisguiseTextures.CreateTexture(descriptor.Width, descriptor.Height, descriptor.Format, descriptor.Linear, null);
        }

        void DestroyTexture(Texture2D texture)
        {
#if UNITY_EDITOR
            UnityEngine.Object.DestroyImmediate(texture);
#else
            UnityEngine.Object.Destroy(texture);
#endif
        }

        void DebugLog(string message)
        {
            Debug.Log($"Texture2DPool: {message}");
        }
    }
}
