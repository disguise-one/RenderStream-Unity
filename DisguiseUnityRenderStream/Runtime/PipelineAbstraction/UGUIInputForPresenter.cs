using UnityEngine;
using UnityEngine.EventSystems;

namespace Disguise.RenderStream
{
    /// <summary>
    /// Attaches to this <see cref="GameObject"/>'s <see cref="UnityEngine.EventSystems.BaseInputModule"/>
    /// to convert the mouse coordinates to the specified <see cref="Presenter"/>.
    /// </summary>
    class UGUIInputForPresenter : BaseInput
    {
        BaseInputModule m_InputModule;

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
            m_InputModule = GetComponent<BaseInputModule>();
            m_InputModule.inputOverride = this;
        }

        protected override void OnDisable()
        {
            if (m_InputModule != null && m_InputModule.inputOverride == this)
                m_InputModule.inputOverride = null;
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

        public override Vector2 mousePosition
        {
            get
            {
                var truePosition = Input.mousePosition;
                var presenterPosition = truePosition;
                
                presenterPosition.x -= Offset.x;
                presenterPosition.y -= Offset.y;
                presenterPosition.x *= Scale.x;
                presenterPosition.y *= Scale.y;
                
                return presenterPosition;
            }
        }
    }
}
