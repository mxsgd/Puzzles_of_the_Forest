using UnityEngine;

/// <summary>
/// Podpina właściwe zachowanie animacji po spawnie zwierzęcia w habitacie.
/// </summary>
public static class HabitatAnimalAnimationSetup
{
    public static void Attach(
        GameObject instance,
        HabitatAnimal animal,
        Vector3 motionAnchorWorld,
        Quaternion gridRotation)
    {
        if (instance == null)
            return;

        switch (animal)
        {
            case HabitatAnimal.Bees:
                AttachBeeFlight(instance, motionAnchorWorld, gridRotation);
                break;
            default:
                AttachIdleAnimator(instance);
                break;
        }

        HabitatAnimalAnimatorBudget.Register(instance);
    }

    private static void AttachBeeFlight(GameObject instance, Vector3 motionAnchorWorld, Quaternion gridRotation)
    {
        // Warm up (enable) the Animator BEFORE adding the flight component: AddComponent fires
        // OnEnable synchronously, which immediately calls animator.Play(...) — on a still-disabled
        // Animator that call is a no-op, and enabling it afterward doesn't retroactively apply it,
        // so the wing-flap state never actually started playing.
        WarmupAnimator(instance);

        var flight = instance.GetComponent<HabitatBeeFigureEightFlight>();
        if (flight == null)
            flight = instance.AddComponent<HabitatBeeFigureEightFlight>();

        flight.Initialize(motionAnchorWorld, gridRotation);
    }

    private static void AttachIdleAnimator(GameObject instance)
    {
        if (instance.GetComponent<HabitatBeeFigureEightFlight>() != null)
            return;

        WarmupAnimator(instance);

        if (instance.GetComponent<HabitatAnimalIdleAnimator>() == null)
            instance.AddComponent<HabitatAnimalIdleAnimator>();
    }

    private static void WarmupAnimator(GameObject instance)
    {
        var animator = instance.GetComponentInChildren<Animator>();
        if (animator == null)
            return;

        animator.enabled = true;
        animator.cullingMode = AnimatorCullingMode.CullUpdateTransforms;
        animator.updateMode = AnimatorUpdateMode.Normal;
    }
}
