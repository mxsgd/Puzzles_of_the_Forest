using UnityEngine;

/// <summary>
/// Breeze VFX: a camera-attached ParticleSystem that blows leaves across the view. The look
/// (atlas cell, tint, sway, gusts) lives in the assigned Shader Graph material (VFXSHADER);
/// this script only owns the *motion*: spawn area, wind velocity, flutter, tumble, gust pulses.
///
/// Wind direction is in SCREEN space (0 = to the right, 90 = up), so the breeze always crosses the
/// screen the same way even when the camera orbits with Q/E.
/// </summary>
[DisallowMultipleComponent]
public class WindLeavesVfx : MonoBehaviour
{
    [Header("Look")]
    [Tooltip("Assign Assets/VFX/VFXSHADER.mat. Falls back to Resources/VFXSHADER if empty.")]
    [SerializeField] private Material material;
    [Tooltip("ON: Unity's Texture Sheet Animation picks the leaf cell from the 4x4 atlas. " +
             "Turn OFF once the shader graph does the atlas cell picking itself (Stage 1).")]
    [SerializeField] private bool useTextureSheetAnimation = true;
    [SerializeField] private Vector2 sizeRange = new Vector2(0.25f, 0.5f);
    [SerializeField] private Color colorA = new Color(0.42f, 0.66f, 0.30f, 1f);
    [SerializeField] private Color colorB = new Color(0.86f, 0.58f, 0.24f, 1f);

    [Header("Wind (screen space)")]
    [SerializeField, Range(0f, 360f)] private float windAngle = 200f;
    [SerializeField, Range(0.5f, 10f)] private float windSpeed = 3f;
    [SerializeField, Range(0f, 2f)] private float flutter = 0.6f;
    [SerializeField, Range(0f, 6f)] private float tumble = 2f;

    [Header("Gusts")]
    [SerializeField, Range(0f, 30f)] private float baseRate = 4f;
    [SerializeField, Range(0f, 60f)] private float gustRate = 14f;
    [SerializeField, Range(0.02f, 0.5f)] private float gustFrequency = 0.12f;

    [Header("Placement")]
    [Tooltip("Distance in front of the camera where leaves live. Keep it smaller than the distance to the tiles.")]
    [SerializeField, Min(1f)] private float depth = 4f;
    [SerializeField, Min(0.1f)] private float depthSpread = 3f;

    private ParticleSystem _ps;
    private ParticleSystem.EmissionModule _emission;
    private Camera _cam;

    private void Awake()
    {
        _cam = Camera.main;
        if (_cam == null)
        {
            Debug.LogWarning("[WindLeavesVfx] No main camera.", this);
            return;
        }

        if (!material)
            material = Resources.Load<Material>("VFXSHADER");
        if (!material)
        {
            Debug.LogWarning("[WindLeavesVfx] No material. Assign VFXSHADER.mat in the Inspector.", this);
            return;
        }

        Build();
    }

    private void Update()
    {
        if (_ps == null) return;

        // Gusts: two slow sine waves multiplied give irregular pulses in [0,1].
        float t = Time.time * gustFrequency;
        float gust = Mathf.Clamp01(0.5f + 0.5f * Mathf.Sin(t * 6.2831f) * Mathf.Cos(t * 2.3f + 1.3f));
        gust = Mathf.SmoothStep(0f, 1f, gust);
        _emission.rateOverTime = baseRate + gustRate * gust;
    }

