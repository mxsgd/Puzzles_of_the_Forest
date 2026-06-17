using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Ogranicza liczbę jednocześnie animowanych zwierząt (skinned mesh + Animator obciąża GPU).
/// Starsze instancje zostają widoczne, ale bez animacji.
/// </summary>
public static class HabitatAnimalAnimatorBudget
{
    private const int DefaultMaxAnimatedAnimals = 8;

    private static readonly List<GameObject> s_spawnOrder = new();
    private static int s_maxAnimated = DefaultMaxAnimatedAnimals;

    public static void Configure(int maxAnimatedAnimals)
    {
        s_maxAnimated = Mathf.Max(1, maxAnimatedAnimals);
        ApplyBudget();
    }

    public static void Register(GameObject instance)
    {
        if (instance == null)
            return;

        PruneDestroyed();
        s_spawnOrder.Add(instance);
        ApplyBudget();
    }

    public static void ResetForNewSession()
    {
        s_spawnOrder.Clear();
    }

    private static void PruneDestroyed()
    {
        for (int i = s_spawnOrder.Count - 1; i >= 0; i--)
        {
            if (s_spawnOrder[i] == null)
                s_spawnOrder.RemoveAt(i);
        }
    }

    private static void ApplyBudget()
    {
        PruneDestroyed();
        int maxAnimated = Mathf.Max(1, s_maxAnimated);
        int animatedStart = Mathf.Max(0, s_spawnOrder.Count - maxAnimated);

        for (int i = 0; i < s_spawnOrder.Count; i++)
        {
            var go = s_spawnOrder[i];
            if (go == null)
                continue;

            bool shouldAnimate = i >= animatedStart;
            SetAnimated(go, shouldAnimate);
        }
    }

    private static void SetAnimated(GameObject instance, bool animated)
    {
        var animator = instance.GetComponentInChildren<Animator>();
        if (animator != null)
            animator.enabled = animated;

        var idle = instance.GetComponent<HabitatAnimalIdleAnimator>();
        if (idle != null)
            idle.enabled = animated;

        var flight = instance.GetComponent<HabitatBeeFigureEightFlight>();
        if (flight != null)
            flight.enabled = animated;
    }
}
