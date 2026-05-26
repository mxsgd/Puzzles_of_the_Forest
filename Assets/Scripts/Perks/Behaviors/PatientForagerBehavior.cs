using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Patient Forager: each habitat created grants 1 free reroll charge.
/// On reroll, if charges > 0, consume one instead of a normal reroll.
/// Hook: HabitatAssigned -> GrantFreeRerollCharges(+1)
/// Hook: RerollCost -> TryConsumeFreeReroll
/// </summary>
[CreateAssetMenu(fileName = "PatientForager", menuName = "Idle Forest/Perk Behaviors/Patient Forager")]
public class PatientForagerBehavior : PerkBehavior
{
    [SerializeField] private int chargesPerHabitat = 1;

    public override void OnHabitatAssigned(HabitatContext ctx, PerkRunState state, List<PerkCommand> commands)
    {
        commands.Add(PerkCommand.GrantFreeRerollCharges(chargesPerHabitat));
    }

    public override bool TryConsumeFreeReroll(PerkRunState state)
    {
        if (state.FreeRerollCharges <= 0) return false;
        state.FreeRerollCharges--;
        return true;
    }
}
