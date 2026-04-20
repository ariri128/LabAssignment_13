using UnityEngine;

public class ReleasedGameGenerator : Generator
{
    public ReleasedGameGenerator() : base("Released Game", 100f, 5f)
    {
    }

    public override float Produce()
    {
        return ownedCount * productionPerUnit;
    }
}
