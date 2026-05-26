using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Handles perk draft logic: building offers from the pool, rerolling individual slots,
/// confirming a pick, and tracking the habitat-count trigger.
/// Pure logic — no MonoBehaviour, no UI. UI reads PerkRunState and calls methods here.
/// </summary>
public class PerkDraftService
{
    private readonly List<PerkDefinition> _pool;
    private readonly PerkRunState _state;
    private readonly HashSet<string> _offered = new();

    public PerkDraftService(List<PerkDefinition> pool, PerkRunState state)
    {
        _pool = pool;
        _state = state;
    }

    /// <summary>Call after each habitat. Returns true when a draft should open.</summary>
    public bool NotifyHabitatCreated()
    {
        _state.HabitatsSinceLastDraft++;
        if (_state.HabitatsSinceLastDraft >= _state.HabitatsPerDraft)
        {
            _state.HabitatsSinceLastDraft = 0;
            return true;
        }
        return false;
    }

    /// <summary>Opens a draft: fills 3 offer slots from the pool excluding active perks.</summary>
    public void OpenDraft()
    {
        _state.DraftOpen = true;
        _offered.Clear();
        for (int i = 0; i < _state.CurrentOffer.Length; i++)
            _state.CurrentOffer[i] = PickFromPool(i);
    }

    /// <summary>Rerolls a single slot. Returns false if no rerolls remain or pool exhausted.</summary>
    public bool RerollSlot(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= _state.CurrentOffer.Length) return false;
        if (_state.DraftRerollsRemaining <= 0) return false;

        var replacement = PickFromPool(slotIndex);
        if (replacement == null) return false;

        _state.CurrentOffer[slotIndex] = replacement;
        _state.DraftRerollsRemaining--;
        return true;
    }

    /// <summary>Player picks a perk from the offer. Activates it and closes draft.</summary>
    public PerkDefinition ConfirmPick(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= _state.CurrentOffer.Length) return null;
        var picked = _state.CurrentOffer[slotIndex];
        if (picked == null) return null;

        _state.ActivePerks.Add(picked);
        _state.DraftOpen = false;
        for (int i = 0; i < _state.CurrentOffer.Length; i++)
            _state.CurrentOffer[i] = null;
        return picked;
    }

    /// <summary>Closes draft without picking (e.g. if pool is empty).</summary>
    public void CloseDraft()
    {
        _state.DraftOpen = false;
        for (int i = 0; i < _state.CurrentOffer.Length; i++)
            _state.CurrentOffer[i] = null;
    }

    private PerkDefinition PickFromPool(int slotIndex)
    {
        var candidates = new List<PerkDefinition>();
        foreach (var p in _pool)
        {
            if (p == null) continue;
            if (_state.HasPerk(p.perkId)) continue;
            if (_offered.Contains(p.perkId)) continue;
            candidates.Add(p);
        }

        if (candidates.Count == 0) return null;
        var pick = candidates[Random.Range(0, candidates.Count)];

        var old = _state.CurrentOffer[slotIndex];
        if (old != null) _offered.Remove(old.perkId);
        _offered.Add(pick.perkId);
        return pick;
    }
}
