using System.Collections.Generic;

/// <summary>
/// Mutable runtime state for one run's perk system.
/// Reset at session start. Holds active perks, draft progress, and per-perk counters.
/// </summary>
public class PerkRunState
{
    public readonly List<PerkDefinition> ActivePerks = new();

    // --- Draft progression ---
    public int HabitatsSinceLastDraft;
    public int HabitatsPerDraft = 5;

    // --- Draft offer (when a draft is open) ---
    public readonly PerkDefinition[] CurrentOffer = new PerkDefinition[3];
    public bool DraftOpen;
    public int DraftRerollsRemaining = 3;
    public int DraftRerollsPerRun = 3;

    // --- Perk-managed counters ---
    public int FreeRerollCharges;
    public int BonusRerolls;
    public int BonusDeckTiles;

    // --- Perk-specific counters (generic bag for custom perks) ---
    private readonly Dictionary<string, int> _counters = new();

    public int GetCounter(string key) => _counters.TryGetValue(key, out var v) ? v : 0;
    public void SetCounter(string key, int value) => _counters[key] = value;
    public void IncrementCounter(string key, int delta = 1)
    {
        _counters.TryGetValue(key, out var v);
        _counters[key] = v + delta;
    }

    public void Reset()
    {
        ActivePerks.Clear();
        HabitatsSinceLastDraft = 0;

        for (int i = 0; i < CurrentOffer.Length; i++)
            CurrentOffer[i] = null;
        DraftOpen = false;
        DraftRerollsRemaining = DraftRerollsPerRun;

        FreeRerollCharges = 0;
        BonusRerolls = 0;
        BonusDeckTiles = 0;
        _counters.Clear();
    }

    public bool HasPerk(string perkId)
    {
        for (int i = 0; i < ActivePerks.Count; i++)
            if (ActivePerks[i] != null && ActivePerks[i].perkId == perkId)
                return true;
        return false;
    }
}
