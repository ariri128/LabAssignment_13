using UnityEngine;
using System.Collections.Generic;

public class ResourceManager : MonoBehaviour
{
    public delegate void ResourceChangedHandler(ResourceType resourceType, float newValue);
    public event ResourceChangedHandler OnResourceChanged;

    private Dictionary<ResourceType, float> resources = new Dictionary<ResourceType, float>();

    private void Awake()
    {
        resources[ResourceType.Revenue] = 0f;
        resources[ResourceType.Reputation] = 0f;
        resources[ResourceType.RevenuePerSecond] = 0f;
    }

    public float GetResource(ResourceType resourceType)
    {
        if (resources.ContainsKey(resourceType))
        {
            return resources[resourceType];
        }

        return 0f;
    }

    public void AddResource(ResourceType resourceType, float amount)
    {
        if (!resources.ContainsKey(resourceType))
        {
            resources[resourceType] = 0f;
        }

        float currentValue = resources[resourceType];

        ModifyResource(ref currentValue, amount);

        resources[resourceType] = currentValue;
        OnResourceChanged?.Invoke(resourceType, currentValue);
    }

    public bool SpendResource(ResourceType resourceType, float amount)
    {
        if (resources.ContainsKey(resourceType) && resources[resourceType] >= amount)
        {
            resources[resourceType] -= amount;
            OnResourceChanged?.Invoke(resourceType, resources[resourceType]);
            return true;
        }

        return false;
    }

    public void SetResource(ResourceType resourceType, float value)
    {
        resources[resourceType] = value;
        OnResourceChanged?.Invoke(resourceType, value);
    }

    private void ModifyResource(ref float resourceValue, float amount)
    {
        resourceValue += amount;
    }
}
