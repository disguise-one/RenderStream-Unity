using System;
using UnityEngine;
using UnityEngine.Assertions;

namespace Disguise.RenderStream
{
    /// <summary>
    /// <para>
    /// Blits this <see cref="GameObject"/>'s <see cref="CameraCapture"/> to the local screen.
    /// A number of <see cref="BlitStrategy.Strategy">strategies</see> are available to handle
    /// the size and aspect ratio differences between the two surfaces.
    /// </para>
    ///
    /// <para>
    /// <see cref="Presenter.autoFlipY"/> is disabled when <see cref="CameraCaptureDescription.m_autoFlipY"/> is enabled to avoid flipping twice.
    /// </para>
    ///
    /// <para>
    /// <see cref="Presenter.sourceColorSpace"/> is automatically set to match <see cref="CameraCaptureDescription.m_colorSpace"/>.
    /// </para>
    ///
    /// <para>
    /// <see cref="UITKInputForPresenter"/> and <see cref="UGUIInputForPresenter"/> are responsible for
    /// adjusting the <see cref="UnityEngine.EventSystems.EventSystem"/> mouse coordinates to account for the blit.
    /// </para>
    /// </summary>
    [ExecuteAlways]
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
        
        public CameraCapture cameraCapture
        {
            get => m_cameraCapture;
            set
            {
                m_cameraCapture = value;
                Assign(m_cameraCapture);
            }
        }
        
        protected override void OnEnable()
        {
            base.OnEnable();

            if (m_cameraCapture == null)
                m_cameraCapture = GetComponent<CameraCapture>();
        }
        
        protected override void Update()
        {
            Assign(m_cameraCapture);
            
            base.Update();
        }

        void Assign(CameraCapture capture)
        {
            if (capture == null)
            {
                source = null;
                return;
            }
            
            if (m_cameraCapture.description.m_autoFlipY && autoFlipY)
            {
                autoFlipY = false;
                
                Debug.LogWarning($"Disabled {nameof(CameraCapturePresenter)}.{nameof(CameraCapturePresenter.autoFlipY)}" +
                                 $"because it's already enabled in the sibling {nameof(CameraCapture)} component");
            }

            sourceColorSpace = m_cameraCapture.description.m_colorSpace switch
            {
                CameraCaptureDescription.ColorSpace.Linear => SourceColorSpace.Linear,
                CameraCaptureDescription.ColorSpace.sRGB => SourceColorSpace.sRGB,
                _ => throw new ArgumentOutOfRangeException()
            };

            switch (m_mode)
            {
                case Mode.Color:
                    source = capture.cameraTexture;
                    break;
                
                case Mode.Depth:
                    Assert.IsTrue(capture.description.m_copyDepth);
                    source = capture.depthTexture;
                    break;
                    
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }
    }
}
