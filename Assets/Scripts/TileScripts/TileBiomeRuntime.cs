using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Komponent doklejany do instancji bazowego Tile.prefab po jego postawieniu.
/// Trzyma biom kafla i jego 12 slotów-trójkątów, w których kod gry rozmieszcza
/// drzewa, krzaki, kamienie, kwiaty itd.
/// </summary>
public class TileBiomeRuntime : MonoBehaviour
{
    [SerializeField] private TileBiome biome = TileBiome.None;
    [SerializeField] private float hexRadius = 1f;
    [SerializeField] private List<TileTriangleSlot> slots = new();

    public TileBiome Biome => biome;
    public float HexRadius => hexRadius;
    public IReadOnlyList<TileTriangleSlot> Slots => slots;

    /// <summary>Czyści sloty i tworzy 12 nowych zgodnie z layoutem heksu.</summary>
    public void Initialize(TileBiome tileBiome, float radius)
    {
        biome = tileBiome;
        hexRadius = Mathf.Max(0.0001f, radius);

        ClearAll();
        slots = new List<TileTriangleSlot>(HexTileLayout.TriangleCount);
        foreach (var t in HexTileLayout.GetTrianglesR1())
        {
            slots.Add(new TileTriangleSlot
            {
                sideEdge = t.sideEdge,
                sideHypo = t.sideHypo,
                localCentroid = new Vector3(t.centroidR1.x * hexRadius, 0f, t.centroidR1.y * hexRadius)
            });
        }
    }

    public TileTriangleSlot GetSlot(int sideEdge, int sideHypo)
    {
        foreach (var s in slots)
            if (s.sideEdge == sideEdge && s.sideHypo == sideHypo) return s;
        return null;
    }

    public TileTriangleSlot GetSlot(string id)
    {
        foreach (var s in slots) if (s.Id == id) return s;
        return null;
    }

    public IEnumerable<TileTriangleSlot> GetSlotsByEdgeSide(int sideEdge)
    {
        foreach (var s in slots)
            if (s.sideEdge == sideEdge) yield return s;
    }

    public Vector3 GetWorldPosition(TileTriangleSlot slot)
    {
        if (slot == null) return transform.position;
        return transform.position + slot.localCentroid;
    }

    public void ClearAll()
    {
        if (slots == null) return;
        foreach (var s in slots) s?.Clear();
    }

    private void OnDrawGizmosSelected()
    {
        if (slots == null) return;
        Gizmos.color = Color.yellow;
        foreach (var s in slots)
        {
            if (s == null) continue;
            var p = transform.position + s.localCentroid;
            Gizmos.DrawSphere(p, hexRadius * 0.04f);
#if UNITY_EDITOR
            UnityEditor.Handles.Label(p + Vector3.up * 0.1f, s.Id);
#endif
        }
    }
}
