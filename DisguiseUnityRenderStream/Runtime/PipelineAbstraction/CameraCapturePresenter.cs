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

        [SerializeField]
        Mode m_mode;
        
        CameraCapture m_cameraCapture;
        RenderTexture m_ColorTexture;
        RenderTexture m_DepthTexture;
        
        protected override void OnEnable()
        {
            base.OnEnable();

            m_cameraCapture = GetComponent<CameraCapture>();
            m_cameraCapture.onCapture += OnCapture;
        }
        
        protected override void OnDisable()
        {
            base.OnEnable();

            if (m_cameraCapture != null)
                m_cameraCapture.onCapture -= OnCapture;
        }

        void OnCapture(ScriptableRenderContext context, CameraCapture.Capture capture)
        {
            m_ColorTexture = capture.cameraTexture;
            m_DepthTexture = capture.depthTexture;
        }

        protected override void Update()
        {
            base.Update();
            
            switch (m_mode)
            {
                case Mode.Color:
                    m_source = m_ColorTexture;
                    break;
                
                case Mode.Depth:
                    Assert.IsTrue(m_cameraCapture.description.m_copyDepth);
                    m_source = m_DepthTexture;
                    break;
                    
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }
    }
}
