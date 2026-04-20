using UnityEngine;

public class JuniorDeveloperGenerator : Generator
{
    public JuniorDeveloperGenerator() : base("Junior Developer", 50f, 2f)
    {
    }

    public override float Produce()
    {
        return ownedCount * productionPerUnit;
    }
}
