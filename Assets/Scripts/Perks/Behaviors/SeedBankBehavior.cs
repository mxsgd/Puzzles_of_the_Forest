using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Seed Bank: +5 tiles in the starting deck.
/// Hook: SessionStart -> AddDeckTiles(+5)
/// </summary>
[CreateAssetMenu(fileName = "SeedBank", menuName = "Idle Forest/Perk Behaviors/Seed Bank")]
public class SeedBankBehavior : PerkBehavior
{
    [SerializeField] private int bonusTiles = 5;

    public override void OnSessionStart(PerkRunState state, List<PerkCommand> commands)
    {
        commands.Add(PerkCommand.AddDeckTiles(bonusTiles));
    }
}
