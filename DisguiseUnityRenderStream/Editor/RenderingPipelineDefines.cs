﻿using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine.Rendering;

[InitializeOnLoad]
public class RenderingPipelineDefines
{
    enum PipelineType
    {
        Unsupported,
        BuiltInPipeline,
        UniversalPipeline,
        HDRPipeline
    }

    static RenderingPipelineDefines()
    {
        UpdateDefines();
    }

    static void UpdateDefines()
    {
        var pipeline = GetPipeline();

        if (pipeline == PipelineType.UniversalPipeline)
            AddDefine("UNITY_PIPELINE_URP");
        else
            RemoveDefine("UNITY_PIPELINE_URP");

        if (pipeline == PipelineType.HDRPipeline)
            AddDefine("UNITY_PIPELINE_HDRP");
        else
            RemoveDefine("UNITY_PIPELINE_HDRP");
        
        if (PlayerSettings.useHDRDisplay)
            AddDefine("DISGUISE_UNITY_USE_HDR_DISPLAY");
    }

    static PipelineType GetPipeline()
    {
#if UNITY_2019_1_OR_NEWER
        if (GraphicsSettings.renderPipelineAsset != null)
        {
            var srpType = GraphicsSettings.renderPipelineAsset.GetType().ToString();

            if (srpType.Contains("HDRenderPipelineAsset"))
                return PipelineType.HDRPipeline;
            
            if (srpType.Contains("UniversalRenderPipelineAsset") || srpType.Contains("LightweightRenderPipelineAsset"))
                return PipelineType.UniversalPipeline;
            
            return PipelineType.Unsupported;
        }

#elif UNITY_2017_1_OR_NEWER
        if (GraphicsSettings.renderPipelineAsset != null) 
            return PipelineType.Unsupported; // SRP not supported before 2019
#endif

        return PipelineType.BuiltInPipeline;
    }

    static void AddDefine(string define)
    {
        var definesList = GetDefines();
        if (definesList.Contains(define)) 
            return;
        definesList.Add(define);
        SetDefines(definesList);
    }

    public static void RemoveDefine(string define)
    {
        var definesList = GetDefines();
        if (!definesList.Contains(define)) 
            return;
        definesList.Remove(define);
        SetDefines(definesList);
    }

    public static List<string> GetDefines()
    {
        var target = EditorUserBuildSettings.activeBuildTarget;
        var buildTargetGroup = BuildPipeline.GetBuildTargetGroup(target);
        
#if UNITY_2023_1_OR_NEWER
        var namedBuildTarget = UnityEditor.Build.NamedBuildTarget.FromBuildTargetGroup(buildTargetGroup);
        PlayerSettings.GetScriptingDefineSymbols(namedBuildTarget, out var defines);
        return defines.ToList();
#else
        var defines = PlayerSettings.GetScriptingDefineSymbolsForGroup(buildTargetGroup);
        return defines.Split(';').ToList();
#endif
    }

    public static void SetDefines(List<string> definesList)
    {
        var target = EditorUserBuildSettings.activeBuildTarget;
        var buildTargetGroup = BuildPipeline.GetBuildTargetGroup(target);
        var defines = string.Join(";", definesList.ToArray());

#if UNITY_2023_1_OR_NEWER
        var namedBuildTarget = UnityEditor.Build.NamedBuildTarget.FromBuildTargetGroup(buildTargetGroup);
        PlayerSettings.SetScriptingDefineSymbols(namedBuildTarget, defines);
#else
        PlayerSettings.SetScriptingDefineSymbolsForGroup(buildTargetGroup, defines);
#endif
    }
}
