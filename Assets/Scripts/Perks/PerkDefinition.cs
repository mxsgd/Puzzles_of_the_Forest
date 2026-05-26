using UnityEngine;

/// <summary>
/// Immutable asset describing a single perk: its identity, UI data, and which hooks it uses.
/// Actual behavior lives in PerkBehavior subclasses referenced by <see cref="behavior"/>.
/// Create via Create > Idle Forest > Perk Definition.
/// </summary>
[CreateAssetMenu(fileName = "NewPerk", menuName = "Idle Forest/Perk Definition")]
public class PerkDefinition : ScriptableObject
{
    [Header("Identity")]
    public string perkId;
    public string displayName;
    [TextArea(2, 4)] public string description;
    public Sprite icon;

    [Header("Hook categories this perk participates in")]
    public PerkHook[] hooks = System.Array.Empty<PerkHook>();

    [Header("Behavior (assign a PerkBehavior sub-asset or reference)")]
    public PerkBehavior behavior;
}
