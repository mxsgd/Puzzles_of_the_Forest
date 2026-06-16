using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// Steruje cyklem idle → animacja → idle na Animatorze (CrossFade ze skryptu).
/// Kontroler nie powinien mieć automatycznych transitionów — tylko stany z clipami.
/// </summary>
[DisallowMultipleComponent]
public class HabitatAnimalIdleAnimator : MonoBehaviour
{
    [SerializeField] private Animator animator;
    [SerializeField] private int animatorLayer;

    [Header("Stany")]
    [SerializeField] private string idleStateName = "Idle";
    [SerializeField] private string[] lookAroundStateNames = Array.Empty<string>();

    [Header("Timing")]
    [SerializeField] private Vector2 idleWaitSeconds = new(4f, 4f);
    [SerializeField, Range(0f, 1f)] private float lookAroundChance = 1f;
    [SerializeField, Min(0f)] private float crossFadeSeconds = 0.25f;
    [SerializeField, Min(0f)] private float lookClipLengthPadding = 0.05f;

    private Coroutine _loop;
    private bool _clipNamesResolved;
    private int _nextLookStateIndex;

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

        if (!EnsureAnimatorReady())
            yield break;

        ResolveClipNamesIfNeeded();

        if (lookAroundStateNames == null || lookAroundStateNames.Length == 0)
            Debug.LogWarning($"[HabitatAnimalIdleAnimator] Brak stanów animacji na {name}.", this);

        while (enabled)
        {
            PlayState(idleStateName);
            yield return new WaitForSeconds(UnityEngine.Random.Range(idleWaitSeconds.x, idleWaitSeconds.y));

            if (lookAroundStateNames == null || lookAroundStateNames.Length == 0)
                continue;

            if (UnityEngine.Random.value > lookAroundChance)
                continue;

            string lookState = PickNextLookState();
            if (string.IsNullOrEmpty(lookState))
                continue;

            PlayState(lookState);
            yield return WaitForCurrentStateClip(lookState);
        }
    }

    private bool EnsureAnimatorReady()
    {
        if (!animator)
            animator = GetComponentInChildren<Animator>();

        if (animator == null)
        {
            Debug.LogWarning($"[HabitatAnimalIdleAnimator] Brak Animator na {name}.", this);
            return false;
        }

        animator.enabled = true;
        animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

        if (animator.avatar == null)
            Debug.LogWarning($"[HabitatAnimalIdleAnimator] Animator bez Avatar na {name} — animacje mogą nie działać.", this);

        if (animator.runtimeAnimatorController == null)
            Debug.LogWarning($"[HabitatAnimalIdleAnimator] Animator bez kontrolera na {name}.", this);

        animator.Rebind();
        animator.Update(0f);
        return animator.runtimeAnimatorController != null;
    }

    private string PickNextLookState()
    {
        if (lookAroundStateNames.Length == 1)
            return lookAroundStateNames[0];

        string state = lookAroundStateNames[_nextLookStateIndex];
        _nextLookStateIndex = (_nextLookStateIndex + 1) % lookAroundStateNames.Length;
        return state;
    }

    private IEnumerator WaitForCurrentStateClip(string stateName)
    {
        if (animator == null)
            yield break;

        int hash = Animator.StringToHash(stateName);
        float clipLength = GetClipLengthForState(stateName);
        float wait = Mathf.Max(clipLength, 0.1f) + lookClipLengthPadding;

        float elapsed = 0f;
        while (elapsed < crossFadeSeconds + 0.15f)
        {
            elapsed += Time.deltaTime;
            yield return null;
        }

        elapsed = 0f;
        while (elapsed < wait)
        {
            var info = animator.GetCurrentAnimatorStateInfo(animatorLayer);
            if (info.shortNameHash == hash && info.normalizedTime >= 0.98f)
                yield break;

            elapsed += Time.deltaTime;
            yield return null;
        }
    }

    private float GetClipLengthForState(string stateName)
    {
        if (animator?.runtimeAnimatorController == null)
            return 1f;

        var clips = animator.runtimeAnimatorController.animationClips;
        for (int i = 0; i < clips.Length; i++)
        {
            var clip = clips[i];
            if (clip == null)
                continue;

            if (string.Equals(clip.name, stateName, StringComparison.OrdinalIgnoreCase))
                return clip.length;
        }

        for (int i = 0; i < clips.Length; i++)
        {
            var clip = clips[i];
            if (clip == null || string.Equals(clip.name, "AnimalBindPoseIdle", StringComparison.OrdinalIgnoreCase))
                continue;

            return clip.length;
        }

        return 1f;
    }

    private void ResolveClipNamesIfNeeded()
    {
        if (_clipNamesResolved || animator == null || animator.runtimeAnimatorController == null)
            return;

        _clipNamesResolved = true;

        if (string.IsNullOrEmpty(idleStateName) || !HasState(idleStateName))
        {
            if (HasState("Idle"))
                idleStateName = "Idle";
        }

        if (lookAroundStateNames == null || lookAroundStateNames.Length == 0)
            lookAroundStateNames = CollectLookAroundStateNames();
    }

    private string[] CollectLookAroundStateNames()
    {
        if (HasState("Headshake") && HasState("LookingAround"))
            return new[] { "Headshake", "LookingAround" };

        if (HasState("Headshake"))
            return new[] { "Headshake" };

        if (HasState("LookingAround"))
            return new[] { "LookingAround" };

        return Array.Empty<string>();
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

        if (crossFadeSeconds > 0f)
            animator.CrossFade(hash, crossFadeSeconds, animatorLayer, 0f);
        else
            animator.Play(hash, animatorLayer, 0f);

        animator.Update(0f);
    }
}
