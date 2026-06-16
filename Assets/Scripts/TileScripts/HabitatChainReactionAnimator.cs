using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Tile = TileGrid.Tile;

/// <summary>
/// Chain reaction po stworzeniu habitatu:
/// każdy kafelek po kolei podnosi się, podświetla (highlight color), a dopiero
/// wtedy dostaje właściwy kolor habitatu w shaderze (_HabitatColor / _HabitatStrength).
///
/// Wymaga: HabitatGridManager.deferInfectionToAnimator = true.
/// Po zakończeniu podnosi TileEvents.HabitatChainCompleted(habitatId).
/// </summary>
[DisallowMultipleComponent]
public class HabitatChainReactionAnimator : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private HabitatGridManager gridManager;
    [SerializeField] private TileRuntimeStore runtimeStore;
    [SerializeField] private HabitatTintProfile tintProfile;

    [Header("Chain — opóźnienie między kafelkami")]
    [SerializeField, Min(0f)] private float delayBetweenTiles = 0.1f;

    [Header("Rise — uniesienie kafelka w górę")]
    [SerializeField, Min(0f)]    private float riseHeight   = 0.45f;
    [SerializeField, Min(0.02f)] private float riseDuration = 0.14f;
    [SerializeField, Min(0f)]    private float peakHold     = 0.06f;
    [SerializeField, Min(0.02f)] private float fallDuration = 0.18f;

    [Header("Highlight — skala przy podświetleniu")]
    [SerializeField, Min(1f)] private float highlightScaleMultiplier = 1.13f;

    [Header("Highlight — kolor w shaderze zanim zmieni się na habitat")]
    [SerializeField] private bool useHighlightColorFlash = true;
    [ColorUsage(true, true)]
    [SerializeField] private Color highlightColor = new Color(1.8f, 1.8f, 1.5f, 1f);
    [SerializeField, Min(0f)] private float highlightFlashDuration = 0.07f;

    [Header("Chain SFX — rosnący pitch")]
    [SerializeField] private GameSfxCatalog sfxCatalog;
    [SerializeField] private AudioSource chainAudioSource;
    [SerializeField] private AudioClip   chainTileClip;
    [SerializeField, Range(0f, 1f)] private float chainVolume   = 0.75f;
    [SerializeField] private float basePitch  = 0.85f;
    [SerializeField] private float pitchStep  = 0.07f;
    [SerializeField] private float maxPitch   = 2.0f;

    private void Awake()
    {
        if (!gridManager) gridManager = FindAnyObjectByType<HabitatGridManager>();
        if (!runtimeStore) runtimeStore = FindAnyObjectByType<TileRuntimeStore>();

        if (!chainAudioSource)
            chainAudioSource = GetComponent<AudioSource>();
        if (!chainAudioSource)
        {
            chainAudioSource = gameObject.AddComponent<AudioSource>();
            chainAudioSource.playOnAwake = false;
        }

        if (sfxCatalog == null)
            sfxCatalog = GameSfxCatalog.Default;

        if (chainTileClip == null && sfxCatalog != null)
            chainTileClip = sfxCatalog.chainTileClip;

        if (chainVolume <= 0f && sfxCatalog != null)
            chainVolume = sfxCatalog.chainVolume;
    }

    private void OnEnable()  => TileEvents.HabitatAssigned += OnHabitatAssigned;
    private void OnDisable() => TileEvents.HabitatAssigned -= OnHabitatAssigned;

    private void OnHabitatAssigned(HabitatAssignmentData data)
    {
        if (data.Tiles == null || data.Tiles.Count == 0)
        {
            TileEvents.RaiseHabitatChainCompleted(data.HabitatId);
            return;
        }

        Color habitatColor = Color.white;
        tintProfile?.TryGetColor(data.Animal, out habitatColor);

        StartCoroutine(ChainReactionRoutine(data.HabitatId, new List<Tile>(data.Tiles), habitatColor));
    }

    private IEnumerator ChainReactionRoutine(int habitatId, List<Tile> tiles, Color habitatColor)
    {
        for (int i = 0; i < tiles.Count; i++)
        {
            Tile tile = tiles[i];
            if (tile == null) continue;

            float pitch = Mathf.Min(basePitch + pitchStep * i, maxPitch);
            PlayChainTick(pitch);

            var rt  = runtimeStore?.Get(tile);
            var pos = new Vector2Int(tile.q, tile.r);

            StartCoroutine(AnimateTile(rt?.occupantInstance, pos, habitatColor));

            if (i < tiles.Count - 1 && delayBetweenTiles > 0f)
                yield return new WaitForSeconds(delayBetweenTiles);
        }

        // Czekaj aż ostatni kafel skończy animację (rise + flash + fall).
        float lastTileAnimDuration = riseDuration + Mathf.Max(highlightFlashDuration, peakHold) + fallDuration;
        if (lastTileAnimDuration > 0f)
            yield return new WaitForSeconds(lastTileAnimDuration);

        TileEvents.RaiseHabitatChainCompleted(habitatId);
    }

    private void PlayChainTick(float pitch)
    {
        if (chainAudioSource == null || chainTileClip == null)
            return;

        chainAudioSource.pitch = pitch;
        chainAudioSource.PlayOneShot(chainTileClip, chainVolume);
    }

    private IEnumerator AnimateTile(GameObject instance, Vector2Int tilePos, Color habitatColor)
    {
        Transform tr = instance != null ? instance.transform : null;
        var rt = ResolveRuntime(instance);
        Vector3 origScale = TileOccupantScale.GetCanonicalLocalScale(instance, rt);
        Vector3 origPos   = tr != null ? tr.localPosition : Vector3.zero;
        Vector3 peakPos   = origPos   + Vector3.up * riseHeight;
        Vector3 peakScale = origScale * highlightScaleMultiplier;

        // ── 1. Rise ──────────────────────────────────────────────────────────
        yield return LerpTransform(tr, origPos, origScale, peakPos, peakScale, riseDuration);

        // ── 2. Highlight flash ────────────────────────────────────────────────
        if (useHighlightColorFlash && highlightFlashDuration > 0f)
        {
            gridManager?.InfectTilePublic(tilePos, highlightColor, 1f);
            yield return new WaitForSeconds(highlightFlashDuration);
        }
        else if (peakHold > 0f)
        {
            yield return new WaitForSeconds(peakHold);
        }

        // ── 3. Właściwy kolor habitatu ────────────────────────────────────────
        gridManager?.InfectTilePublic(tilePos, habitatColor, 1f);

        // ── 4. Fall ───────────────────────────────────────────────────────────
        Vector3 fallStartPos   = tr != null ? tr.localPosition : peakPos;
        Vector3 fallStartScale = tr != null ? tr.localScale    : peakScale;
        yield return LerpTransform(tr, fallStartPos, fallStartScale, origPos, origScale, fallDuration);

        if (tr != null)
        {
            tr.localPosition = origPos;
            tr.localScale    = origScale;
        }
    }

    private TileRuntimeStore.Runtime ResolveRuntime(GameObject instance)
    {
        if (instance == null || runtimeStore == null)
            return null;

        var tileObj = instance.GetComponent<TileObject>();
        if (tileObj?.Tile == null)
            return null;

        return runtimeStore.Get(tileObj.Tile);
    }

    private static IEnumerator LerpTransform(
        Transform tr,
        Vector3 fromPos, Vector3 fromScale,
        Vector3 toPos,   Vector3 toScale,
        float duration)
    {
        if (duration <= 0f)
        {
            if (tr != null) { tr.localPosition = toPos; tr.localScale = toScale; }
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < duration)
        {
            if (tr == null) yield break;
            elapsed += Time.deltaTime;
            float p = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / duration));
            tr.localPosition = Vector3.Lerp(fromPos,   toPos,   p);
            tr.localScale    = Vector3.Lerp(fromScale, toScale, p);
            yield return null;
        }

        if (tr != null)
        {
            tr.localPosition = toPos;
            tr.localScale    = toScale;
        }
    }
}
