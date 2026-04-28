using System;
using UnityEngine;

/// <summary>
/// Jedno z 12 pól (trójkąt prostokątny) wewnątrz kafla heksagonalnego.
/// Identyfikowany parą (sideEdge, sideHypo) lub stringiem "ab".
/// Może trzymać tag obiektu (Tree/Bush/Rock/...) i referencję do instancji.
/// </summary>
[Serializable]
public class TileTriangleSlot
{
    [Range(1, 6)] public int sideEdge;
    [Range(1, 6)] public int sideHypo;

    public Vector3 localCentroid;

    public string contentTag;
    public GameObject contentInstance;

    public string Id => $"{sideEdge}{sideHypo}";

    public bool IsEmpty => string.IsNullOrEmpty(contentTag) && contentInstance == null;

    public void Clear()
    {
        contentTag = null;
        if (contentInstance != null) UnityEngine.Object.Destroy(contentInstance);
        contentInstance = null;
    }
}
