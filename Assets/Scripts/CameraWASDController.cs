using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Poruszanie kamerą klawiszami WASD (lewo, prawo, przód, tył) w płaszczyźnie XZ.
/// Wymaga Input System package.
/// </summary>
public class CameraWASDController : MonoBehaviour
{
    [Header("Ruch")]
    [SerializeField] private float moveSpeed = 32f;
    [SerializeField] private bool useCameraDirection = true;

    [Header("Ograniczenie (opcjonalne)")]
    [SerializeField] private bool clampPosition = false;
    [SerializeField] private Vector3 clampMin = new Vector3(-50f, 0f, -50f);
    [SerializeField] private Vector3 clampMax = new Vector3(50f, 0f, 50f);

    private Vector3 _sessionStartPosition;
    private Quaternion _sessionStartRotation;
    private bool _sessionStartPoseCaptured;

    private void Awake()
    {
        CaptureSessionStartPose();
    }

    /// <summary>Przywraca pozycję i obrót z momentu wejścia w Play Mode (pierwszy start sesji).</summary>
    public void ResetToSessionStart()
    {
        if (!_sessionStartPoseCaptured)
            CaptureSessionStartPose();

        transform.SetPositionAndRotation(_sessionStartPosition, _sessionStartRotation);
    }

    public void CaptureSessionStartPose()
    {
        _sessionStartPosition = transform.position;
        _sessionStartRotation = transform.rotation;
        _sessionStartPoseCaptured = true;
    }

    private void Update()
    {
        var keyboard = Keyboard.current;
        if (keyboard == null) return;

        float h = 0f, v = 0f;
        if (keyboard.wKey.isPressed) v += 1f;
        if (keyboard.sKey.isPressed) v -= 1f;
        if (keyboard.aKey.isPressed) h -= 1f;
        if (keyboard.dKey.isPressed) h += 1f;

        if (h == 0f && v == 0f) return;

        Vector3 dir;
        if (useCameraDirection)
        {
            var forward = transform.forward;
            var right = transform.right;
            forward.y = 0f;
            right.y = 0f;
            forward.Normalize();
            right.Normalize();
            dir = (forward * v + right * h).normalized;
        }
        else
        {
            dir = new Vector3(h, 0f, v).normalized;
        }

        var delta = dir * (moveSpeed * Time.deltaTime);
        var pos = transform.position + delta;

        if (clampPosition)
        {
            pos.x = Mathf.Clamp(pos.x, clampMin.x, clampMax.x);
            pos.y = Mathf.Clamp(pos.y, clampMin.y, clampMax.y);
            pos.z = Mathf.Clamp(pos.z, clampMin.z, clampMax.z);
        }

        transform.position = pos;
    }
}
