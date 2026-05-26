using System;
using System.Collections.Generic;
using UnityEngine;
using Tile = TileGrid.Tile;

/// <summary>
/// Central perk orchestrator. Listens to game events and dispatches to active perks
/// through hook-based behaviors. The only class that bridges perks with the rest of the game.
///
/// Other systems call into PerkManager at specific seams:
///   - GameFlowController.StartSession -> OnSessionStart
///   - GameUI.TryReroll -> QueryRerollCost
///   - TileAvailabilityVisualizer (post-placement) -> OnTilePlaced
///   - TileEvents.HabitatAssigned -> automatic subscription
///   - HabitatHoverEvaluator / BiomeHabitatClassifier -> ModifyHabitatScore
/// </summary>
[DisallowMultipleComponent]
public class PerkManager : MonoBehaviour
{
    [Header("Perk Pool (all available perks for drafting)")]
    [SerializeField] private List<PerkDefinition> perkPool = new();

    [Header("Draft Settings")]
    [SerializeField, Min(1)] private int habitatsPerDraft = 5;
    [SerializeField, Min(0)] private int draftRerollsPerRun = 3;

    [Header("References")]
    [SerializeField] private TileDeck tileDeck;
    [SerializeField] private TilePlacementService placement;
    [SerializeField] private TileRuntimeStore runtimeStore;
    [SerializeField] private TileGrid tileGrid;

    private PerkRunState _state;
    private PerkDraftService _draft;
    private PerkEffectExecutor _executor;
    private readonly List<PerkCommand> _commandBuffer = new();

    private Tile _lastPlacedTile;

    // --- Public accessors for UI and other systems ---
    public PerkRunState RunState => _state;
    public PerkDraftService Draft => _draft;
    public IReadOnlyList<PerkDefinition> PerkPool => perkPool;

    /// <summary>Fired when a draft should open (UI listens to show draft screen).</summary>
    public event Action DraftTriggered;

    /// <summary>Fired when a perk is activated (UI can show notification).</summary>
    public event Action<PerkDefinition> PerkActivated;

    // --- Singleton-lite for global query access (hover/classifier) ---
    public static PerkManager Instance { get; private set; }

    private void Awake()
    {
        Instance = this;

        if (!tileDeck) tileDeck = FindAnyObjectByType<TileDeck>();
        if (!placement) placement = FindAnyObjectByType<TilePlacementService>();
        if (!runtimeStore) runtimeStore = FindAnyObjectByType<TileRuntimeStore>();
        if (!tileGrid) tileGrid = FindAnyObjectByType<TileGrid>();

        _state = new PerkRunState
        {
            HabitatsPerDraft = habitatsPerDraft,
            DraftRerollsPerRun = draftRerollsPerRun
        };
        _draft = new PerkDraftService(perkPool, _state);
        _executor = new PerkEffectExecutor(tileDeck, placement, runtimeStore, tileGrid, _state);
    }

    private void OnEnable()
    {
        TileEvents.HabitatAssigned += OnHabitatAssigned;
        TileEvents.TileStateChanged += OnTileStateChanged;
    }

