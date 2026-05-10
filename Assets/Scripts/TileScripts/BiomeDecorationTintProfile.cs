using System;
using UnityEngine;

[CreateAssetMenu(
    fileName = "BiomeDecorationTintProfile",
    menuName = "Idle Forest/Biome Decoration Tint Profile")]
public class BiomeDecorationTintProfile : ScriptableObject
{
    [Serializable]
    public class TagTintRule
    {
        [Tooltip("Tag treści slotu, np. Grass / Bush.")]
        public string tag = TileContentTags.Grass;

        [Header("Kolory bazowe")]
        public Color beigeColor = new Color(0.86f, 0.81f, 0.66f, 1f);
        public Color greenColor = new Color(0.40f, 0.62f, 0.36f, 1f);

        [Header("Intensywność")]
        [Range(0f, 1f)] public float tintStrength = 0.25f;

        [Header("Mapowanie pozycji")]
        [Min(0.001f)] public float noiseScale = 0.065f;
        public Vector2 noiseOffset = new Vector2(100f, 200f);
        public bool addWave = true;
        [Min(0.001f)] public float waveScale = 0.12f;
        [Range(0f, 1f)] public float waveInfluence = 0.30f;

        [Header("Modyfikator biomu")]
        [Range(0f, 1f)] public float forestBiasToGreen = 0.25f;
        [Range(0f, 1f)] public float meadowBiasToGreen = 0.35f;
        [Range(0f, 1f)] public float mountainousBiasToGreen = 0.05f;
        [Range(0f, 1f)] public float bushyBiasToGreen = 0.20f;
    }

    [SerializeField] private TagTintRule[] rules =
    {
        new TagTintRule { tag = TileContentTags.Grass, tintStrength = 0.30f, meadowBiasToGreen = 0.45f },
        new TagTintRule { tag = TileContentTags.Bush, tintStrength = 0.22f, bushyBiasToGreen = 0.28f }
    };

    [SerializeField] private TagTintRule fallbackRule = new TagTintRule { tag = "Default", tintStrength = 0.2f };

    public bool TryEvaluate(
        string tag,
        TileBiome biome,
        Vector3 worldPos,
        out Color tintColor,
        out float tintStrength)
    {
        var rule = FindRule(tag) ?? fallbackRule;
        if (rule == null)
        {
            tintColor = Color.white;
            tintStrength = 0f;
            return false;
        }

        float t = EvaluatePosition01(rule, worldPos);
        t = ApplyBiomeBias(rule, biome, t);

        tintColor = Color.Lerp(rule.beigeColor, rule.greenColor, t);
        tintStrength = rule.tintStrength;
        return tintStrength > 0f;
    }

    private TagTintRule FindRule(string tag)
    {
        if (rules == null) return null;
        for (int i = 0; i < rules.Length; i++)
        {
            var r = rules[i];
            if (r == null || string.IsNullOrWhiteSpace(r.tag)) continue;
            if (string.Equals(r.tag, tag, StringComparison.OrdinalIgnoreCase))
                return r;
        }
        return null;
    }

    private static float EvaluatePosition01(TagTintRule rule, Vector3 worldPos)
    {
        float n = Mathf.PerlinNoise(
            worldPos.x * rule.noiseScale + rule.noiseOffset.x,
            worldPos.z * rule.noiseScale + rule.noiseOffset.y);

        if (!rule.addWave)
            return n;

        float wave = Mathf.Sin((worldPos.x + worldPos.z) * rule.waveScale) * 0.5f + 0.5f;
        return Mathf.Lerp(n, wave, rule.waveInfluence);
    }

    private static float ApplyBiomeBias(TagTintRule rule, TileBiome biome, float t)
    {
        float bias = biome switch
        {
            TileBiome.Forested => rule.forestBiasToGreen,
            TileBiome.Meadow => rule.meadowBiasToGreen,
            TileBiome.Mountainous => rule.mountainousBiasToGreen,
            TileBiome.Bushy => rule.bushyBiasToGreen,
            _ => 0f
        };
        return Mathf.Clamp01(Mathf.Lerp(t, 1f, bias));
    }
}
