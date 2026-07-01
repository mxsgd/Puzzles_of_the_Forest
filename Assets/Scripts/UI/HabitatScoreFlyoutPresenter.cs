using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using Tile = TileGrid.Tile;

/// <summary>
/// Po chain reaction + spawnie zwierzęcia pokazuje napis (np. Deer x1) i odlatuje do score.
/// Przy połączeniu wielu habitatów — sekwencja flyoutów (+pkt) z każdego dołączonego regionu.
/// </summary>
[DisallowMultipleComponent]
public class HabitatScoreFlyoutPresenter : MonoBehaviour
{
    private struct FlyoutStep
    {
        public int HabitatId;
        public string Label;
        public int Points;
    }

    [Header("References")]
    [SerializeField] private GameUI gameUI;
    [SerializeField] private Camera worldCamera;
    [SerializeField] private TileRuntimeStore runtimeStore;
    [SerializeField] private GameSfxCatalog sfxCatalog;

    [Header("Layout")]
    [SerializeField, Min(8f)] private float fontSize = 18f;
    [SerializeField] private Vector3 worldOffsetAboveAnimal = new(0f, 1.35f, 0f);
    [SerializeField] private Vector2 screenOffsetAboveAnimal = new(0f, 28f);

    [Header("Timing")]
    [SerializeField, Min(0.02f)] private float fadeInDuration = 0.22f;
    [SerializeField, Min(0f)] private float holdDuration = 0.28f;
    [SerializeField, Min(0.05f)] private float flyDuration = 0.42f;
    [SerializeField, Min(0f), Tooltip("Pauza między kolejnymi flyoutami przy merge habitatów.")]
    private float staggerBetweenMergeFlyouts = 0.16f;
    [SerializeField, Min(0f)] private float mergeFlyoutHoldDuration = 0.12f;

    [Header("Motion")]
    [SerializeField, Min(0f)] private float flyArcHeight = 42f;

    [Header("SFX")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip arriveClipOverride;
    [SerializeField, Range(0f, 1f)] private float arriveVolume = 0.85f;

    [Header("Events")]
    [SerializeField] private UnityEvent onFlyoutArrived;

    private Canvas _overlayCanvas;
    private RectTransform _overlayRoot;
    private readonly Dictionary<int, HabitatAssignmentData> _assignmentsByHabitatId = new();
    private readonly Dictionary<int, Vector3> _fallbackWorldPosByHabitatId = new();
    private HabitatMergeData? _pendingMerge;
    private Coroutine _activeSequence;

    private void Awake()
    {
        if (!gameUI)
            gameUI = GetComponent<GameUI>() ?? FindAnyObjectByType<GameUI>();

        if (!worldCamera)
            worldCamera = Camera.main;

        if (!runtimeStore)
            runtimeStore = FindAnyObjectByType<TileRuntimeStore>();

        if (!audioSource)
            audioSource = GetComponent<AudioSource>();

        if (!audioSource)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.spatialBlend = 0f;
        }

        if (sfxCatalog == null)
            sfxCatalog = GameSfxCatalog.Default;

        EnsureOverlayCanvas();
    }

    private void OnEnable()
    {
        TileEvents.HabitatAssigned += OnHabitatAssigned;
        TileEvents.HabitatMerged += OnHabitatMerged;
        TileEvents.HabitatPresentationCompleted += OnHabitatPresentationCompleted;
    }

    private void OnDisable()
    {
        TileEvents.HabitatAssigned -= OnHabitatAssigned;
        TileEvents.HabitatMerged -= OnHabitatMerged;
        TileEvents.HabitatPresentationCompleted -= OnHabitatPresentationCompleted;
    }

    private void OnHabitatAssigned(HabitatAssignmentData data)
    {
        if (data.Animal == HabitatAnimal.None)
            return;

        _pendingMerge = null;
        _assignmentsByHabitatId[data.HabitatId] = data;

        if (TryGetFallbackWorldPos(data, out var worldPos))
            _fallbackWorldPosByHabitatId[data.HabitatId] = worldPos;
    }

    private void OnHabitatMerged(HabitatMergeData data)
    {
        if (data.Animal == HabitatAnimal.None || data.MergedHabitatCount <= 1)
            return;

        _pendingMerge = data;
    }

