using UnityEngine;

public abstract class Generator
{
    public string generatorName;
    public float baseCost;
    public int ownedCount;
    public float productionPerUnit;

    public Generator(string name, float cost, float productionPerUnit)
    {
        this.generatorName = name;
        this.baseCost = cost;
        this.productionPerUnit = productionPerUnit;
        this.ownedCount = 0;
    }

    public float GetCurrentCost()
    {
        return baseCost * (float)System.Math.Pow(1.15, ownedCount);
    }

    public abstract float Produce();
}
