using UnityEngine;

public class Upgrade
{
    public string upgradeName;
    public float cost;
    public UpgradeEffect effect;
    public int tier;
    public UpgradeState state;

    public Upgrade(string name, float cost, UpgradeEffect effect, int tier, UpgradeState state)
    {
        this.upgradeName = name;
        this.cost = cost;
        this.effect = effect;
        this.tier = tier;
        this.state = state;
    }
}
