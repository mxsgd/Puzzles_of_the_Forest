using UnityEngine;

/// <summary>Canonical occupant scale — always from prefab, never from mid-animation transform.</summary>
public static class TileOccupantScale
{
    public static Vector3 GetCanonicalLocalScale(GameObject instance, TileRuntimeStore.Runtime rt)
    {
        if (rt?.templatePrefab != null)
            return rt.templatePrefab.transform.localScale;

        if (instance != null)
            return instance.transform.localScale;

        return Vector3.one;
    }

    public static void ApplyCanonicalLocalScale(Transform target, GameObject instance, TileRuntimeStore.Runtime rt)
    {
        if (target == null) return;
        target.localScale = GetCanonicalLocalScale(instance, rt);
    }
}
