using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Draws the animated background (BalatroBackground shader) as a single fullscreen triangle whose
/// vertex shader pins it to the far plane — it only shows where no scene geometry covers the
/// screen, needs no camera parenting, and doesn't touch skybox/ambient lighting settings.
/// Auto-attached by GameManager; material loaded from Resources/Background.
/// </summary>
[DisallowMultipleComponent]
public class BackgroundQuad : MonoBehaviour
{
    [SerializeField] private Material backgroundMaterial;

    private GameObject _quad;

    private void Awake()
    {
        if (!backgroundMaterial)
            backgroundMaterial = Resources.Load<Material>("Background");

        if (backgroundMaterial == null)
        {
            Debug.LogWarning("[BackgroundQuad] No background material (Resources/Background).", this);
            return;
        }

        // Anything the background doesn't cover (first frames, edge cases) should fall back to the
        // palette's dark color rather than the scene camera's blue clear color.
        var cam = Camera.main;
        if (cam != null && backgroundMaterial.HasProperty("_Color3"))
        {
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = backgroundMaterial.GetColor("_Color3");
        }

        var mesh = new Mesh { name = "FullscreenTriangle" };
        mesh.vertices = new[]
        {
            new Vector3(-1f, -1f, 0f),
            new Vector3(-1f,  3f, 0f),
            new Vector3( 3f, -1f, 0f)
        };
        mesh.triangles = new[] { 0, 1, 2 };
        mesh.bounds = new Bounds(Vector3.zero, Vector3.one * 100000f);

        _quad = new GameObject("BackgroundQuad", typeof(MeshFilter), typeof(MeshRenderer));
        _quad.transform.SetParent(transform, false);
        _quad.GetComponent<MeshFilter>().sharedMesh = mesh;

        var renderer = _quad.GetComponent<MeshRenderer>();
        renderer.sharedMaterial = backgroundMaterial;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        renderer.lightProbeUsage = LightProbeUsage.Off;
        renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
    }

    private void OnDestroy()
    {
        if (_quad != null)
            Destroy(_quad);
    }
}
