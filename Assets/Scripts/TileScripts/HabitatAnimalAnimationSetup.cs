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
    }

    private static void AttachBeeFlight(GameObject instance, Vector3 motionAnchorWorld, Quaternion gridRotation)
    {
        var flight = instance.GetComponent<HabitatBeeFigureEightFlight>();
        if (flight == null)
            flight = instance.AddComponent<HabitatBeeFigureEightFlight>();

        flight.Initialize(motionAnchorWorld, gridRotation);
    }

    private static void AttachIdleAnimator(GameObject instance)
    {
        if (instance.GetComponent<HabitatBeeFigureEightFlight>() != null)
            return;

        if (instance.GetComponent<HabitatAnimalIdleAnimator>() == null)
            instance.AddComponent<HabitatAnimalIdleAnimator>();
    }
}