    private void Build()
    {
        var go = new GameObject("WindLeaves", typeof(ParticleSystem));
        go.transform.SetParent(_cam.transform, false);
        _ps = go.GetComponent<ParticleSystem>();
        _ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        // View rectangle at the middle of the leaf depth range.
        float halfH = depth * Mathf.Tan(_cam.fieldOfView * 0.5f * Mathf.Deg2Rad);
        float halfW = halfH * _cam.aspect;
        float halfDiag = Mathf.Sqrt(halfW * halfW + halfH * halfH);

        float rad = windAngle * Mathf.Deg2Rad;
        var wind = new Vector2(Mathf.Cos(rad), Mathf.Sin(rad));

        // --- Main ---
        var main = _ps.main;
        main.loop = true;
        main.playOnAwake = true;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.startSpeed = 0f;                                   // motion comes from Velocity over Lifetime
        main.startLifetime = new ParticleSystem.MinMaxCurve(
            2f * halfDiag * 1.15f / windSpeed, 2f * halfDiag * 1.5f / windSpeed);
        main.startSize = new ParticleSystem.MinMaxCurve(sizeRange.x, sizeRange.y);
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        main.startColor = new ParticleSystem.MinMaxGradient(colorA, colorB);
        main.maxParticles = 300;

        // --- Shape: a thin slab on the upwind edge, long side perpendicular to the wind ---
        var shape = _ps.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.position = new Vector3(-wind.x * halfDiag * 1.1f, -wind.y * halfDiag * 1.1f, depth);
        shape.rotation = new Vector3(0f, 0f, windAngle);
        shape.scale = new Vector3(0.5f, halfDiag * 2f, depthSpread);

        // --- Wind velocity (camera-local space so it stays screen-relative) ---
        var vel = _ps.velocityOverLifetime;
        vel.enabled = true;
        vel.space = ParticleSystemSimulationSpace.Local;
        vel.x = new ParticleSystem.MinMaxCurve(wind.x * windSpeed * 0.8f, wind.x * windSpeed * 1.2f);
        vel.y = new ParticleSystem.MinMaxCurve(wind.y * windSpeed * 0.8f, wind.y * windSpeed * 1.2f);
        vel.z = new ParticleSystem.MinMaxCurve(0f, 0f);

        // --- Flutter ---
        var noise = _ps.noise;
        noise.enabled = flutter > 0f;
        noise.strength = flutter;
        noise.frequency = 0.35f;
        noise.scrollSpeed = 0.4f;
        noise.damping = true;

        // --- Tumble ---
        var rot = _ps.rotationOverLifetime;
        rot.enabled = tumble > 0f;
        rot.z = new ParticleSystem.MinMaxCurve(-tumble, tumble);

        // --- Fade in / out ---
        var col = _ps.colorOverLifetime;
        col.enabled = true;
        var g = new Gradient();
        g.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[]
            {
                new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.12f),
                new GradientAlphaKey(1f, 0.85f), new GradientAlphaKey(0f, 1f)
            });
        col.color = g;

        // --- Interim atlas cell picking (replaced by the shader graph in Stage 1) ---
        var sheet = _ps.textureSheetAnimation;
        sheet.enabled = useTextureSheetAnimation;
        if (useTextureSheetAnimation)
        {
            sheet.mode = ParticleSystemAnimationMode.Grid;
            sheet.numTilesX = 4;
            sheet.numTilesY = 4;
            sheet.animation = ParticleSystemAnimationType.WholeSheet;
            sheet.frameOverTime = new ParticleSystem.MinMaxCurve(0f);   // frame 0 = leaf
            sheet.startFrame = new ParticleSystem.MinMaxCurve(0f);
            sheet.cycleCount = 1;
        }

        // --- Custom data: per-particle values the shader graph reads from UV1 ---
        //   UV1.x = atlas cell index (0 = leaf, 3 = blade), UV1.y = random 0..1 (sway phase)
        var custom = _ps.customData;
        custom.enabled = true;
        custom.SetMode(ParticleSystemCustomData.Custom1, ParticleSystemCustomDataMode.Vector);
        custom.SetVectorComponentCount(ParticleSystemCustomData.Custom1, 2);
        custom.SetVector(ParticleSystemCustomData.Custom1, 0, new ParticleSystem.MinMaxCurve(0f));
        custom.SetVector(ParticleSystemCustomData.Custom1, 1, new ParticleSystem.MinMaxCurve(0f, 1f));

        // --- Renderer ---
        var r = go.GetComponent<ParticleSystemRenderer>();
        r.renderMode = ParticleSystemRenderMode.Billboard;
        r.sharedMaterial = material;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        r.receiveShadows = false;
        r.SetActiveVertexStreams(new System.Collections.Generic.List<ParticleSystemVertexStream>
        {
            ParticleSystemVertexStream.Position,
            ParticleSystemVertexStream.Color,
            ParticleSystemVertexStream.UV,
            ParticleSystemVertexStream.Custom1XY
        });

        _emission = _ps.emission;
        _emission.rateOverTime = baseRate;

        // Pre-warm so the screen isn't empty for the first seconds.
        _ps.Simulate(main.startLifetime.constantMin * 0.6f, true, true);
        _ps.Play();
    }

    private void OnDestroy()
    {
        if (_ps != null)
            Destroy(_ps.gameObject);
    }
}