    private void OnHabitatPresentationCompleted(int habitatId)
    {
        if (!_assignmentsByHabitatId.TryGetValue(habitatId, out var data))
            return;

        _assignmentsByHabitatId.Remove(habitatId);
        _fallbackWorldPosByHabitatId.Remove(habitatId);

        if (data.Animal == HabitatAnimal.None)
            return;

        if (_activeSequence != null)
            StopCoroutine(_activeSequence);

        if (_pendingMerge.HasValue)
        {
            var merge = _pendingMerge.Value;
            _pendingMerge = null;
            _activeSequence = StartCoroutine(PlayMergeFlyoutSequence(merge, data));
            return;
        }

        if (data.PointsAwarded <= 0)
            return;

        if (!TryGetHabitatWorldPos(habitatId, data, out var worldPos))
            return;

        worldPos += worldOffsetAboveAnimal;
        string label = $"{HabitatAnimalDisplay.GetShortLabel(data.Animal)} x1";
        _activeSequence = StartCoroutine(PlayFlyoutRoutine(label, worldPos, data.PointsAwarded, holdDuration));
    }

    private IEnumerator PlayMergeFlyoutSequence(HabitatMergeData merge, HabitatAssignmentData assignment)
    {
        var steps = BuildMergeFlyoutSteps(merge, assignment);
        if (steps.Count == 0)
        {
            _activeSequence = null;
            yield break;
        }

        for (int i = 0; i < steps.Count; i++)
        {
            var step = steps[i];
            if (!TryGetHabitatWorldPos(step.HabitatId, assignment, out var worldPos))
                continue;

            worldPos += worldOffsetAboveAnimal;
            yield return PlayFlyoutRoutine(step.Label, worldPos, step.Points, mergeFlyoutHoldDuration);

            if (i < steps.Count - 1 && staggerBetweenMergeFlyouts > 0f)
                yield return new WaitForSecondsRealtime(staggerBetweenMergeFlyouts);
        }

        _activeSequence = null;
    }

    private static List<FlyoutStep> BuildMergeFlyoutSteps(HabitatMergeData merge, HabitatAssignmentData assignment)
    {
        var steps = new List<FlyoutStep>(merge.MergedHabitatCount + 1);
        int linkRef = HabitatRequirements.ComputeAwardedPoints(merge.Animal, 2);

        if (assignment.PointsAwarded > 0)
        {
            steps.Add(new FlyoutStep
            {
                HabitatId = assignment.HabitatId,
                Label = $"+{assignment.PointsAwarded}",
                Points = assignment.PointsAwarded
            });
        }

        if (merge.AbsorbedHabitatIds != null && linkRef > 0)
        {
            var absorbed = new List<int>(merge.AbsorbedHabitatIds);
            absorbed.Sort();
            for (int i = 0; i < absorbed.Count; i++)
            {
                int habitatId = absorbed[i];
                steps.Add(new FlyoutStep
                {
                    HabitatId = habitatId,
                    Label = $"+{linkRef}",
                    Points = linkRef
                });
            }
        }

        if (merge.BasePointsAwarded > 0)
        {
            steps.Add(new FlyoutStep
            {
                HabitatId = merge.SurvivorHabitatId,
                Label = $"+{merge.BasePointsAwarded}",
                Points = merge.BasePointsAwarded
            });
        }

        return steps;
    }

    private IEnumerator PlayFlyoutRoutine(string label, Vector3 worldStart, int pointsAwarded, float hold)
    {
        if (pointsAwarded <= 0)
            yield break;

        EnsureOverlayCanvas();

        var go = new GameObject("HabitatScoreFlyout", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI), typeof(CanvasGroup));
        go.transform.SetParent(_overlayRoot, false);

        var rt = (RectTransform)go.transform;
        rt.sizeDelta = new Vector2(220f, 36f);

        var tmp = go.GetComponent<TextMeshProUGUI>();
        tmp.text = label;
        tmp.fontSize = fontSize;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = UISpriteFactory.ScoreValue;
        tmp.raycastTarget = false;
        tmp.font = TMP_Settings.defaultFontAsset;

        var group = go.GetComponent<CanvasGroup>();
        group.alpha = 0f;
        group.interactable = false;
        group.blocksRaycasts = false;

        if (!TryWorldToOverlayPoint(worldStart, out var startLocal))
        {
            Destroy(go);
            yield break;
        }

        startLocal += screenOffsetAboveAnimal;
        rt.anchoredPosition = startLocal;

        if (!TryGetScoreTargetLocal(out var endLocal))
        {
            Destroy(go);
            yield break;
        }

