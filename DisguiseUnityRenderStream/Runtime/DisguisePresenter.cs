using System;
using System.Collections.Generic;
using System.Linq;
using Disguise.RenderStream.Utils;
using UnityEngine;
using UnityEngine.Assertions;

namespace Disguise.RenderStream
{
    class DisguisePresenter : MonoBehaviour
    {
        public enum PresenterResizeStrategies
        {
            ActualSize,
            Stretch,
            Fill,
            Fit,
            Clamp
        }

        public int Selected
        {
            get => m_Selected;
            set => m_Selected = value;
        }
        
        public PresenterResizeStrategies ResizeStrategy
        {
            get => m_ResizeStrategy;
            set => m_ResizeStrategy = value;
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
        int m_Selected;

        [SerializeField]
        PresenterResizeStrategies m_ResizeStrategy = PresenterResizeStrategies.Fit;

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
        public static List<ManagedRemoteParameter> GetManagedRemoteParameters(ManagedSchema schema, ManagedRemoteParameters sceneSchema)
        {
            var prefab = LoadPrefab();
            var parameters = prefab.GetComponent<DisguiseRemoteParameters>();
            var managedParameters = parameters.exposedParameters();

            foreach (var parameter in managedParameters)
            {
                // Discard the name of the GameObject, keep only the field ex:
                // "DisguisePresenter Mode" => "Mode"
                parameter.displayName = parameter.displayName.Substring(parameter.displayName.IndexOf(" ") + 1);

                // Generate dropdown choices corresponding to None + Channels + Live textures
                if (parameter.key == "unity-screen-presenter m_Selected")
                {
                    List<string> options = new List<string>();
                    options.Add("None");

                    foreach (var channel in schema.channels)
                    {
                        options.Add(channel);
                    }

                    var remoteParameters = FindObjectsByType<DisguiseRemoteParameters>(FindObjectsSortMode.None);
                    foreach (var sceneParameter in sceneSchema.parameters)
                    {
                        var remoteParams = Array.Find(remoteParameters, rp => sceneParameter.key.StartsWith(rp.prefix));
                        var field = new ObjectField();
                        field.info = remoteParams.GetMemberInfoFromPropertyPath(sceneParameter.key.Substring(remoteParams.prefix.Length + 1));

                        if (field.FieldType == typeof(Texture))
                        {
                            options.Add(sceneParameter.displayName);
                        }
                    }

                    parameter.options = options.ToArray();
                }
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

            if (Selected == 0)
            {
                m_OutputPresenter.enabled = m_InputPresenter.enabled = false;
                return;
            }

            var outputIdx = Selected - 1;
            var inputIdx = Selected - 1 - m_Outputs.Length;

            var outputIsActive = outputIdx >= 0 && outputIdx < m_Outputs.Length;

            m_OutputPresenter.enabled = outputIsActive;
            m_InputPresenter.enabled = !outputIsActive;
            
            m_OutputPresenter.strategy = m_InputPresenter.strategy = PresenterStrategyToBlitStrategy(m_ResizeStrategy);

            if (outputIsActive)
                m_OutputPresenter.cameraCapture = m_Outputs[outputIdx];
            else
                m_InputPresenter.source = m_Inputs[inputIdx];
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
    }
}
