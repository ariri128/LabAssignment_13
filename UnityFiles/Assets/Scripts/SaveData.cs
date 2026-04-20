using UnityEngine;
using System;
using System.Collections.Generic;

[Serializable]
public class SaveData
{
    public float revenue;
    public float reputation;
    public int juniorDeveloperCount;
    public int releasedGameCount;
    public List<string> purchasedUpgrades = new List<string>();
}
