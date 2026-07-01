using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Śledzi postęp questów, sprawdza warunki ukończenia i przyznaje nagrody.
/// Jeden main quest i jeden side quest aktywny w danej chwili.
/// </summary>
[DisallowMultipleComponent]
public class QuestManager : MonoBehaviour
{
    public static QuestManager Instance { get; private set; }

    // ── Eventy ────────────────────────────────────────────────────────────────
    /// <summary>Wywoływany gdy quest zostaje ukończony (przed przyznaniem nagrody).</summary>
    public static event Action<QuestDefinition, string> QuestCompleted;
    /// <summary>Wywoływany po każdej zmianie postępu (do odświeżenia UI).</summary>
    public static event Action QuestProgressChanged;

    // ── Refs ──────────────────────────────────────────────────────────────────
    [SerializeField] private QuestCatalog catalog;
    [SerializeField] private GameUI gameUI;
    [SerializeField] private TileDeck tileDeck;
    [SerializeField] private PerkManager perkManager;

    // ── Stan ─────────────────────────────────────────────────────────────────
    private int _activeMainIdx = 0;
    private int _activeSideIdx = 0;
    private bool _sessionActive;

    /// <summary>Ile habitatów danego zwierzęcia (netto, po korekcji merge).</summary>
    private readonly Dictionary<HabitatAnimal, int> _habitatCountByAnimal = new();
    private int _totalHabitatCount;

    /// <summary>Największa dotychczas widziana liczba kafli w grupie (per animal).</summary>
    private readonly Dictionary<HabitatAnimal, int> _maxGroupTilesByAnimal = new();

    /// <summary>Perk draft z questa czeka na chain + spawn zwierzęcia dla tego habitatu.</summary>
    private int _pendingPerkDraftHabitatId = -1;

    // ── Publiczne dane bieżących questów ─────────────────────────────────────
    public QuestDefinition ActiveMainQuest
    {
        get
        {
            var list = Catalog.MainQuests;
            return (_activeMainIdx >= 0 && _activeMainIdx < list.Count) ? list[_activeMainIdx] : null;
        }
    }

    public QuestDefinition ActiveSideQuest
    {
        get
        {
            var list = Catalog.SideQuests;
            return (_activeSideIdx >= 0 && _activeSideIdx < list.Count) ? list[_activeSideIdx] : null;
        }
    }

    /// <summary>Postęp main quest [0–1].</summary>
    public float MainQuestProgress => GetProgress(ActiveMainQuest);

    /// <summary>Postęp side quest [0–1].</summary>
    public float SideQuestProgress => GetProgress(ActiveSideQuest);

    /// <summary>Aktualny numeryczny postęp main questa (np. 7 dla "7/10").</summary>
    public int MainQuestCurrentValue => GetCurrentValue(ActiveMainQuest);

    /// <summary>Aktualny numeryczny postęp side questa.</summary>
    public int SideQuestCurrentValue => GetCurrentValue(ActiveSideQuest);

    private QuestCatalog Catalog => catalog != null ? catalog : QuestCatalog.Default;

