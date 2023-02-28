using System;
using UnityEngine;
using UnityEngine.Assertions;

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
    [ExecuteAlways]
    public class CameraCapturePresenter : Presenter
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
        
        protected override void OnEnable()
        {
            base.OnEnable();

            m_cameraCapture = GetComponent<CameraCapture>();
        }

        protected override void Update()
        {
            base.Update();
            
            switch (m_mode)
            {
                case Mode.Color:
                    source = m_cameraCapture.cameraTexture;
                    break;
                
                case Mode.Depth:
                    Assert.IsTrue(m_cameraCapture.description.m_copyDepth);
                    source = m_cameraCapture.depthTexture;
                    break;
                    
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }
    }
}
