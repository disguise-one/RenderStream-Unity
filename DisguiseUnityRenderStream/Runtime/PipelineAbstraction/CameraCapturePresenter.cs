using System;
using UnityEngine;
using UnityEngine.Assertions;

namespace Disguise.RenderStream
{
    class CameraCapturePresenter : Presenter
    {
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

        void Update()
        {
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
    }
}
