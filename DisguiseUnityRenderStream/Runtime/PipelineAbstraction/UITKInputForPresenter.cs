using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UIElements;

namespace Disguise.RenderStream
{
    /// <summary>
    /// Attaches to this <see cref="GameObject"/>'s <see cref="UnityEngine.UIElements.UIDocument"/>
    /// to convert the input coordinates to the specified <see cref="Presenter"/>.
    /// </summary>
    class UITKInputForPresenter : BaseInput
    {
        UIDocument m_Document;

        [SerializeField]
        Vector2 m_Scale = Vector2.one;
        
        [SerializeField]
        Vector2 m_Offset = Vector2.zero;
        
        [SerializeField]
        Presenter m_Presenter;

        public Vector2 Scale
        {
            get => m_Scale;
            set => m_Scale = value;
        }
        
        public Vector2 Offset
        {
            get => m_Offset;
            set => m_Offset = value;
        }
        
        public Presenter Presenter
        {
            get => m_Presenter;
            set => m_Presenter = value;
        }
        
        protected override void OnEnable()
        {
            m_Document = GetComponent<UIDocument>();
            m_Document.panelSettings.SetScreenToPanelSpaceFunction(ScreenToPanelSpaceFunction);
        }

        protected override void OnDisable()
        {
            if (m_Document != null)
                m_Document.panelSettings.SetScreenToPanelSpaceFunction(null);
        }

        void LateUpdate()
        {
            if (m_Presenter != null && m_Presenter.IsValid)
            {
                var scaleBias = m_Presenter.GetScaleBias(true);
                var uiPosOnScreen = new Vector2(scaleBias.z, scaleBias.w) * m_Presenter.targetSize;
                var uiSizeOnScreen = new Vector2(scaleBias.x, scaleBias.y) * m_Presenter.targetSize;
                
                Offset = new Vector2(uiPosOnScreen.x, uiPosOnScreen.y);
                Scale = m_Presenter.sourceSize / uiSizeOnScreen;
            }
        }

        Vector2 ScreenToPanelSpaceFunction(Vector2 truePosition)
        {
            var presenterPosition = truePosition;
                
            presenterPosition.x -= Offset.x;
            presenterPosition.y -= Offset.y;
            presenterPosition.x *= Scale.x;
            presenterPosition.y *= Scale.y;
                
            return presenterPosition;
        }
    }
}
