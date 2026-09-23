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

    [Header("Obrót (Q / E) wokół punktu, w który celuje środek kamery")]
    [SerializeField] private float rotateSpeed = 90f;
    [SerializeField, Tooltip("Wysokość płaszczyzny ziemi (Y), na którą rzutowany jest środek ekranu.")]
    private float groundHeight = 0f;

    [Header("Zoom (kółko myszy)")]
    [SerializeField, Range(0.3f, 1f), Tooltip("Najbliższy zoom jako ułamek startowego dystansu do ziemi.")]
    private float minZoomFactor = 0.6f;
    [SerializeField, Range(1f, 2f), Tooltip("Najdalszy zoom jako ułamek startowego dystansu do ziemi.")]
    private float maxZoomFactor = 1.4f;
    [SerializeField, Range(0.02f, 0.3f), Tooltip("Zmiana dystansu na jedno kliknięcie kółka (ułamek startowego dystansu).")]
    private float zoomStepFraction = 0.08f;
    [SerializeField, Min(0.1f)] private float zoomSmoothing = 12f;

    private Vector3 _sessionStartPosition;
    private Quaternion _sessionStartRotation;
    private bool _sessionStartPoseCaptured;
    private Camera _camera;
    private float _startDistance;
    private float _targetDistance;

    private void Awake()
    {
        _camera = GetComponent<Camera>();
        if (!_camera) _camera = GetComponentInChildren<Camera>();
        CaptureSessionStartPose();
    }

    /// <summary>Przywraca pozycję i obrót z momentu wejścia w Play Mode (pierwszy start sesji).</summary>
    public void ResetToSessionStart()
    {
        if (!_sessionStartPoseCaptured)
            CaptureSessionStartPose();

        transform.SetPositionAndRotation(_sessionStartPosition, _sessionStartRotation);
        _targetDistance = _startDistance;
        if (_camera != null && _camera.orthographic)
            _camera.orthographicSize = _startDistance;
    }

    public void CaptureSessionStartPose()
    {
        _sessionStartPosition = transform.position;
        _sessionStartRotation = transform.rotation;
        _sessionStartPoseCaptured = true;

        if (_camera != null && _camera.orthographic)
            _startDistance = _camera.orthographicSize;
        else if (TryGetGroundPivot(out var pivot))
            _startDistance = Vector3.Distance(transform.position, pivot);
        else
            _startDistance = 0f;

        _targetDistance = _startDistance;
    }

    private bool TryGetGroundPivot(out Vector3 pivot)
    {
        var ground = new Plane(Vector3.up, new Vector3(0f, groundHeight, 0f));
        var ray = new Ray(transform.position, transform.forward);
        if (ground.Raycast(ray, out float enter) && enter > 0f)
        {
            pivot = ray.GetPoint(enter);
            return true;
        }

        pivot = default;
        return false;
    }

    private void HandleRotate(Keyboard keyboard)
    {
        float turn = 0f;
        if (keyboard.qKey.isPressed) turn -= 1f;
        if (keyboard.eKey.isPressed) turn += 1f;
        if (turn == 0f || !TryGetGroundPivot(out var pivot)) return;

        transform.RotateAround(pivot, Vector3.up, turn * rotateSpeed * Time.deltaTime);
    }

    private void HandleZoom()
    {
        if (_startDistance <= 0f) return;

        var mouse = Mouse.current;
        float scroll = mouse != null ? mouse.scroll.ReadValue().y : 0f;
        float min = _startDistance * minZoomFactor;
        float max = _startDistance * maxZoomFactor;

        if (Mathf.Abs(scroll) > 0.01f)
        {
            // Scroll up (positive) = zoom in = smaller distance.
            _targetDistance -= Mathf.Sign(scroll) * _startDistance * zoomStepFraction;
            _targetDistance = Mathf.Clamp(_targetDistance, min, max);
        }

        float t = 1f - Mathf.Exp(-zoomSmoothing * Time.deltaTime);

        if (_camera != null && _camera.orthographic)
        {
            _camera.orthographicSize = Mathf.Lerp(_camera.orthographicSize, _targetDistance, t);
            return;
        }

        if (!TryGetGroundPivot(out var pivot)) return;

        float current = Vector3.Distance(transform.position, pivot);
        float next = Mathf.Lerp(current, _targetDistance, t);
        transform.position = pivot - transform.forward * next;
    }

    private void Update()
    {
        var keyboard = Keyboard.current;
        if (keyboard == null) return;

        HandleRotate(keyboard);
        HandleZoom();

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
