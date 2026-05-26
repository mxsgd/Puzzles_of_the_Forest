using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Mulligan: +2 rerolls at session start.
/// Hook: SessionStart -> AddRerolls(+2)
/// </summary>
[CreateAssetMenu(fileName = "Mulligan", menuName = "Idle Forest/Perk Behaviors/Mulligan")]
public class MulliganBehavior : PerkBehavior
{
    [SerializeField] private int bonusRerolls = 2;

    public override void OnSessionStart(PerkRunState state, List<PerkCommand> commands)
    {
        commands.Add(PerkCommand.AddRerolls(bonusRerolls));
    }
}
