using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Stałe identyfikatory wariantów biomu (używane w Inspectorze, talii i późniejszych strefach klimatu).
/// </summary>
public static class BiomeVariantIds
{
    public const string Default = "default";
    public const string Coniferous = "coniferous";
    public const string Deciduous = "deciduous";
}

/// <summary>
/// Jeden wariant biomu — własna pula dekoracji (drzewa, krzaki, skały…).
/// Ten sam <see cref="TileBiome"/> może mieć wiele wariantów (np. las iglasty i liściasty).
/// </summary>
[Serializable]
public class BiomeVariantProfile
{
    [Tooltip("Biom logiczny (habitat, wektor biomu) — bez zmian względem enuma.")]
    public TileBiome biome = TileBiome.None;

    [Tooltip("Unikalny id wariantu w ramach biomu, np. coniferous / deciduous / default.")]
    public string variantId = BiomeVariantIds.Default;

    [Min(1), Tooltip("Waga przy losowaniu wariantu (2 warianty po 1 = 50/50). Później nadpisze strefa klimatu.")]
    public int weight = 1;

    [Tooltip("Dekoracje tego wariantu — tagi Tree, Bush, Grass…")]
    public List<BiomeTilePopulator.ContentPrefab> contents = new();
}

/// <summary>
/// Wybór wariantu biomu. Na razie równomierne losowanie wg wag profili.
/// Później strefy klimatu mogą wstrzyknąć własną politykę zamiast <see cref="PickWeighted"/>.
/// </summary>
public static class BiomeVariantSelector
{
    /// <summary>
    /// Zwraca wariant do użycia. Gdy <paramref name="forcedVariantId"/> jest ustawiony i istnieje — używa go.
    /// W przeciwnym razie losuje wg wag (domyślnie 50/50 przy dwóch wariantach o wadze 1).
    /// </summary>
    public static string ResolveVariantId(
        string forcedVariantId,
        IReadOnlyList<BiomeVariantProfile> candidates,
        System.Random rng)
    {
        if (candidates == null || candidates.Count == 0)
            return BiomeVariantIds.Default;

        if (!string.IsNullOrWhiteSpace(forcedVariantId))
        {
            var trimmed = forcedVariantId.Trim();
            for (int i = 0; i < candidates.Count; i++)
            {
                var c = candidates[i];
                if (c != null && string.Equals(c.variantId, trimmed, StringComparison.OrdinalIgnoreCase))
                    return c.variantId;
            }
        }

        return PickWeighted(candidates, rng);
    }

    public static string PickWeighted(IReadOnlyList<BiomeVariantProfile> candidates, System.Random rng)
    {
        if (candidates == null || candidates.Count == 0)
            return BiomeVariantIds.Default;

        int totalWeight = 0;
        for (int i = 0; i < candidates.Count; i++)
        {
            var c = candidates[i];
            if (c == null || !HasUsableContent(c)) continue;
            totalWeight += Mathf.Max(1, c.weight);
        }

        if (totalWeight <= 0)
            return candidates[0]?.variantId ?? BiomeVariantIds.Default;

        int roll = rng.Next(totalWeight);
        for (int i = 0; i < candidates.Count; i++)
        {
            var c = candidates[i];
            if (c == null || !HasUsableContent(c)) continue;
            roll -= Mathf.Max(1, c.weight);
            if (roll < 0)
                return c.variantId;
        }

        return candidates[0].variantId;
    }

    public static bool HasUsableContent(BiomeVariantProfile profile)
    {
        if (profile?.contents == null) return false;
        for (int i = 0; i < profile.contents.Count; i++)
        {
            var def = profile.contents[i];
            if (def?.prefabs == null) continue;
            for (int p = 0; p < def.prefabs.Count; p++)
                if (def.prefabs[p]?.prefab != null) return true;
        }
        return false;
    }
}
