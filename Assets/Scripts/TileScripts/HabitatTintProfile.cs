using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "HabitatTintProfile", menuName = "Idle Forest/Habitat Tint Profile")]
public class HabitatTintProfile : ScriptableObject
{
    [Serializable]
    public struct HabitatTintEntry
    {
        public HabitatAnimal animal;
        public Color color;
    }

    [SerializeField] private Color defaultColor = Color.white;
    [SerializeField] private List<HabitatTintEntry> tints = new();

    public Color DefaultColor => defaultColor;

    public bool TryGetColor(HabitatAnimal animal, out Color color)
    {
        for (int i = 0; i < tints.Count; i++)
        {
            var entry = tints[i];
            if (entry.animal != animal) continue;
            color = entry.color;
            return true;
        }

        color = defaultColor;
        return false;
    }
}
