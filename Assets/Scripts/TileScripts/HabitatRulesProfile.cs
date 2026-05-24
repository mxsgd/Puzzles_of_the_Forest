using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Per-habitat rules (core tile requirements, etc.). Falls back to defaults when no override exists.
/// </summary>
[CreateAssetMenu(fileName = "HabitatRulesProfile", menuName = "Idle Forest/Habitat Rules Profile")]
public class HabitatRulesProfile : ScriptableObject
{
    [Serializable]
    public struct CoreRuleEntry
    {
        public HabitatAnimal animal;
        [Min(0)] public int minNeighborsForCore;
        [Min(0)] public int requiredCoreTiles;
    }

    [Header("Domyślne (rdzeń habitatu)")]
    [SerializeField, Min(1)] private int defaultMinNeighborsForCore = 3;
    [SerializeField, Min(1)] private int defaultRequiredCoreTiles = 2;

    [Header("Nadpisania per zwierzę (0 = użyj domyślnej)")]
    [SerializeField] private List<CoreRuleEntry> coreOverrides = new();

    public int GetMinNeighborsForCore(HabitatAnimal animal)
    {
        for (int i = 0; i < coreOverrides.Count; i++)
        {
            var e = coreOverrides[i];
            if (e.animal != animal) continue;
            return e.minNeighborsForCore > 0 ? e.minNeighborsForCore : defaultMinNeighborsForCore;
        }
        return defaultMinNeighborsForCore;
    }

    public int GetRequiredCoreTiles(HabitatAnimal animal)
    {
        for (int i = 0; i < coreOverrides.Count; i++)
        {
            var e = coreOverrides[i];
            if (e.animal != animal) continue;
            return e.requiredCoreTiles > 0 ? e.requiredCoreTiles : defaultRequiredCoreTiles;
        }
        return defaultRequiredCoreTiles;
    }
}
