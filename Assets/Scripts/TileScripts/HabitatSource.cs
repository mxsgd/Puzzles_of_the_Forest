using UnityEngine;

[System.Serializable]
public class HabitatSource
{
    public Vector2Int position;
    public HabitatAnimal animal = HabitatAnimal.None;
    public Color color = Color.white;
    [Range(0f, 1f)] public float strength = 1f;
}
