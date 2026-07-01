using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// ScriptableObject listing main and side quests.
/// Generate asset: Idle Forest → Quests → Ensure Quest Catalog.
/// </summary>
[CreateAssetMenu(fileName = "QuestCatalog", menuName = "Idle Forest/Quest Catalog")]
public class QuestCatalog : ScriptableObject
{
    [SerializeField] private List<QuestDefinition> mainQuests = new();
    [SerializeField] private List<QuestDefinition> sideQuests = new();

    public IReadOnlyList<QuestDefinition> MainQuests => mainQuests;
    public IReadOnlyList<QuestDefinition> SideQuests => sideQuests;

    private static QuestCatalog _default;
    public static QuestCatalog Default
    {
        get
        {
            if (_default != null) return _default;
            _default = Resources.Load<QuestCatalog>("QuestCatalog");
            if (_default == null)
            {
                _default = CreateInstance<QuestCatalog>();
                _default.PopulateDefaults();
            }
            return _default;
        }
    }

    [ContextMenu("Populate Defaults")]
    public void PopulateDefaults()
    {
        mainQuests = BuildDefaultMainQuests();
        sideQuests = BuildDefaultSideQuests();
#if UNITY_EDITOR
        UnityEditor.EditorUtility.SetDirty(this);
#endif
    }

    public static List<QuestDefinition> BuildDefaultMainQuests()
    {
        return new List<QuestDefinition>
        {
            Main("main_deer_1",    "Great Herd",          HabitatAnimal.Deer,   10),
            Main("main_deer_2",    "Herd Migration",      HabitatAnimal.Deer,   15),
            Main("main_beaver_1",  "Great Dam",           HabitatAnimal.Beaver, 10),
            Main("main_beaver_2",  "Wetland Network",     HabitatAnimal.Beaver, 15),
            Main("main_bees_1",    "Great Swarm",         HabitatAnimal.Bees,   10),
            Main("main_bees_2",    "Great Colony",        HabitatAnimal.Bees,   15),
            Main("main_bear_1",    "King of the Forest",  HabitatAnimal.Bear,   10),
            Main("main_bear_2",    "Predator Territory",  HabitatAnimal.Bear,   15),
        };
    }

    public static List<QuestDefinition> BuildDefaultSideQuests()
    {
        return new List<QuestDefinition>
        {
            Side("side_deer_1",    "First Herd",          HabitatAnimal.Deer,   1,
                Reward(QuestRewardType.AddRerolls, 1)),
            Side("side_deer_2",    "Two Herds",           HabitatAnimal.Deer,   2,
                Reward(QuestRewardType.AddDeckCards, 2)),

            Side("side_beaver_1",  "Small Dam",           HabitatAnimal.Beaver, 1,
                Reward(QuestRewardType.AddDeckCards, 2)),
            Side("side_beaver_2",  "Beaver Lodge",        HabitatAnimal.Beaver, 2,
                Reward(QuestRewardType.AddRerolls, 1),
                BiomeReward(TileBiome.Water, 1)),

            Side("side_bees_1",    "First Hive",          HabitatAnimal.Bees,   1,
                Reward(QuestRewardType.AddRerolls, 1)),
            Side("side_bees_2",    "Blooming Meadow",     HabitatAnimal.Bees,   2,
                Reward(QuestRewardType.AddDeckCards, 2)),

            Side("side_bear_1",    "Den",                 HabitatAnimal.Bear,   1,
                Reward(QuestRewardType.AddPoints, 1000)),
            Side("side_bear_2",    "Predator Tracks",     HabitatAnimal.Bear,   2,
                BiomeReward(TileBiome.Rocks, 2)),

            Side("side_any_1",     "Ecosystem Start",     HabitatAnimal.None,   2,
                Reward(QuestRewardType.AddRerolls, 1)),
            Side("side_any_2",     "Living Forest",       HabitatAnimal.None,   4,
                Reward(QuestRewardType.AddDeckCards, 3)),
        };
    }

    // ─── builders ───────────────────────────────────────────────────────

    private static QuestDefinition Main(string id, string name, HabitatAnimal animal, int tiles)
    {
        string animalName = AnimalName(animal);
        return new QuestDefinition
        {
            id = id,
            displayName = name,
            description = $"Build a {animalName} habitat with {tiles}+ tiles.",
            questType = QuestType.Main,
            conditionType = QuestConditionType.HabitatMinTiles,
            conditionAnimal = animal,
            conditionTarget = tiles,
            rewards = new List<QuestReward> { Reward(QuestRewardType.PerkChoice, 1) }
        };
    }

    private static QuestDefinition Side(string id, string name, HabitatAnimal animal,
        int count, params QuestReward[] rewards)
    {
        string animalName = animal == HabitatAnimal.None ? "any animal" : AnimalName(animal);
        string goal = count == 1
            ? $"Create 1 {animalName} habitat."
            : $"Create {count} {animalName} habitats.";

        return new QuestDefinition
        {
            id = id,
            displayName = name,
            description = goal,
            questType = QuestType.Side,
            conditionType = QuestConditionType.HabitatCount,
            conditionAnimal = animal,
            conditionTarget = count,
            rewards = new List<QuestReward>(rewards)
        };
    }

    private static QuestReward Reward(QuestRewardType type, int amount = 1)
        => new QuestReward { type = type, amount = amount };

    private static QuestReward BiomeReward(TileBiome biome, int count)
        => new QuestReward { type = QuestRewardType.AddBiomeTiles, amount = count, biome = biome };

    private static string AnimalName(HabitatAnimal a) => a switch
    {
        HabitatAnimal.Deer        => "deer",
        HabitatAnimal.Beaver      => "beaver",
        HabitatAnimal.Bear        => "bear",
        HabitatAnimal.Bees        => "bees",
        _                         => a.ToString().ToLowerInvariant()
    };
}