        float fadeT = 0f;
        while (fadeT < fadeInDuration)
        {
            fadeT += Time.unscaledDeltaTime;
            group.alpha = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(fadeT / fadeInDuration));
            yield return null;
        }

        group.alpha = 1f;

        if (hold > 0f)
            yield return new WaitForSecondsRealtime(hold);

        float flyT = 0f;
        while (flyT < flyDuration)
        {
            flyT += Time.unscaledDeltaTime;
            float p = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(flyT / flyDuration));
            var pos = Vector2.Lerp(startLocal, endLocal, p);
            pos.y += Mathf.Sin(p * Mathf.PI) * flyArcHeight;
            rt.anchoredPosition = pos;
            group.alpha = Mathf.Lerp(1f, 0.85f, p);
            yield return null;
        }

        rt.anchoredPosition = endLocal;
        Destroy(go);

        PlayArriveSfx();
        onFlyoutArrived?.Invoke();
        gameUI?.ApplyScoreFromFlyout(pointsAwarded);
    }

    private bool TryGetHabitatWorldPos(int habitatId, HabitatAssignmentData assignmentFallback, out Vector3 worldPos)
    {
        if (HabitatAnimalPlacement.TryGetSpawnAnchor(habitatId, out worldPos))
            return true;

        if (runtimeStore != null && runtimeStore.TryGetHabitatWorldAnchor(habitatId, out worldPos))
            return true;

        if (_fallbackWorldPosByHabitatId.TryGetValue(habitatId, out worldPos))
            return true;

        if (assignmentFallback.HabitatId == habitatId && TryGetFallbackWorldPos(assignmentFallback, out worldPos))
            return true;

        worldPos = default;
        return false;
    }

    private void PlayArriveSfx()
    {
        var clip = arriveClipOverride != null
            ? arriveClipOverride
            : sfxCatalog != null ? sfxCatalog.scoreFlyoutArriveClip : null;

        if (audioSource == null || clip == null)
            return;

        float volume = sfxCatalog != null && arriveClipOverride == null
            ? sfxCatalog.scoreFlyoutArriveVolume
            : arriveVolume;

        audioSource.PlayOneShot(clip, volume);
    }

    private bool TryGetScoreTargetLocal(out Vector2 localPoint)
    {
        localPoint = default;
        var scoreRect = gameUI != null ? gameUI.ScoreValueRect : null;
        if (scoreRect == null || _overlayRoot == null)
            return false;

        var screen = RectTransformUtility.WorldToScreenPoint(null, scoreRect.TransformPoint(scoreRect.rect.center));
        return RectTransformUtility.ScreenPointToLocalPointInRectangle(_overlayRoot, screen, null, out localPoint);
    }

    private bool TryWorldToOverlayPoint(Vector3 worldPos, out Vector2 localPoint)
    {
        localPoint = default;
        if (_overlayRoot == null)
            return false;

        var cam = worldCamera != null ? worldCamera : Camera.main;
        if (cam == null)
            return false;

        var screen = RectTransformUtility.WorldToScreenPoint(cam, worldPos);
        return RectTransformUtility.ScreenPointToLocalPointInRectangle(_overlayRoot, screen, null, out localPoint);
    }

    private static bool TryGetFallbackWorldPos(HabitatAssignmentData data, out Vector3 worldPos)
    {
        worldPos = default;
        var tile = data.PrimaryCoreTile;
        if (tile == null && data.Tiles != null)
        {
            for (int i = 0; i < data.Tiles.Count; i++)
            {
                if (data.Tiles[i] != null)
                {
                    tile = data.Tiles[i];
                    break;
                }
            }
        }

        if (tile == null)
            return false;

        worldPos = tile.worldPos;
        return true;
    }

    private void EnsureOverlayCanvas()
    {
        if (_overlayCanvas != null)
            return;

        var canvasGo = new GameObject("HabitatScoreFlyoutOverlay",
            typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasGo.transform.SetParent(transform, false);

        _overlayCanvas = canvasGo.GetComponent<Canvas>();
        _overlayCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        _overlayCanvas.sortingOrder = 150;
        _overlayCanvas.pixelPerfect = false;

        var scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        var raycaster = canvasGo.GetComponent<GraphicRaycaster>();
        raycaster.enabled = false;

        _overlayRoot = (RectTransform)canvasGo.transform;
    }
}
