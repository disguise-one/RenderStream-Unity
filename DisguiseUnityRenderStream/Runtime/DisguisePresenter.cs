using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Assertions;

namespace Disguise.RenderStream
{
    class DisguisePresenter : MonoBehaviour
    {
        public enum PresenterMode
        {
            Off,
            Output,
            Input
        }

        public enum PresenterResizeStrategies
        {
            ActualSize,
            Stretch,
            Fill,
            Fit,
            Clamp
        }

        public PresenterMode Mode
        {
            get => m_Mode;
            set => m_Mode = value;
        }
        
        public int Index
        {
            get => m_Index;
            set => m_Index = value;
        }
        
        public PresenterResizeStrategies OutputResizeStrategy
        {
            get => m_OutputResizeStrategy;
            set => m_OutputResizeStrategy = value;
        }
        
        public PresenterResizeStrategies InputResizeStrategy
        {
            get => m_InputResizeStrategy;
            set => m_InputResizeStrategy = value;
        }

        public CameraCapturePresenter OutputPresenter
        {
            get => m_OutputPresenter;
            set => m_OutputPresenter = value;
        }

        public Presenter InputPresenter
        {
            get => m_InputPresenter;
            set => m_InputPresenter = value;
        }

        [SerializeField]
        PresenterMode m_Mode = PresenterMode.Output;
        
        [SerializeField]
        int m_Index;

        [SerializeField]
        PresenterResizeStrategies m_OutputResizeStrategy = PresenterResizeStrategies.Fill;
        
        [SerializeField]
        PresenterResizeStrategies m_InputResizeStrategy = PresenterResizeStrategies.Clamp;

        [SerializeField]
        CameraCapturePresenter m_OutputPresenter;
        
        [SerializeField]
        Presenter m_InputPresenter;

        CameraCapture[] m_Outputs = {};
        RenderTexture[] m_Inputs = {};

        static BlitStrategy.Strategy PresenterStrategyToBlitStrategy(PresenterResizeStrategies strategy) => strategy switch
        {
            PresenterResizeStrategies.ActualSize => BlitStrategy.Strategy.NoResize,
            PresenterResizeStrategies.Stretch => BlitStrategy.Strategy.Stretch,
            PresenterResizeStrategies.Fill => BlitStrategy.Strategy.Fill,
            PresenterResizeStrategies.Fit => BlitStrategy.Strategy.Letterbox,
            PresenterResizeStrategies.Clamp => BlitStrategy.Strategy.Clamp,
            _ => throw new ArgumentOutOfRangeException()
        };

        public static GameObject LoadPrefab()
        {
            return Resources.Load<GameObject>("DisguisePresenter");
        }
        
#if UNITY_EDITOR
        public static List<ManagedRemoteParameter> GetManagedRemoteParameters()
        {
            var prefab = LoadPrefab();
            var parameters = prefab.GetComponent<DisguiseRemoteParameters>();
            var managedParameters = parameters.exposedParameters();

            // Discard the name of the GameObject, keep only the field ex:
            // "DisguisePresenter Mode" => "Mode"
            foreach (var parameter in managedParameters)
            {
                parameter.displayName = parameter.displayName.Substring(parameter.displayName.IndexOf(" ") + 1);
            }
            
            return managedParameters;
        }
#endif

        void OnEnable()
        {
            DisguiseRenderStream.SceneLoaded += RefreshInput;
            DisguiseRenderStream.StreamsChanged += RefreshOutput;
        }
        
        void OnDisable()
        {
            DisguiseRenderStream.SceneLoaded -= RefreshInput;
            DisguiseRenderStream.StreamsChanged -= RefreshOutput;
        }

        void Update()
        {
            Assert.IsNotNull(m_OutputPresenter);
            Assert.IsNotNull(m_InputPresenter);
            
            if (Mode == PresenterMode.Off)
            {
                m_OutputPresenter.enabled = m_InputPresenter.enabled = false;
                return;
            }

            m_OutputPresenter.enabled = Mode == PresenterMode.Output;
            m_InputPresenter.enabled = Mode == PresenterMode.Input;
            
            m_OutputPresenter.strategy = PresenterStrategyToBlitStrategy(m_OutputResizeStrategy);
            m_InputPresenter.strategy = PresenterStrategyToBlitStrategy(m_InputResizeStrategy);

            if (Mode == PresenterMode.Output)
                AssignOutput();
            else if (Mode == PresenterMode.Input)
                AssignInput();
            else
                throw new ArgumentOutOfRangeException();
        }

        void RefreshOutput()
        {
            m_Outputs = FindObjectsByType<DisguiseCameraCapture>(
                FindObjectsSortMode.InstanceID).Select(
                    x => x.GetComponent<CameraCapture>()).ToArray();
        }
        
        void RefreshInput()
        {
            if (DisguiseRenderStream.Instance is { InputTextures: { } inputTextures })
            {
                m_Inputs = inputTextures.ToArray();
            }
        }

        void AssignOutput()
        {
            m_OutputPresenter.cameraCapture = ClampIndex(Index, m_Outputs, out var clampedIndex)
                ? m_Outputs[clampedIndex]
                : null;
        }
        
        void AssignInput()
        {
            m_InputPresenter.source = ClampIndex(Index, m_Inputs, out var clampedIndex)
                ? m_Inputs[clampedIndex]
                : null;
        }

        bool ClampIndex(int index, IList list, out int clampedIndex)
        {
            if (list.Count == 0)
            {
                clampedIndex = 0;
                return false;
            }
            
            clampedIndex = Mathf.Clamp(index, 0, list.Count - 1);
            return true;
        }
    }
}
