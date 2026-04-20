using UnityEngine;

public struct UpgradeEffect
{
    public float multiplier;
    public ResourceType targetResourceType;

    public UpgradeEffect(float multiplier, ResourceType targetResourceType)
    {
        this.multiplier = multiplier;
        this.targetResourceType = targetResourceType;
    }
}
