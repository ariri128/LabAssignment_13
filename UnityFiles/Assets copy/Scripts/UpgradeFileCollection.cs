using UnityEngine;
using System;
using System.Collections.Generic;
using System.Xml.Serialization;

[Serializable]
[XmlRoot("UpgradeCollection")]
public class UpgradeFileCollection
{
    [XmlElement("Upgrade")]
    public List<UpgradeFileData> upgrades = new List<UpgradeFileData>();
}
