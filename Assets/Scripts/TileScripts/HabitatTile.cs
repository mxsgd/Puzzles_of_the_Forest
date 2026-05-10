using UnityEngine;

[System.Serializable]
public class HabitatTile
{
    public Vector2Int gridPos;
    [Range(0f, 1f)] public float influence;
    [Range(0f, 1f)] public float targetInfluence;
    public Color habitatColor = Color.white;
}
