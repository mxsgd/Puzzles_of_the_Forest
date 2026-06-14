using UnityEngine;

/// <summary>
/// Pszczoły: ruch po ósemce wokół kafelka rdzeniowego (pozycja skryptem, skrzydła z Animatora).
/// </summary>
[DisallowMultipleComponent]
public class HabitatBeeFigureEightFlight : MonoBehaviour
{
    [SerializeField] private Animator animator;
    [SerializeField] private int animatorLayer;
    [SerializeField] private string wingFlapStateName = "Fly";

    [Header("Orbita — ósemka w płaszczyźnie siatki")]
    [SerializeField, Min(0.01f)] private float orbitRadiusA = 1.52f;
    [SerializeField, Min(0.01f)] private float orbitRadiusB = 0.96f;
    [SerializeField, Min(0f)] private float flyHeight = 1.35f;
    [SerializeField, Min(0.01f)] private float orbitSpeed = 1.6f;
    [SerializeField, Min(0f)] private float verticalBobAmplitude = 0.07f;
    [SerializeField, Min(0.01f)] private float verticalBobFrequency = 2.2f;
    [SerializeField, Min(0f)] private float faceMotionSmoothing = 10f;
    [SerializeField, Range(0f, 1f)] private float tiltTowardMotion = 0.35f;

    private Vector3 _orbitCenterWorld;
    private Quaternion _planeRotation = Quaternion.identity;
    private float _phase;
    private bool _initialized;

    private void Awake()
    {
        if (!animator)
            animator = GetComponentInChildren<Animator>();
    }

    private void OnEnable()
    {
        ResolveWingStateNameIfNeeded();
        PlayWingAnimation();
    }

    private void ResolveWingStateNameIfNeeded()
    {
        if (animator == null || animator.runtimeAnimatorController == null)
            return;

        if (!string.IsNullOrEmpty(wingFlapStateName) && HasState(wingFlapStateName))
            return;

        var clips = animator.runtimeAnimatorController.animationClips;
        if (clips == null || clips.Length == 0)
            return;

        for (int i = 0; i < clips.Length; i++)
        {
            var clip = clips[i];
            if (clip == null)
                continue;

            string name = clip.name;
            if (name.IndexOf("fly", System.StringComparison.OrdinalIgnoreCase) >= 0
                || name.IndexOf("wing", System.StringComparison.OrdinalIgnoreCase) >= 0
                || name.IndexOf("idle", System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                wingFlapStateName = name;
                return;
            }
        }

        wingFlapStateName = clips[0].name;
    }

    private bool HasState(string stateName)
    {
        if (animator == null || string.IsNullOrEmpty(stateName))
            return false;

        return animator.HasState(animatorLayer, Animator.StringToHash(stateName));
    }

    public void Initialize(Vector3 orbitCenterWorld, Quaternion gridRotation)
    {
        _orbitCenterWorld = orbitCenterWorld;
        _planeRotation = gridRotation;
        _phase = Random.Range(0f, Mathf.PI * 2f);
        _initialized = true;

        transform.position = EvaluateOrbitPosition(_phase);
        ApplyFacing(EvaluateOrbitTangent(_phase), snap: true);
    }

    private void Update()
    {
        if (!_initialized)
            return;

        _phase += orbitSpeed * Time.deltaTime;
        Vector3 nextPos = EvaluateOrbitPosition(_phase);
        Vector3 tangent = EvaluateOrbitTangent(_phase);

        transform.position = nextPos;
        ApplyFacing(tangent, snap: false);
    }

    private Vector3 EvaluateOrbitPosition(float phase)
    {
        float x = orbitRadiusA * Mathf.Sin(phase);
        float z = orbitRadiusB * Mathf.Sin(phase * 2f);
        float y = flyHeight + verticalBobAmplitude * Mathf.Sin(phase * verticalBobFrequency);
        Vector3 local = new Vector3(x, y, z);
        return _orbitCenterWorld + _planeRotation * local;
    }

    private Vector3 EvaluateOrbitTangent(float phase)
    {
        float dx = orbitRadiusA * Mathf.Cos(phase);
        float dz = 2f * orbitRadiusB * Mathf.Cos(phase * 2f);
        Vector3 localTangent = new Vector3(dx, 0f, dz);
        return _planeRotation * localTangent;
    }

    private void ApplyFacing(Vector3 tangentWorld, bool snap)
    {
        Vector3 flat = tangentWorld;
        flat.y *= tiltTowardMotion;
        if (flat.sqrMagnitude < 0.0001f)
            return;

        Quaternion target = Quaternion.LookRotation(flat.normalized, Vector3.up);
        transform.rotation = snap
            ? target
            : Quaternion.Slerp(transform.rotation, target, faceMotionSmoothing * Time.deltaTime);
    }

    private void PlayWingAnimation()
    {
        if (animator == null || animator.runtimeAnimatorController == null)
            return;

        if (string.IsNullOrEmpty(wingFlapStateName))
        {
            animator.Play(0, animatorLayer, Random.value);
            return;
        }

        int hash = Animator.StringToHash(wingFlapStateName);
        if (animator.HasState(animatorLayer, hash))
            animator.Play(hash, animatorLayer, Random.value);
        else
            animator.Play(wingFlapStateName, animatorLayer, Random.value);
    }
}