    // ── Lifecycle ────────────────────────────────────────────────────────────
    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }
        Instance = this;
        ResolveRefs();
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private void OnEnable()
    {
        TileEvents.HabitatAssigned += OnHabitatAssigned;
        TileEvents.HabitatMerged   += OnHabitatMerged;
        TileEvents.HabitatPresentationCompleted += OnHabitatPresentationCompleted;
    }

    private void OnDisable()
    {
        TileEvents.HabitatAssigned -= OnHabitatAssigned;
        TileEvents.HabitatMerged   -= OnHabitatMerged;
        TileEvents.HabitatPresentationCompleted -= OnHabitatPresentationCompleted;
    }

    private void ResolveRefs()
    {
        if (!gameUI)      gameUI      = FindAnyObjectByType<GameUI>();
        if (!tileDeck)    tileDeck    = FindAnyObjectByType<TileDeck>();
        if (!perkManager) perkManager = FindAnyObjectByType<PerkManager>();
    }

    // ── Sesja ────────────────────────────────────────────────────────────────
    public void ResetForNewSession()
    {
        ResolveRefs();
        _habitatCountByAnimal.Clear();
        _maxGroupTilesByAnimal.Clear();
        _totalHabitatCount = 0;
        _activeMainIdx = 0;
        _activeSideIdx = 0;
        _pendingPerkDraftHabitatId = -1;
        _sessionActive = true;
        QuestProgressChanged?.Invoke();
    }

    // ── Eventy habitatów ─────────────────────────────────────────────────────
    private void OnHabitatAssigned(HabitatAssignmentData data)
    {
        if (!_sessionActive || data.Animal == HabitatAnimal.None) return;

        // Nowy habitat → increment
        _totalHabitatCount++;
        _habitatCountByAnimal.TryGetValue(data.Animal, out int prev);
        _habitatCountByAnimal[data.Animal] = prev + 1;

        // Dla HabitatMinTiles sprawdzamy tę wartość — merge będzie ją aktualizował
        UpdateMaxGroupTiles(data.Animal, data.TileCount);
        CheckAndAdvanceQuests(data.HabitatId);
        QuestProgressChanged?.Invoke();
    }

    private void OnHabitatPresentationCompleted(int habitatId)
    {
        if (_pendingPerkDraftHabitatId != habitatId)
            return;

        _pendingPerkDraftHabitatId = -1;
        ResolveRefs();
        perkManager?.TriggerPerkDraft();
    }

    private void OnHabitatMerged(HabitatMergeData data)
    {
        if (!_sessionActive || data.Animal == HabitatAnimal.None) return;

        // Korekta licznika: N sub-habitatów → 1 grupa
        int excess = data.MergedHabitatCount - 1;
        _totalHabitatCount = Mathf.Max(0, _totalHabitatCount - excess);

        _habitatCountByAnimal.TryGetValue(data.Animal, out int cur);
        _habitatCountByAnimal[data.Animal] = Mathf.Max(0, cur - excess);

        // Zaktualizuj max tiles dla połączonej grupy
        UpdateMaxGroupTiles(data.Animal, data.TileCount);

        CheckAndAdvanceQuests(deferPerkDraftForHabitatId: -1);
        QuestProgressChanged?.Invoke();
    }

    private void UpdateMaxGroupTiles(HabitatAnimal animal, int tileCount)
    {
        _maxGroupTilesByAnimal.TryGetValue(animal, out int prev);
        if (tileCount > prev)
            _maxGroupTilesByAnimal[animal] = tileCount;
    }

    // ── Sprawdzanie questów ───────────────────────────────────────────────────
    private void CheckAndAdvanceQuests(int deferPerkDraftForHabitatId = -1)
    {
        bool changed = true;
        while (changed)
        {
            changed = false;
            if (TryCompleteQuest(ActiveMainQuest, ref _activeMainIdx, Catalog.MainQuests.Count, deferPerkDraftForHabitatId))
                changed = true;
            if (TryCompleteQuest(ActiveSideQuest, ref _activeSideIdx, Catalog.SideQuests.Count, deferPerkDraftForHabitatId))
                changed = true;

            // Kolejne questy w tej samej pętli nie dziedziczą defer — tylko pierwszy habitat trigger.
            deferPerkDraftForHabitatId = -1;
        }
    }

    private bool TryCompleteQuest(QuestDefinition quest, ref int idx, int listCount, int deferPerkDraftForHabitatId)
    {
        if (quest == null) return false;
        if (!IsConditionMet(quest)) return false;

        string rewardLabel = quest.RewardsLabel();
        QuestCompleted?.Invoke(quest, rewardLabel);
        Debug.Log($"[Quest] Completed: \"{quest.displayName}\" → {rewardLabel}");

        ExecuteRewards(quest, deferPerkDraftForHabitatId);

        idx++;
        if (idx >= listCount) idx = listCount; // koniec listy = brak aktywnego
        return true;
    }

    private bool IsConditionMet(QuestDefinition quest)
    {
        if (quest == null) return false;

        return quest.conditionType switch
        {
            QuestConditionType.HabitatMinTiles => IsMinTilesMet(quest),
            QuestConditionType.HabitatCount    => IsHabitatCountMet(quest),
            _ => false
        };
    }

    private bool IsMinTilesMet(QuestDefinition quest)
    {
        if (quest.conditionAnimal == HabitatAnimal.None)
        {
            foreach (var kv in _maxGroupTilesByAnimal)
                if (kv.Value >= quest.conditionTarget) return true;
            return false;
        }
        _maxGroupTilesByAnimal.TryGetValue(quest.conditionAnimal, out int max);
        return max >= quest.conditionTarget;
    }

    private bool IsHabitatCountMet(QuestDefinition quest)
    {
        if (quest.conditionAnimal == HabitatAnimal.None)
            return _totalHabitatCount >= quest.conditionTarget;
        _habitatCountByAnimal.TryGetValue(quest.conditionAnimal, out int count);
        return count >= quest.conditionTarget;
    }

    // ── Nagrody ───────────────────────────────────────────────────────────────
    private void ExecuteRewards(QuestDefinition quest, int deferPerkDraftForHabitatId)
    {
        if (quest.rewards == null) return;
        ResolveRefs();

        foreach (var reward in quest.rewards)
        {
            switch (reward.type)
            {
                case QuestRewardType.PerkChoice:
                    if (deferPerkDraftForHabitatId >= 0)
                        _pendingPerkDraftHabitatId = deferPerkDraftForHabitatId;
                    else
                        perkManager?.TriggerPerkDraft();
                    break;

                case QuestRewardType.AddRerolls:
                    gameUI?.AddRerolls(reward.amount);
                    break;

                case QuestRewardType.AddDeckCards:
                    tileDeck?.EnqueueRandomTilesFromPool(reward.amount);
                    break;

                case QuestRewardType.AddPoints:
                    gameUI?.AddScore(reward.amount, animate: true);
                    break;

                case QuestRewardType.AddBiomeTiles:
                    if (reward.biome != TileBiome.None)
                        tileDeck?.EnqueueBiomeTile(reward.biome, reward.amount);
                    break;
            }
        }
    }

    // ── Progress helpers ─────────────────────────────────────────────────────
    private float GetProgress(QuestDefinition quest)
    {
        if (quest == null) return 1f;
        float cur = GetCurrentValue(quest);
        return Mathf.Clamp01(cur / Mathf.Max(1, quest.conditionTarget));
    }

    public int GetCurrentValue(QuestDefinition quest)
    {
        if (quest == null) return 0;

        return quest.conditionType switch
        {
            QuestConditionType.HabitatMinTiles => GetMaxTiles(quest.conditionAnimal),
            QuestConditionType.HabitatCount    => GetHabitatCount(quest.conditionAnimal),
            _ => 0
        };
    }

    private int GetMaxTiles(HabitatAnimal animal)
    {
        if (animal == HabitatAnimal.None)
        {
            int max = 0;
            foreach (var kv in _maxGroupTilesByAnimal)
                if (kv.Value > max) max = kv.Value;
            return max;
        }
        _maxGroupTilesByAnimal.TryGetValue(animal, out int v);
        return v;
    }

    private int GetHabitatCount(HabitatAnimal animal)
    {
        if (animal == HabitatAnimal.None) return _totalHabitatCount;
        _habitatCountByAnimal.TryGetValue(animal, out int v);
        return v;
    }

    // ── EnsureInstance ────────────────────────────────────────────────────────
    public static QuestManager EnsureInstance()
    {
        if (Instance != null) return Instance;
        Instance = FindAnyObjectByType<QuestManager>();
        if (Instance != null) return Instance;
        var go = new GameObject("QuestManager");
        Instance = go.AddComponent<QuestManager>();
        return Instance;
    }
}
