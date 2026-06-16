using System;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

/// <summary>
/// Rebuilds animal AnimatorControllers: Idle (bind pose), Headshake, LookingAround.
/// Transitions are script-driven (HabitatAnimalIdleAnimator / HabitatBeeFigureEightFlight).
/// </summary>
public static class AnimalAnimatorControllerBuilder
{
    private const string IdleClipPath = "Assets/Animations/AnimalBindPoseIdle.anim";

    [MenuItem("Idle Forest/Animations/Rebuild Animal Controllers")]
    public static void RebuildAll()
    {
        var idleClip = EnsureBindPoseIdleClip();

        RebuildGroundAnimal("Assets/prefabs/Bear.controller", "Assets/Models/BearLowPoly.fbx", idleClip);
        RebuildGroundAnimal("Assets/prefabs/Dear.controller", "Assets/Models/DearLowPoly.fbx", idleClip);
        RebuildGroundAnimal("Assets/prefabs/Beaver.controller", "Assets/Models/BoberLowPoly.fbx", idleClip);
        RebuildBee("Assets/prefabs/Bee.controller", "Assets/Models/BeeLowPoly.fbx");

        AssetDatabase.SaveAssets();
        Debug.Log("[AnimalAnimators] Rebuilt animal animator controllers.");
    }

    private static AnimationClip EnsureBindPoseIdleClip()
    {
        EnsureAnimationsFolder();

        var existing = AssetDatabase.LoadAssetAtPath<AnimationClip>(IdleClipPath);
        if (existing != null)
            return existing;

        var clip = new AnimationClip { name = "AnimalBindPoseIdle" };
        var settings = AnimationUtility.GetAnimationClipSettings(clip);
        settings.loopTime = true;
        AnimationUtility.SetAnimationClipSettings(clip, settings);

        AssetDatabase.CreateAsset(clip, IdleClipPath);
        AssetDatabase.SaveAssets();
        return clip;
    }

    private static void EnsureAnimationsFolder()
    {
        if (!AssetDatabase.IsValidFolder("Assets/Animations"))
            AssetDatabase.CreateFolder("Assets", "Animations");
    }

    private static void RebuildGroundAnimal(string controllerPath, string modelPath, AnimationClip idleClip)
    {
        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
        if (controller == null)
        {
            Debug.LogError($"[AnimalAnimators] Missing controller: {controllerPath}");
            return;
        }

        var headshake = FindClip(modelPath, "headshake", "head");
        var looking = FindClip(modelPath, "looking", "look", "around");

        var sm = controller.layers[0].stateMachine;
        ClearStateMachine(sm);

        var idleState = sm.AddState("Idle", new Vector3(300f, 0f, 0f));
        idleState.motion = idleClip;

        if (headshake != null)
        {
            var headshakeState = sm.AddState("Headshake", new Vector3(300f, 80f, 0f));
            headshakeState.motion = headshake;
        }

        if (looking != null)
        {
            var lookingState = sm.AddState("LookingAround", new Vector3(300f, 160f, 0f));
            lookingState.motion = looking;
        }

        sm.defaultState = idleState;
        EditorUtility.SetDirty(controller);

        Debug.Log(
            $"[AnimalAnimators] {controllerPath}: Idle + " +
            $"{(headshake != null ? "Headshake" : "—")} + " +
            $"{(looking != null ? "LookingAround" : "—")}");
    }

    private static void RebuildBee(string controllerPath, string modelPath)
    {
        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
        if (controller == null)
        {
            Debug.LogError($"[AnimalAnimators] Missing controller: {controllerPath}");
            return;
        }

        var flyClip = FindClip(modelPath, "fly", "flight", "wing");
        if (flyClip == null)
        {
            Debug.LogWarning($"[AnimalAnimators] No wing clip in {modelPath}");
            return;
        }

        var sm = controller.layers[0].stateMachine;
        ClearStateMachine(sm);

        var flyState = sm.AddState("Fly", new Vector3(300f, 0f, 0f));
        flyState.motion = flyClip;
        sm.defaultState = flyState;

        EditorUtility.SetDirty(controller);
        Debug.Log($"[AnimalAnimators] {controllerPath}: Fly ({flyClip.name})");
    }

    private static void ClearStateMachine(AnimatorStateMachine sm)
    {
        var childStates = sm.states;
        for (int i = childStates.Length - 1; i >= 0; i--)
            sm.RemoveState(childStates[i].state);

        var childMachines = sm.stateMachines;
        for (int i = childMachines.Length - 1; i >= 0; i--)
            sm.RemoveStateMachine(childMachines[i].stateMachine);
    }

    private static AnimationClip FindClip(string assetPath, params string[] keywords)
    {
        foreach (var obj in AssetDatabase.LoadAllAssetsAtPath(assetPath))
        {
            if (obj is not AnimationClip clip || IsPreviewClip(clip))
                continue;

            for (int i = 0; i < keywords.Length; i++)
            {
                if (clip.name.IndexOf(keywords[i], StringComparison.OrdinalIgnoreCase) >= 0)
                    return clip;
            }
        }

        foreach (var obj in AssetDatabase.LoadAllAssetsAtPath(assetPath))
        {
            if (obj is AnimationClip clip && !IsPreviewClip(clip))
                return clip;
        }

        return null;
    }

    private static bool IsPreviewClip(AnimationClip clip)
        => clip.name.StartsWith("__preview__", StringComparison.Ordinal);
}
