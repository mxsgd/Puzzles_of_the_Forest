using UnityEngine;

/// <summary>
/// Geometria pojedynczego pointy-top heksu o circumradius R = 1.
/// Boki numerowane CW (zgodnie z ruchem wskazówek) startując od lewego górnego boku:
/// 1 = lewy-górny, 2 = prawy-górny, 3 = prawy, 4 = prawy-dolny, 5 = lewy-dolny, 6 = lewy.
///
/// Heks dzielony jest na 12 prostokątnych trójkątów. Każdy trójkąt ma:
///   - krótszą nogę (połowa boku heksu) leżącą na boku „a”,
///   - dłuższą nogę (apothem) idącą od środka boku do środka heksu,
///   - przeciwprostokątną idącą od środka heksu do wierzchołka.
/// Identyfikator trójkąta: "ab" gdzie a = bok przylegający, b = sąsiedni bok przy wierzchołku.
/// </summary>
public static class HexTileLayout
{
    public const int SideCount = 6;
    public const int TriangleCount = 12;

    /// <summary>
    /// Wierzchołki pointy-top heksu w lokalnym XZ (R = 1):
    /// [0] V_TopLeft, [1] V_Top, [2] V_TopRight, [3] V_BottomRight, [4] V_Bottom, [5] V_BottomLeft.
    /// </summary>
    public static readonly Vector2[] VerticesXZ_R1 = new[]
    {
        new Vector2(-0.5f * Mathf.Sqrt(3f),  0.5f),
        new Vector2( 0f,                     1f  ),
        new Vector2( 0.5f * Mathf.Sqrt(3f),  0.5f),
        new Vector2( 0.5f * Mathf.Sqrt(3f), -0.5f),
        new Vector2( 0f,                    -1f  ),
        new Vector2(-0.5f * Mathf.Sqrt(3f), -0.5f),
    };

    /// <summary>Zwraca środek boku o numerze 1..6 w XZ (R = 1).</summary>
    public static Vector2 GetSideMidpointR1(int side)
    {
        int aStart = (side - 1 + 6) % 6;
        int aEnd   = side % 6;
        return (VerticesXZ_R1[aStart] + VerticesXZ_R1[aEnd]) * 0.5f;
    }

    /// <summary>Zwraca wierzchołek wspólny dwóch sąsiednich boków (a i b) w XZ (R = 1).</summary>
    public static Vector2 GetSharedCornerR1(int a, int b)
    {
        int aStart = (a - 1 + 6) % 6;
        int aEnd   = a % 6;
        int bStart = (b - 1 + 6) % 6;
        int bEnd   = b % 6;
        if (aStart == bStart || aStart == bEnd) return VerticesXZ_R1[aStart];
        return VerticesXZ_R1[aEnd];
    }

    /// <summary>
    /// Zwraca wszystkie 12 trójkątów heksu: (sideEdge, sideHypo, centroid w XZ przy R = 1).
    /// Centroid liczony jako średnia: środek heksu (0,0), środek boku a, wierzchołek między a i b.
    /// </summary>
    public static (int sideEdge, int sideHypo, Vector2 centroidR1)[] GetTrianglesR1()
    {
        var result = new (int, int, Vector2)[TriangleCount];
        int idx = 0;
        for (int a = 1; a <= 6; a++)
        {
            int prev = a == 1 ? 6 : a - 1;
            int next = a == 6 ? 1 : a + 1;
            result[idx++] = MakeTri(a, prev);
            result[idx++] = MakeTri(a, next);
        }
        return result;
    }

    public static string TriangleId(int sideEdge, int sideHypo) => $"{sideEdge}{sideHypo}";

    public static Vector3 LocalXZToWorld(Vector2 localXZ_R1, float radius, Vector3 tileWorldCenter)
    {
        return tileWorldCenter + new Vector3(localXZ_R1.x * radius, 0f, localXZ_R1.y * radius);
    }

    /// <summary>
    /// Zwraca dwóch sąsiadów slotu-trójkąta (sideEdge, sideHypo) wewnątrz heksu:
    ///   - ten sam bok krawędzi, drugi narożnik (dzielą apothem od środka heksu),
    ///   - sąsiedni bok zamieniony parami (dzielą przeciwprostokątną do wspólnego wierzchołka).
    /// Każdy z 12 trójkątów ma dokładnie 2 sąsiadów; razem tworzą cykl o długości 12.
    /// </summary>
    public static (int sideEdge, int sideHypo)[] GetTriangleNeighbors(int sideEdge, int sideHypo)
    {
        int prev = sideEdge == 1 ? 6 : sideEdge - 1;
        int next = sideEdge == 6 ? 1 : sideEdge + 1;
        int otherHypo = (sideHypo == prev) ? next : prev;
        return new[]
        {
            (sideEdge, otherHypo),
            (sideHypo, sideEdge)
        };
    }

    private static (int, int, Vector2) MakeTri(int a, int b)
    {
        Vector2 mid    = GetSideMidpointR1(a);
        Vector2 corner = GetSharedCornerR1(a, b);
        Vector2 centroid = (Vector2.zero + mid + corner) / 3f;
        return (a, b, centroid);
    }
}
