using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Tile = TileGrid.Tile;

/// <summary>
/// Quick, low-impact visual for the same-biome group bonus (see <see cref="TileNeighborMatchScorer"/>).
/// Deliberately lighter than <see cref="HabitatChainReactionAnimator"/> — no tile raise, no color
/// re-tint, just a brief glowing edge, drawn the same way as habitat outlines / hover previews
/// (LineRenderer + a glow material, see <see cref="HabitatOutlineVisualizer"/> /
/// <see cref="TileNextTileHoverPreview"/>) but placed on the *shared* edge between two connected
/// same-biome tiles instead of the outer boundary of a region. After a short pause (so it doesn't
/// look like it's part of the placement itself), it lights up the placed tile's edges facing
/// same-biome neighbors, then ripples outward wave by wave, lighting each newly reached tile's
/// edges toward further same-biome connections (if any) until the whole connected group has been
/// revealed once. Each edge fades in, holds, and fades out on its own timer.
/// </summary>
public class SameBiomeConnectionAnimator : MonoBehaviour
{
    [SerializeField] private TileRuntimeStore runtimeStore;

    [Header("Edge glow")]
    [Tooltip("Leave empty to load Resources/TileSideGlow.mat.")]
    [SerializeField] private Material glowMaterial;
    [SerializeField] private float lineHeightOffset = 2.5f;
    [SerializeField, Min(0f)] private float lineInset = 0.08f;
    [SerializeField] private float lineWidth = 0.3f;

    [Header("Timing")]
    [Tooltip("Pause after the placement before the reveal starts.")]
    [SerializeField, Min(0f)] private float delayBeforeReveal = 0.5f;
    [SerializeField, Min(0f)] private float delayBetweenWaves = 0.08f;
    [SerializeField, Min(0f)] private float fadeInDuration = 0.15f;
    [SerializeField, Min(0f)] private float holdDuration = 0.25f;
    [SerializeField, Min(0f)] private float fadeOutDuration = 0.25f;

    private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

    private void Awake()
    {
        if (!runtimeStore) runtimeStore = FindAnyObjectByType<TileRuntimeStore>();
        if (!glowMaterial) glowMaterial = Resources.Load<Material>("TileSideGlow");
    }

    private void OnEnable()  => TileEvents.SameBiomeGroupScored += OnSameBiomeGroupScored;
    private void OnDisable() => TileEvents.SameBiomeGroupScored -= OnSameBiomeGroupScored;

    private void OnSameBiomeGroupScored(SameBiomeGroupData data)
    {
        if (runtimeStore == null || data.PlacedTile == null) return;
        if (data.GroupTiles == null || data.GroupTiles.Count < 2) return;
        if (!glowMaterial) glowMaterial = Resources.Load<Material>("TileSideGlow");

        StartCoroutine(CascadeRoutine(data.PlacedTile, new HashSet<Tile>(data.GroupTiles)));
    }

    private IEnumerator CascadeRoutine(Tile start, HashSet<Tile> group)
    {
        if (delayBeforeReveal > 0f)
            yield return new WaitForSeconds(delayBeforeReveal);

        var revealedTiles = new HashSet<Tile> { start };
        var revealedEdges = new HashSet<(Tile, Tile)>();
        var wave = new List<Tile> { start };

        while (wave.Count > 0)
        {
            var nextWave = new List<Tile>();

            foreach (var tile in wave)
            {
                foreach (var neighbor in tile.GetNeighbors())
                {
                    if (neighbor == null || !group.Contains(neighbor)) continue;

                    var edgeKey = EdgeKey(tile, neighbor);
                    if (revealedEdges.Add(edgeKey))
                        SpawnEdgeGlow(tile, neighbor);

                    if (revealedTiles.Add(neighbor))
                        nextWave.Add(neighbor);
                }
            }

            if (nextWave.Count == 0)
                break;

            wave = nextWave;
            if (delayBetweenWaves > 0f)
                yield return new WaitForSeconds(delayBetweenWaves);
        }
    }

    private static (Tile, Tile) EdgeKey(Tile a, Tile b)
    {
        // Order by grid coords so (a,b) and (b,a) collapse to the same key.
        if (a.q != b.q) return a.q < b.q ? (a, b) : (b, a);
        return a.r < b.r ? (a, b) : (b, a);
    }

    private void SpawnEdgeGlow(Tile tile, Tile neighbor)
    {
        if (glowMaterial == null) return;

        float y = tile.worldPos.y + lineHeightOffset;
        if (!HabitatRegionOutlineUtility.TryComputeEdgeSegment(tile, neighbor, y, lineInset, out var a, out var b))
            return;

        var go = new GameObject("SameBiomeEdgeGlow");
        go.transform.SetParent(transform, false);
        var lr = go.AddComponent<LineRenderer>();
        lr.positionCount     = 2;
        lr.startWidth        = lineWidth;
        lr.endWidth          = lineWidth * 0.8f;
        lr.useWorldSpace     = true;
        lr.numCapVertices    = 2;
        lr.numCornerVertices = 0;
        lr.material          = glowMaterial; // LineRenderer.material auto-instances a copy
        lr.SetPosition(0, a);
        lr.SetPosition(1, b);

        StartCoroutine(FadeInHoldFadeOutRoutine(go, lr.material));
    }

    private IEnumerator FadeInHoldFadeOutRoutine(GameObject go, Material instanceMat)
    {
        if (instanceMat == null) { Destroy(go); yield break; }

        Color fullEmission = instanceMat.GetColor(EmissionColorId);

        yield return LerpEmission(instanceMat, Color.black, fullEmission, fadeInDuration);
        if (holdDuration > 0f)
            yield return new WaitForSeconds(holdDuration);
        yield return LerpEmission(instanceMat, fullEmission, Color.black, fadeOutDuration);

        Destroy(go);
    }

    private static IEnumerator LerpEmission(Material mat, Color from, Color to, float duration)
    {
        if (duration <= 0f)
        {
            mat.SetColor(EmissionColorId, to);
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            if (mat == null) yield break;
            mat.SetColor(EmissionColorId, Color.Lerp(from, to, Mathf.Clamp01(elapsed / duration)));
            yield return null;
        }

        if (mat != null) mat.SetColor(EmissionColorId, to);
    }
}
