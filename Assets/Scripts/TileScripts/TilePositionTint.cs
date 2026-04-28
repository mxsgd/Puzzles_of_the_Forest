using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Delikatnie tintuje kafel na spektrum zielony ↔ bezowy na podstawie pozycji.
/// Najlepiej podpiac pod bazowy prefab kafla (nie pod prefab wody).
/// </summary>
[DisallowMultipleComponent]
public class TilePositionTint : MonoBehaviour
{
    [Header("Renderery (opcjonalnie)")]
    [SerializeField] private Renderer[] targetRenderers;
    [SerializeField] private bool includeChildren = true;

    [Header("Kolory spektrum")]
    [SerializeField] private Color beigeColor = new Color(0.86f, 0.81f, 0.66f, 1f);
    [SerializeField] private Color greenColor = new Color(0.40f, 0.62f, 0.36f, 1f);
    [SerializeField, Range(0f, 1f)] private float tintStrength = 0.20f;

    [Header("Mapowanie pozycji")]
    [SerializeField, Min(0.001f)] private float noiseScale = 0.065f;
    [SerializeField] private Vector2 noiseOffset = new Vector2(100f, 200f);
    [SerializeField] private bool addWave = true;
    [SerializeField, Min(0.001f)] private float waveScale = 0.12f;
    [SerializeField, Range(0f, 1f)] private float waveInfluence = 0.30f;

    [Header("Filtry")]
    [SerializeField] private bool skipWaterBiome = true;

    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorId = Shader.PropertyToID("_Color");

    private readonly List<MaterialPropertyBlock> _blocks = new();

    private void OnEnable()
    {
        ApplyTint();
    }

    [ContextMenu("Apply Tint")]
    public void ApplyTint()
    {
        if (skipWaterBiome && IsWaterTile())
            return;

        var renderers = ResolveRenderers();
        if (renderers.Count == 0)
            return;

        float t = EvaluatePosition01(transform.position);
        Color tint = Color.Lerp(beigeColor, greenColor, t);

        for (int r = 0; r < renderers.Count; r++)
        {
            var renderer = renderers[r];
            if (renderer == null)
                continue;

            var mats = renderer.sharedMaterials;
            EnsureBlockCapacity(mats.Length);

            for (int i = 0; i < mats.Length; i++)
            {
                var mat = mats[i];
                if (mat == null)
                    continue;

                int colorProperty = ResolveColorProperty(mat);
                if (colorProperty == -1)
                    continue;

                Color baseColor = mat.GetColor(colorProperty);
                Color targetColor = Color.Lerp(baseColor, tint, tintStrength);

                var block = _blocks[i];
                renderer.GetPropertyBlock(block, i);
                block.SetColor(colorProperty, targetColor);
                renderer.SetPropertyBlock(block, i);
            }
        }
    }

    private bool IsWaterTile()
    {
        var biomeRuntime = GetComponent<TileBiomeRuntime>();
        return biomeRuntime != null && biomeRuntime.Biome == TileBiome.Water;
    }

    private List<Renderer> ResolveRenderers()
    {
        var result = new List<Renderer>();

        if (targetRenderers != null)
        {
            for (int i = 0; i < targetRenderers.Length; i++)
            {
                var r = targetRenderers[i];
                if (r != null)
                    result.Add(r);
            }
        }

        if (result.Count == 0)
        {
            var found = includeChildren
                ? GetComponentsInChildren<Renderer>(true)
                : GetComponents<Renderer>();
            result.AddRange(found);
        }

        return result;
    }

    private float EvaluatePosition01(Vector3 worldPos)
    {
        float n = Mathf.PerlinNoise(
            worldPos.x * noiseScale + noiseOffset.x,
            worldPos.z * noiseScale + noiseOffset.y);

        if (!addWave)
            return n;

        float wave = Mathf.Sin((worldPos.x + worldPos.z) * waveScale) * 0.5f + 0.5f;
        return Mathf.Lerp(n, wave, waveInfluence);
    }

    private static int ResolveColorProperty(Material mat)
    {
        if (mat.HasProperty(BaseColorId))
            return BaseColorId;
        if (mat.HasProperty(ColorId))
            return ColorId;
        return -1;
    }

    private void EnsureBlockCapacity(int materialCount)
    {
        while (_blocks.Count < materialCount)
            _blocks.Add(new MaterialPropertyBlock());
    }
}
