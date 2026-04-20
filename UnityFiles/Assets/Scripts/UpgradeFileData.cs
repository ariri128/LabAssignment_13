using UnityEngine;
using System;

[Serializable]
public class UpgradeFileData
{
    public string upgradeName;
    public float cost;
    public float multiplier;
    public ResourceType targetResourceType;
    public int tier;
    public UpgradeState state;

    public UpgradeFileData()
    {
    }

    public UpgradeFileData(string upgradeName, float cost, float multiplier, ResourceType targetResourceType, int tier, UpgradeState state)
    {
        this.upgradeName = upgradeName;
        this.cost = cost;
        this.multiplier = multiplier;
        this.targetResourceType = targetResourceType;
        this.tier = tier;
        this.state = state;
    }
}
