using System;
using System.Collections.Generic;
using System.Linq;
using Disguise.RenderStream.Utils;
using UnityEngine;
using UnityEngine.Assertions;

namespace Disguise.RenderStream
{
    /// <summary>
    /// <para>
    /// This component together with the prefab of the same name offer drop-in support for presenting any Disguise-related texture to the Unity window.
    /// This class has two responsibilities:
    /// </para>
    /// <para>
    /// 1. Generating the remote parameters for each scene - including the texture selection dropdown choices specific to each scene.
    /// </para>
    /// <para>
    /// 2. Presenting a texture to the screen according to <see cref="Selected"/> and <see cref="ResizeStrategy"/>.
    /// </para>
    /// </summary>
    class DisguisePresenter : MonoBehaviour
    {
        /// <summary>
        /// This is a user-friendly subset of <see cref="BlitStrategy.Strategy"/>.
        /// </summary>
        public enum PresenterResizeStrategies
        {
            /// <see cref="BlitStrategy.Strategy.NoResize"/>
            ActualSize,
            /// <see cref="BlitStrategy.Strategy.Stretch"/>
            Stretch,
            /// <see cref="BlitStrategy.Strategy.Fill"/>
            Fill,
            /// <see cref="BlitStrategy.Strategy.Letterbox"/>
            Fit,
            /// <see cref="BlitStrategy.Strategy.Clamp"/>
            Clamp
        }

        private const string k_PrefabPath = "DisguisePresenter";
        private const string k_NoneTextureLabel = "None";

        /// <summary>
        /// The index of the selection in the texture dropdown to present to the screen.
        /// The dropdown choices are generated inside <see cref="GetManagedRemoteParameters"/>, as a concatenated list of:
        /// None + Channels (output) + Live textures (input).
        /// </summary>
        public int Selected
        {
            get => m_Selected;
            set => m_Selected = value;
        }
        
        /// <summary>
        /// The <see cref="PresenterResizeStrategies">strategy</see> for resizing the selected texture to screen.
        /// </summary>
        public PresenterResizeStrategies ResizeStrategy
        {
            get => m_ResizeStrategy;
            set => m_ResizeStrategy = value;
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

        /// <summary>
        /// Instantiates a prefab with a GameObject hierarchy configured to be dropped into a scene.
        /// It contains the <see cref="DisguisePresenter"/> and <see cref="DisguiseRemoteParameters"/> components,
        /// as well as the necessary <see cref="m_OutputPresenter"/> and <see cref="m_InputPresenter"/>.
        /// </summary>
        /// <returns></returns>
        public static GameObject LoadPrefab()
        {
            return Resources.Load<GameObject>(k_PrefabPath);
        }
        
#if UNITY_EDITOR
        /// <summary>
        /// Returns the list of remote parameters to control the presenter.
        /// The parameters are pre-configured in the prefab used by <see cref="LoadPrefab"/>.
        /// </summary>
        /// <remarks>
        /// The choices for the texture selection dropdown are scene-specific and correspond to a concatenated list of:
        /// None + Channels (output) + Live textures (input).
        /// </remarks>
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

                // Generate dropdown choices as a concatenated list of: None + Channels (output) + Live textures (input)
                if (parameter.displayName == nameof(Selected))
                {
                    List<string> options = new List<string>();
                    options.Add(k_NoneTextureLabel);

                    foreach (var channel in schema.channels)
                    {
                        options.Add(channel);
                    }

                    var remoteParameters = FindObjectsByType<DisguiseRemoteParameters>(FindObjectsSortMode.None);
                    foreach (var sceneParameter in sceneSchema.parameters)
                    {
                        var remoteParams = Array.Find(remoteParameters, rp => sceneParameter.key.StartsWith(rp.prefix));
                        var field = new ObjectField();
                        field.info = remoteParams.GetMemberInfoFromManagedParameter(sceneParameter);

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

        /// <remarks>
        /// This class should only be instantiated through <see cref="LoadPrefab"/>.
        /// </remarks>
        private DisguisePresenter()
        {
            
        }

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

            VirtualIndexToRealIndex(Selected, out var outputIdx, out var inputIdx);
            
            // No output or input selected
            if (outputIdx < 0 && inputIdx < 0)
            {
                m_OutputPresenter.enabled = m_InputPresenter.enabled = false;
                return;
            }

            m_OutputPresenter.enabled = outputIdx >= 0;
            m_InputPresenter.enabled = inputIdx >= 0;
            
            m_OutputPresenter.strategy = m_InputPresenter.strategy = PresenterStrategyToBlitStrategy(m_ResizeStrategy);

            if (m_OutputPresenter.enabled)
                m_OutputPresenter.cameraCapture = m_Outputs[outputIdx];
            
            if (m_InputPresenter.enabled)
                m_InputPresenter.source = m_Inputs[inputIdx];
        }

        /// <summary>
        /// Maps a virtual index like <see cref="Selected"/> to real indexes into <see cref="m_Outputs"/> and <see cref="m_Inputs"/>.
        /// The virtual list is described in <see cref="GetManagedRemoteParameters"/>.
        /// </summary>
        /// <remarks>
        /// <paramref name="outputIdx"/> and <paramref name="inputIdx"/> are -1 when invalid.
        /// </remarks>
        void VirtualIndexToRealIndex(int virtualIdx, out int outputIdx, out int inputIdx)
        {
            outputIdx = virtualIdx - 1;
            inputIdx = virtualIdx - 1 - m_Outputs.Length;

            if (outputIdx < 0 || outputIdx >= m_Outputs.Length)
                outputIdx = -1;
            
            if (inputIdx < 0 || inputIdx >= m_Inputs.Length)
                inputIdx = -1;
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
