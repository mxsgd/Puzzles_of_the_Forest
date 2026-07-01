using System;
using System.Collections.Generic;
using UnityEngine;

public enum QuestType { Main, Side }

public enum QuestConditionType
{
    /// <summary>Zbuduj habitat danego zwierzęcia z N+ kaflami (po merge).</summary>
    HabitatMinTiles,
    /// <summary>Stwórz N habitatów danego zwierzęcia (None = dowolne).</summary>
    HabitatCount,
}

public enum QuestRewardType
{
    PerkChoice,
    AddRerolls,
    AddDeckCards,
    AddPoints,
    AddBiomeTiles,
}

[Serializable]
public class QuestReward
{
    public QuestRewardType type;
    [Min(1)] public int amount = 1;
    public TileBiome biome = TileBiome.None; // dla AddBiomeTiles
}

[Serializable]
public class QuestDefinition
{
    public string id;
    public string displayName;
    [TextArea(2, 4)] public string description;
    public QuestType questType;

    [Header("Warunek")]
    public QuestConditionType conditionType;
    public HabitatAnimal conditionAnimal; // None = dowolny
    [Min(1)] public int conditionTarget = 1;

    [Header("Nagrody")]
    public List<QuestReward> rewards = new();

    // ── Pomocnicze ──────────────────────────────────────────────
    public string GoalLabel()
    {
        string animalName = conditionAnimal == HabitatAnimal.None
            ? "any animal"
            : AnimalDisplayName(conditionAnimal);

        return conditionType switch
        {
            QuestConditionType.HabitatMinTiles =>
                $"Build a {animalName} habitat\nwith {conditionTarget}+ tiles",
            QuestConditionType.HabitatCount =>
                conditionTarget == 1
                    ? $"Create 1 {animalName} habitat"
                    : $"Create {conditionTarget} {animalName} habitats",
            _ => description
        };
    }

    public string RewardsLabel()
    {
        if (rewards == null || rewards.Count == 0) return "";
        var parts = new System.Text.StringBuilder();
        foreach (var r in rewards)
        {
            if (parts.Length > 0) parts.Append(", ");
            parts.Append(r.type switch
            {
                QuestRewardType.PerkChoice    => "Choose 1 of 3 perks",
                QuestRewardType.AddRerolls    => $"+{r.amount} reroll{(r.amount == 1 ? "" : "s")}",
                QuestRewardType.AddDeckCards  => $"+{r.amount} card{(r.amount == 1 ? "" : "s")} to deck",
                QuestRewardType.AddPoints     => $"+{r.amount} points",
                QuestRewardType.AddBiomeTiles => $"+{r.amount} {r.biome} tile{(r.amount == 1 ? "" : "s")} to deck",
                _ => r.type.ToString()
            });
        }
        return parts.ToString();
    }

    private static string AnimalDisplayName(HabitatAnimal a) => a switch
    {
        HabitatAnimal.Deer        => "deer",
        HabitatAnimal.Beaver      => "beaver",
        HabitatAnimal.Bear        => "bear",
        HabitatAnimal.Bees        => "bees",
        _                         => a.ToString().ToLowerInvariant()
    };
}