    private void OnDisable()
    {
        TileEvents.HabitatAssigned -= OnHabitatAssigned;
        TileEvents.TileStateChanged -= OnTileStateChanged;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    // -------------------------------------------------------------------------
    // Session lifecycle (called by GameFlowController)
    // -------------------------------------------------------------------------

    /// <summary>Reset perk state and apply session-start perks. Call before deck rebuild.</summary>
    public void OnSessionStart()
    {
        _state.HabitatsPerDraft = habitatsPerDraft;
        _state.DraftRerollsPerRun = draftRerollsPerRun;
        _state.Reset();
        _lastPlacedTile = null;

        _commandBuffer.Clear();
        foreach (var perk in _state.ActivePerks)
            perk.behavior?.OnSessionStart(_state, _commandBuffer);
        _executor.Execute(_commandBuffer);
    }

    // -------------------------------------------------------------------------
    // Habitat assigned (automatic via TileEvents)
    // -------------------------------------------------------------------------

    private void OnHabitatAssigned(HabitatAssignmentData data)
    {
        if (data.Animal == HabitatAnimal.None) return;

        int distinctBiomes = CountDistinctBiomes(data.Tiles);
        var ctx = new HabitatContext(data, distinctBiomes, _lastPlacedTile, runtimeStore);

        _commandBuffer.Clear();
        foreach (var perk in _state.ActivePerks)
            perk.behavior?.OnHabitatAssigned(ctx, _state, _commandBuffer);
        _executor.Execute(_commandBuffer);

        if (_draft.NotifyHabitatCreated())
        {
            _draft.OpenDraft();
            DraftTriggered?.Invoke();
        }
    }

    // -------------------------------------------------------------------------
    // Reroll cost query (called by GameUI before deducting a reroll)
    // -------------------------------------------------------------------------

    /// <summary>
    /// Returns true if the reroll should be free (perk consumed a charge).
    /// GameUI calls this before decrementing its reroll counter.
    /// </summary>
    public bool QueryRerollIsFree()
    {
        foreach (var perk in _state.ActivePerks)
            if (perk.behavior != null && perk.behavior.TryConsumeFreeReroll(_state))
                return true;
        return false;
    }

    /// <summary>Total rerolls available: base + perk bonus.</summary>
    public int GetTotalStartingRerolls(int baseRerolls)
        => baseRerolls + _state.BonusRerolls;

    /// <summary>Total deck tiles: base + perk bonus.</summary>
    public int GetTotalDeckSize(int baseDeckSize)
        => baseDeckSize + _state.BonusDeckTiles;

    // -------------------------------------------------------------------------
    // Tile placed (called by TileAvailabilityVisualizer after placement commit)
    // -------------------------------------------------------------------------

    /// <summary>
    /// Notify perks about a placed tile. World-effect perks can produce spawn/transform commands.
    /// Call after placement is committed but before the frame ends.
    /// </summary>
    public void OnTilePlaced(PlacementContext ctx)
    {
        _lastPlacedTile = ctx.PlacedTile;
        _commandBuffer.Clear();
        foreach (var perk in _state.ActivePerks)
            perk.behavior?.OnTilePlaced(ctx, _state, _commandBuffer);
        _executor.Execute(_commandBuffer);
    }

    // -------------------------------------------------------------------------
    // Habitat evaluation modifier (called by classifier and hover evaluator)
    // -------------------------------------------------------------------------

    /// <summary>
    /// Shared pipeline: both hover preview and final classifier call this
    /// so perk-modified scores stay consistent.
    /// </summary>
    public float ModifyHabitatScore(HabitatAnimal animal, float baseScore,
        int regionTileCount, int distinctBiomes)
    {
        float score = baseScore;
        foreach (var perk in _state.ActivePerks)
            if (perk.behavior != null)
                score = perk.behavior.ModifyHabitatScore(
                    animal, score, regionTileCount, distinctBiomes, _state);
        return score;
    }

    // -------------------------------------------------------------------------
    // Draft actions (called by UI)
    // -------------------------------------------------------------------------

    public bool DraftRerollSlot(int slotIndex) => _draft.RerollSlot(slotIndex);

    public PerkDefinition DraftConfirmPick(int slotIndex)
    {
        var picked = _draft.ConfirmPick(slotIndex);
        if (picked != null)
        {
            // Immediately apply session-start effects for mid-run picks
            // (e.g. Mulligan grants +2 rerolls, Seed Bank grants +5 tiles right away).
            _commandBuffer.Clear();
            picked.behavior?.OnSessionStart(_state, _commandBuffer);
            _executor.Execute(_commandBuffer);
            PerkActivated?.Invoke(picked);
        }
        return picked;
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private void OnTileStateChanged(Tile tile)
    {
        if (tile != null && runtimeStore != null)
        {
            var rt = runtimeStore.Get(tile);
            if (rt != null && rt.occupied)
                _lastPlacedTile = tile;
        }
    }

    private int CountDistinctBiomes(IReadOnlyList<Tile> tiles)
    {
        if (tiles == null || runtimeStore == null) return 0;
        int mask = 0;
        for (int i = 0; i < tiles.Count; i++)
        {
            var rt = runtimeStore.Get(tiles[i]);
            if (rt != null && rt.biome != TileBiome.None)
                mask |= 1 << (int)rt.biome;
        }
        int count = 0;
        while (mask != 0) { count += mask & 1; mask >>= 1; }
        return count;
    }
}
