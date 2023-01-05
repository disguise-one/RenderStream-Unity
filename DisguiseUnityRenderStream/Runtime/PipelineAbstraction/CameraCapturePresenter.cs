using System;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.Rendering;

namespace Disguise.RenderStream
{
    /// <summary>
    /// <para>
    /// Blits this <see cref="GameObject"/>'s <see cref="CameraCapture"/> to the local screen.
    /// A number of strategies are available to handle the size and aspect ratio differences between the two surfaces.
    /// </para>
    ///
    /// <para>
    /// <see cref="PresenterInput"/> is responsible for adjusting the <see cref="UnityEngine.EventSystems.EventSystem"/>
    /// mouse coordinates to account for the blit.
    /// </para>
    /// </summary>
    class CameraCapturePresenter : Presenter
    {
        /// <summary>
        /// Describes which texture to present.
        /// </summary>
        public enum Mode
        {
            Color,
            Depth
        }
        
        /// <summary>
        /// Describes when to blit to screen.
        /// </summary>
        public enum PresentMode
        {
            /// <summary>
            /// Triggered after camera capture finishes.
            /// This is defined by the <see cref="P:CameraCapture.captureMode"/> of the sibling component.
            /// </summary>
            CameraCapture,
            
            /// <summary>
            /// <para>
            /// Triggered after Unity finishes all of its rendering (including UI overlays).
            /// This corresponds to the "PlayerEndOfFrame" graphics profiler tag.
            /// </para>
            ///
            /// <remarks>
            /// The blit will overwrite any Overlay UIs if there are any in the scene.
            /// </remarks>
            /// </summary>
            FrameEnd
        }

        [SerializeField]
        Mode m_mode;
        
        [SerializeField]
        PresentMode m_presentMode;
        
        CameraCapture m_cameraCapture;
        
        protected override void OnEnable()
        {
            base.OnEnable();

            m_cameraCapture = GetComponent<CameraCapture>();
            m_cameraCapture.onTexturesReady += OnTexturesReady;
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            
            if (m_cameraCapture != null)
                m_cameraCapture.onTexturesReady -= OnTexturesReady;
        }

        protected override void Update()
        {
            base.Update();
            
            switch (m_mode)
            {
                case Mode.Color:
                    m_source = m_cameraCapture.cameraTexture;
                    break;
                
                case Mode.Depth:
                    Assert.IsTrue(m_cameraCapture.description.m_copyDepth);
                    m_source = m_cameraCapture.depthTexture;
                    break;
                    
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        protected override void Present(ScriptableRenderContext? ctx = null)
        {
            if (m_presentMode == PresentMode.FrameEnd)
                base.Present(ctx);
        }

        void OnTexturesReady(ScriptableRenderContext? ctx, CameraCapture capture)
        {
            if (m_presentMode == PresentMode.CameraCapture)
                base.Present(ctx);
        }
    }
}
