/// <summary>UI labels for <see cref="HabitatAnimal"/>.</summary>
public static class HabitatAnimalDisplay
{
    public static string GetShortLabel(HabitatAnimal animal) => animal switch
    {
        HabitatAnimal.Deer        => "Deer",
        HabitatAnimal.Beaver      => "Beaver",
        HabitatAnimal.Bear        => "Bear",
        HabitatAnimal.Bees        => "Bees",
        HabitatAnimal.RockDweller => "Rock",
        _                         => animal.ToString()
    };
}
