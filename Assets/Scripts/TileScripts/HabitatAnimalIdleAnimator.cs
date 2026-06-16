using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// Losowo odtwarza idle i krótkie animacje typu „rozglądanie się” na Animatorze.
/// Nazwy stanów można wpisać w Inspectorze albo zostawić puste — skrypt spróbuje je znaleźć po nazwie klipu.
/// </summary>
[DisallowMultipleComponent]
public class HabitatAnimalIdleAnimator : MonoBehaviour
{
    [SerializeField] private Animator animator;
    [SerializeField] private int animatorLayer;

    [Header("Stany / klipy")]
    [SerializeField] private string idleStateName = "Idle";
    [SerializeField] private string[] lookAroundStateNames = Array.Empty<string>();

    [Header("Timing")]
    [SerializeField] private Vector2 idleWaitSeconds = new(3f, 7f);
    [SerializeField] private Vector2 lookWaitSeconds = new(1.2f, 2.5f);
    [SerializeField, Range(0f, 1f)] private float lookAroundChance = 0.45f;
    [SerializeField, Min(0f)] private float crossFadeSeconds = 0.25f;

    private Coroutine _loop;
    private bool _clipNamesResolved;

    private void Awake()
    {
        if (!animator)
            animator = GetComponentInChildren<Animator>();
    }

    private void OnEnable()
    {
        if (_loop != null)
            StopCoroutine(_loop);
        _loop = StartCoroutine(BehaviorLoop());
    }

    private void OnDisable()
    {
        if (_loop != null)
        {
            StopCoroutine(_loop);
            _loop = null;
        }
    }

    private IEnumerator BehaviorLoop()
    {
        yield return null;

        ResolveClipNamesIfNeeded();

        while (enabled)
        {
            PlayState(idleStateName);
            yield return new WaitForSeconds(UnityEngine.Random.Range(idleWaitSeconds.x, idleWaitSeconds.y));

            if (lookAroundStateNames == null || lookAroundStateNames.Length == 0)
                continue;

            if (UnityEngine.Random.value > lookAroundChance)
                continue;

            string lookState = lookAroundStateNames[UnityEngine.Random.Range(0, lookAroundStateNames.Length)];
            if (string.IsNullOrEmpty(lookState))
                continue;

            PlayState(lookState);
            yield return new WaitForSeconds(UnityEngine.Random.Range(lookWaitSeconds.x, lookWaitSeconds.y));
        }
    }

    private void ResolveClipNamesIfNeeded()
    {
        if (_clipNamesResolved || animator == null || animator.runtimeAnimatorController == null)
            return;

        _clipNamesResolved = true;
        var clips = animator.runtimeAnimatorController.animationClips;
        if (clips == null || clips.Length == 0)
            return;

        if (string.IsNullOrEmpty(idleStateName) || !HasState(idleStateName))
            idleStateName = FindClipName(clips, "idle") ?? clips[0].name;

        if (lookAroundStateNames == null || lookAroundStateNames.Length == 0)
            lookAroundStateNames = CollectClipNames(clips, "look", "glance", "scan", "watch", "head");
    }

    private static string FindClipName(AnimationClip[] clips, string keyword)
    {
        for (int i = 0; i < clips.Length; i++)
        {
            var clip = clips[i];
            if (clip == null)
                continue;
            if (clip.name.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0)
                return clip.name;
        }

        return null;
    }

    private static string[] CollectClipNames(AnimationClip[] clips, params string[] keywords)
    {
        int count = 0;
        for (int i = 0; i < clips.Length; i++)
        {
            var clip = clips[i];
            if (clip == null || string.Equals(clip.name, "Idle", StringComparison.OrdinalIgnoreCase))
                continue;

            for (int k = 0; k < keywords.Length; k++)
            {
                if (clip.name.IndexOf(keywords[k], StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    count++;
                    break;
                }
            }
        }

        if (count == 0)
            return Array.Empty<string>();

        var names = new string[count];
        int idx = 0;
        for (int i = 0; i < clips.Length; i++)
        {
            var clip = clips[i];
            if (clip == null)
                continue;

            for (int k = 0; k < keywords.Length; k++)
            {
                if (clip.name.IndexOf(keywords[k], StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    names[idx++] = clip.name;
                    break;
                }
            }
        }

        return names;
    }

    private bool HasState(string stateName)
    {
        if (animator == null || string.IsNullOrEmpty(stateName))
            return false;

        int hash = Animator.StringToHash(stateName);
        return animator.HasState(animatorLayer, hash);
    }

    private void PlayState(string stateName)
    {
        if (animator == null || string.IsNullOrEmpty(stateName))
            return;

        if (animator.runtimeAnimatorController == null)
            return;

        int hash = Animator.StringToHash(stateName);
        if (!animator.HasState(animatorLayer, hash))
            return;

        animator.CrossFade(hash, crossFadeSeconds, animatorLayer, 0f);
    }
}
