using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class BiomeDecorationTintReceiver : MonoBehaviour
{
    [SerializeField] private TilePositionTint tileTintSource;
    [SerializeField] private bool followTileTint = true;
    [SerializeField] private BiomeDecorationTintProfile tintProfile;
    [SerializeField] private string contentTag;
    [SerializeField] private TileBiome tileBiome;
    [SerializeField] private Vector3 tintSampleWorldPosition;
    [SerializeField] private bool includeChildren = true;
    [SerializeField, Range(0f, 1f)] private float tileTintStrengthMultiplier = 0.55f;

    [Header("Runtime refresh")]
    [SerializeField, Min(0.1f)] private float refreshInterval = 0.75f;

    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorId = Shader.PropertyToID("_Color");

    private readonly List<MaterialPropertyBlock> _blocks = new();
    private readonly List<Renderer> _cachedRenderers = new();
    private float _nextRefreshTime;

    public void Configure(
        BiomeDecorationTintProfile profile,
        TilePositionTint tileTint,
        string tag,
        TileBiome biome,
        Vector3 sampleWorldPos)
    {
        tintProfile = profile;
        tileTintSource = tileTint;
        contentTag = tag;
        tileBiome = biome;
        tintSampleWorldPosition = sampleWorldPos;
        CacheRenderers();
        ApplyTint();
    }

    private void OnEnable()
    {
        CacheRenderers();
        ApplyTint();
        _nextRefreshTime = Time.time + Random.Range(0f, refreshInterval);
    }

    private void Update()
    {
        if (tintProfile == null && tileTintSource == null) return;
        if (Time.time < _nextRefreshTime) return;
        _nextRefreshTime = Time.time + refreshInterval;
        ApplyTint();
    }

    [ContextMenu("Apply Tint")]
    public void ApplyTint()
    {
        if (_cachedRenderers.Count == 0) CacheRenderers();

        Color tint;
        float strength;
        bool hasTint = false;

        if (followTileTint && tileTintSource != null && tileTintSource.TryGetTintColor(out var tileTint))
        {
            tint = tileTint;
            strength = tileTintSource.TintStrength * tileTintStrengthMultiplier;
            hasTint = true;
        }
        else if (tintProfile != null && tintProfile.TryEvaluate(contentTag, tileBiome, tintSampleWorldPosition, out var profileTint, out var profileStrength))
        {
            tint = profileTint;
            strength = profileStrength;
            hasTint = true;
        }
        else
        {
            tint = Color.white;
            strength = 0f;
        }

        if (!hasTint) return;

        for (int r = 0; r < _cachedRenderers.Count; r++)
        {
            var renderer = _cachedRenderers[r];
            if (renderer == null) continue;

            var mats = renderer.sharedMaterials;
            EnsureBlockCapacity(mats.Length);
            for (int i = 0; i < mats.Length; i++)
            {
                var mat = mats[i];
                if (mat == null) continue;

                int colorProperty = ResolveColorProperty(mat);
                if (colorProperty == -1) continue;

                Color baseColor = mat.GetColor(colorProperty);
                Color targetColor = Color.Lerp(baseColor, tint, strength);
                targetColor = PreventDarkening(baseColor, targetColor);

                var block = _blocks[i];
                renderer.GetPropertyBlock(block, i);
                block.SetColor(colorProperty, targetColor);
                renderer.SetPropertyBlock(block, i);
            }
        }
    }

    private void CacheRenderers()
    {
        _cachedRenderers.Clear();
        var renderers = includeChildren
            ? GetComponentsInChildren<Renderer>(true)
            : GetComponents<Renderer>();
        for (int i = 0; i < renderers.Length; i++)
            if (renderers[i] != null) _cachedRenderers.Add(renderers[i]);
    }

    private static int ResolveColorProperty(Material mat)
    {
        if (mat.HasProperty(BaseColorId)) return BaseColorId;
        if (mat.HasProperty(ColorId)) return ColorId;
        return -1;
    }

    private void EnsureBlockCapacity(int materialCount)
    {
        while (_blocks.Count < materialCount)
            _blocks.Add(new MaterialPropertyBlock());
    }

    private static Color PreventDarkening(Color baseColor, Color targetColor)
    {
        float baseLum = Mathf.Max(0.0001f, baseColor.r * 0.2126f + baseColor.g * 0.7152f + baseColor.b * 0.0722f);
        float targetLum = Mathf.Max(0.0001f, targetColor.r * 0.2126f + targetColor.g * 0.7152f + targetColor.b * 0.0722f);
        if (targetLum >= baseLum)
            return targetColor;

        float boost = baseLum / targetLum;
        targetColor.r = Mathf.Clamp01(targetColor.r * boost);
        targetColor.g = Mathf.Clamp01(targetColor.g * boost);
        targetColor.b = Mathf.Clamp01(targetColor.b * boost);
        return targetColor;
    }
}
