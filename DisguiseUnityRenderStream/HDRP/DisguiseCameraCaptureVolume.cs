#if UNITY_STANDALONE_WIN 
#define PLUGIN_AVAILABLE
#endif

#if UNITY_PIPELINE_HDRP
using System;
using System.Threading;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

[Serializable, VolumeComponentMenu("Post-processing/Custom/DisguiseCameraCapturePostProcess")]
public sealed class DisguiseCameraCaptureAfterPostProcess : CustomPostProcessVolumeComponent, IPostProcessComponent
{
    public IntParameter width = new IntParameter(1920);
    public IntParameter height = new IntParameter(1080);
    public RenderTexture colourRenderTexture;
    public Texture2D colourTex2D;

    public bool IsActive() => m_colourMaterial != null;
    public override CustomPostProcessInjectionPoint injectionPoint => CustomPostProcessInjectionPoint.AfterPostProcess;

    public override void Setup()
    {
        CreateMaterial();
        CreateTexture();
    }

    public override void Render(CommandBuffer cmd, HDCamera camera, RTHandle source, RTHandle destination)
    {
        if (m_colourMaterial == null)
            return;
        m_colourMaterial.SetTexture("_InputTexture", source);
        cmd.Blit(source.rt, colourRenderTexture, m_colourMaterial);
        FetchTarget(colourRenderTexture, colourTex2D);

        Camera[] cams = Camera.allCameras;
        foreach(Camera cam in cams)
        {
            if (cam.name != camera.camera.name)
                continue;
            DisguiseCameraCapture dcc = cam.GetComponent<DisguiseCameraCapture>();
            if (dcc == null)
                continue;
            if (dcc.m_frameSender == null)
                continue;
            dcc.m_frameSender.SendFrame(colourTex2D);
        }

        //SaveAsRawImage(colourTex2D, "dump.raw");
    }

    public override void Cleanup()
    {
        CoreUtils.Destroy(m_colourMaterial);
    }

    private void CreateMaterial()
    {
        if (Shader.Find(kColourShaderName) != null)
            m_colourMaterial = new Material(Shader.Find(kColourShaderName));
        else
            Debug.LogError($"Unable to find Disguise RenderStream shader '{kColourShaderName}'");
    }

    private void CreateTexture()
    {
        colourRenderTexture = new RenderTexture(width.value, height.value, 24, RenderTextureFormat.ARGBFloat);
        colourTex2D = new Texture2D(width.value, height.value, TextureFormat.RGBAFloat, false);
    }

    // Debug code...
    private void FetchTarget(RenderTexture renderTexture, Texture2D texture2D)
    {
        RenderTexture activeRenderTexture = RenderTexture.active;
        RenderTexture unflipped = RenderTexture.GetTemporary(renderTexture.width, renderTexture.height, 0, renderTexture.format);
        RenderTexture.active = unflipped;
        Graphics.Blit(renderTexture, unflipped, new Vector2(1.0f, -1.0f), new Vector2(0.0f, 1.0f));
        Graphics.CopyTexture(unflipped, texture2D);
        RenderTexture.active = activeRenderTexture;
        RenderTexture.ReleaseTemporary(unflipped);
    }

    private void SaveAsRawImage(Texture2D texture2D, string filename)
    {
        byte[] imageData = texture2D.GetRawTextureData();
        var thread = new Thread(() => System.IO.File.WriteAllBytes(filename, imageData));
        thread.Start();
    }

    private Material m_colourMaterial;
    private const string kColourShaderName = "Hidden/Shader/DisguiseColourFramePostProcess";
}
#endif // UNITY_PIPELINE_HDRP
