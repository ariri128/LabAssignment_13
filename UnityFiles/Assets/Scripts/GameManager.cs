using UnityEngine;
using System.Collections.Generic;
using TMPro;
using System;
using System.IO;
using System.Xml.Serialization;

public class GameManager : MonoBehaviour
{
    public ResourceManager resourceManager;

    public TextMeshProUGUI revenueText;
    public TextMeshProUGUI reputationText;
    public TextMeshProUGUI revenuePerSecondText;
    public TextMeshProUGUI juniorDevCountText;
    public TextMeshProUGUI releasedGamesCountText;
    public TextMeshProUGUI statusMessageText;

    public TextMeshProUGUI engineUpgradeStateText;
    public TextMeshProUGUI assetLibraryUpgradeStateText;
    public TextMeshProUGUI buildOptimizationUpgradeStateText;

    public float developGameClickValue = 10f;

    public List<Upgrade> upgrades = new List<Upgrade>();

    private Generator juniorDeveloperGenerator;
    private Generator releasedGameGenerator;

    private float globalRevenueMultiplier = 1f;

    private string dataPath;
    private string upgradeFilePath;
    private string saveFilePath;
    private string playtimeFilePath;

    private float sessionStartTime;

    private void Start()
    {
        if (resourceManager == null)
        {
            resourceManager = GetComponent<ResourceManager>();
        }

        resourceManager.OnResourceChanged += HandleResourceChanged;

        InitializeFilePaths();
        CreatePlaytimeFileIfNeeded();

        juniorDeveloperGenerator = new JuniorDeveloperGenerator();
        releasedGameGenerator = new ReleasedGameGenerator();

        try
        {
            LoadUpgradeData();
            LoadGameState();
            statusMessageText.text = "Status: Save data loaded.";
        }
        catch (Exception exception)
        {
            statusMessageText.text = "Status: Load failed. Using default data.";
            CreateDefaultUpgradeFile();
            LoadUpgradeData();
            Debug.Log(exception.Message);
        }
        finally
        {
            UpdateUpgradeAvailability();
            UpdateRevenuePerSecond();
            UpdateUI();
        }

        sessionStartTime = Time.time;
    }

    private void Update()
    {
        RunPassiveIncome();
        UpdateGeneratorUI();
        UpdateUpgradeUI();
    }

    private void OnDestroy()
    {
        if (resourceManager != null)
        {
            resourceManager.OnResourceChanged -= HandleResourceChanged;
        }
    }

    private void HandleResourceChanged(ResourceType resourceType, float newValue)
    {
        UpdateResourceUI();
    }

    private void InitializeFilePaths()
    {
        dataPath = Application.persistentDataPath + "/GameStudioSimulatorData/";

        if (!Directory.Exists(dataPath))
        {
            Directory.CreateDirectory(dataPath);
        }

        upgradeFilePath = dataPath + "UpgradeData.xml";
        saveFilePath = dataPath + "SaveData.json";
        playtimeFilePath = dataPath + "SessionPlaytimes.txt";
    }

    private void CreatePlaytimeFileIfNeeded()
    {
        if (!File.Exists(playtimeFilePath))
        {
            File.WriteAllText(playtimeFilePath, "Game Studio Simulator Session Playtimes\n");
        }
    }

    private void LoadUpgradeData()
    {
        if (!File.Exists(upgradeFilePath))
        {
            CreateDefaultUpgradeFile();
        }

        XmlSerializer serializer = new XmlSerializer(typeof(UpgradeFileCollection));

        using (FileStream stream = File.OpenRead(upgradeFilePath))
        {
            UpgradeFileCollection loadedData = (UpgradeFileCollection)serializer.Deserialize(stream);
            upgrades.Clear();

            for (int i = 0; i < loadedData.upgrades.Count; i++)
            {
                UpgradeFileData data = loadedData.upgrades[i];

                upgrades.Add(new Upgrade(
                    data.upgradeName,
                    data.cost,
                    new UpgradeEffect(data.multiplier, data.targetResourceType),
                    data.tier,
                    data.state
                ));
            }
        }
    }

    private void CreateDefaultUpgradeFile()
    {
        UpgradeFileCollection defaultData = new UpgradeFileCollection();

        defaultData.upgrades.Add(new UpgradeFileData(
            "Better Engine",
            75f,
            1.25f,
            ResourceType.RevenuePerSecond,
            1,
            UpgradeState.Available
        ));

        defaultData.upgrades.Add(new UpgradeFileData(
            "Asset Library",
            150f,
            1.5f,
            ResourceType.RevenuePerSecond,
            2,
            UpgradeState.Locked
        ));

        defaultData.upgrades.Add(new UpgradeFileData(
            "Build Optimization",
            250f,
            2f,
            ResourceType.RevenuePerSecond,
            3,
            UpgradeState.Locked
        ));

        XmlSerializer serializer = new XmlSerializer(typeof(UpgradeFileCollection));

        using (FileStream stream = File.Create(upgradeFilePath))
        {
            serializer.Serialize(stream, defaultData);
        }
    }

    private void LoadGameState()
    {
        if (!File.Exists(saveFilePath))
        {
            return;
        }

        using (StreamReader stream = new StreamReader(saveFilePath))
        {
            string jsonString = stream.ReadToEnd();
            SaveData saveData = JsonUtility.FromJson<SaveData>(jsonString);

            if (saveData == null)
            {
                return;
            }

            resourceManager.SetResource(ResourceType.Revenue, saveData.revenue);
            resourceManager.SetResource(ResourceType.Reputation, saveData.reputation);

            juniorDeveloperGenerator.ownedCount = saveData.juniorDeveloperCount;
            releasedGameGenerator.ownedCount = saveData.releasedGameCount;

            for (int i = 0; i < upgrades.Count; i++)
            {
                upgrades[i].state = i == 0 ? UpgradeState.Available : UpgradeState.Locked;
            }

            for (int i = 0; i < saveData.purchasedUpgrades.Count; i++)
            {
                Upgrade loadedUpgrade = GetUpgradeByName(saveData.purchasedUpgrades[i]);

                if (loadedUpgrade != null)
                {
                    loadedUpgrade.state = UpgradeState.Purchased;
                }
            }

            RebuildGlobalRevenueMultiplier();
        }
    }

    private void SaveGameState()
    {
        SaveData saveData = new SaveData();

        saveData.revenue = resourceManager.GetResource(ResourceType.Revenue);
        saveData.reputation = resourceManager.GetResource(ResourceType.Reputation);
        saveData.juniorDeveloperCount = juniorDeveloperGenerator.ownedCount;
        saveData.releasedGameCount = releasedGameGenerator.ownedCount;

        for (int i = 0; i < upgrades.Count; i++)
        {
            if (upgrades[i].state == UpgradeState.Purchased)
            {
                saveData.purchasedUpgrades.Add(upgrades[i].upgradeName);
            }
        }

        string jsonString = JsonUtility.ToJson(saveData, true);

        using (StreamWriter stream = File.CreateText(saveFilePath))
        {
            stream.WriteLine(jsonString);
        }
    }

    private void RebuildGlobalRevenueMultiplier()
    {
        globalRevenueMultiplier = 1f;

        for (int i = 0; i < upgrades.Count; i++)
        {
            if (upgrades[i].state == UpgradeState.Purchased)
            {
                ApplyUpgradeEffect(upgrades[i].effect);
            }
        }
    }

    private Upgrade GetUpgradeByName(string upgradeName)
    {
        for (int i = 0; i < upgrades.Count; i++)
        {
            if (upgrades[i].upgradeName == upgradeName)
            {
                return upgrades[i];
            }
        }

        return null;
    }

    private void RunPassiveIncome()
    {
        float totalPassiveIncome = 0f;

        totalPassiveIncome += juniorDeveloperGenerator.Produce();
        totalPassiveIncome += releasedGameGenerator.Produce();
        totalPassiveIncome *= globalRevenueMultiplier;

        resourceManager.AddResource(ResourceType.Revenue, totalPassiveIncome * Time.deltaTime);
    }

    private void UpdateRevenuePerSecond()
    {
        float totalPassiveIncome = 0f;

        totalPassiveIncome += juniorDeveloperGenerator.Produce();
        totalPassiveIncome += releasedGameGenerator.Produce();
        totalPassiveIncome *= globalRevenueMultiplier;

        resourceManager.SetResource(ResourceType.RevenuePerSecond, totalPassiveIncome);
    }

    private void UpdateUpgradeAvailability()
    {
        for (int i = 0; i < upgrades.Count; i++)
        {
            if (upgrades[i].state == UpgradeState.Purchased)
            {
                continue;
            }

            if (i == 0)
            {
                upgrades[i].state = UpgradeState.Available;
            }
            else if (upgrades[i - 1].state == UpgradeState.Purchased)
            {
                upgrades[i].state = UpgradeState.Available;
            }
            else
            {
                upgrades[i].state = UpgradeState.Locked;
            }
        }
    }

    private void UpdateUI()
    {
        UpdateResourceUI();
        UpdateGeneratorUI();
        UpdateUpgradeUI();
    }

    private void UpdateResourceUI()
    {
        revenueText.text = "Revenue: $" + resourceManager.GetResource(ResourceType.Revenue).ToString("F1");
        reputationText.text = "Reputation: " + resourceManager.GetResource(ResourceType.Reputation).ToString("F1");
        revenuePerSecondText.text = "Revenue / Sec: $" + resourceManager.GetResource(ResourceType.RevenuePerSecond).ToString("F1");
    }

    private void UpdateGeneratorUI()
    {
        juniorDevCountText.text = "Dev Count: " + juniorDeveloperGenerator.ownedCount;
        releasedGamesCountText.text = "Game Count: " + releasedGameGenerator.ownedCount;
    }

    private void UpdateUpgradeUI()
    {
        engineUpgradeStateText.text = upgrades[0].state.ToString();
        assetLibraryUpgradeStateText.text = upgrades[1].state.ToString();
        buildOptimizationUpgradeStateText.text = upgrades[2].state.ToString();
    }

    public void DevelopGame()
    {
        float clickAmount = developGameClickValue * globalRevenueMultiplier;
        resourceManager.AddResource(ResourceType.Revenue, clickAmount);
        resourceManager.AddResource(ResourceType.Reputation, 1f);
        statusMessageText.text = "Status: Developed a game project.";
        SaveGameState();
        UpdateGeneratorUI();
        UpdateUpgradeUI();
    }

    public void BuyJuniorDeveloper()
    {
        string message;
        bool success = TryPurchaseGenerator(juniorDeveloperGenerator, out message);

        statusMessageText.text = "Status: " + message;

        if (success)
        {
            UpdateRevenuePerSecond();
            SaveGameState();
            UpdateGeneratorUI();
            UpdateUpgradeUI();
        }
    }

    public void BuyReleasedGame()
    {
        string message;
        bool success = TryPurchaseGenerator(releasedGameGenerator, out message);

        statusMessageText.text = "Status: " + message;

        if (success)
        {
            resourceManager.AddResource(ResourceType.Reputation, 5f);
            UpdateRevenuePerSecond();
            SaveGameState();
            UpdateGeneratorUI();
            UpdateUpgradeUI();
        }
    }

    private bool TryPurchaseGenerator(Generator generator, out string message)
    {
        float cost = generator.GetCurrentCost();

        if (resourceManager.SpendResource(ResourceType.Revenue, cost))
        {
            generator.ownedCount++;
            message = "Purchased " + generator.generatorName;
            return true;
        }

        message = "Not enough Revenue for " + generator.generatorName;
        return false;
    }

    public void BuyBetterEngine()
    {
        BuyUpgrade("Better Engine");
    }

    public void BuyAssetLibrary()
    {
        BuyUpgrade("Asset Library");
    }

    public void BuyBuildOptimization()
    {
        BuyUpgrade("Build Optimization");
    }

    private void BuyUpgrade(string upgradeName)
    {
        for (int i = 0; i < upgrades.Count; i++)
        {
            if (upgrades[i].upgradeName == upgradeName)
            {
                if (upgrades[i].state != UpgradeState.Available)
                {
                    statusMessageText.text = "Status: Upgrade is not available.";
                    return;
                }

                if (resourceManager.SpendResource(ResourceType.Revenue, upgrades[i].cost))
                {
                    upgrades[i].state = UpgradeState.Purchased;
                    ApplyUpgradeEffect(upgrades[i].effect);
                    UpdateRevenuePerSecond();
                    UpdateUpgradeAvailability();
                    SaveGameState();
                    statusMessageText.text = "Status: Purchased " + upgrades[i].upgradeName;
                    UpdateGeneratorUI();
                    UpdateUpgradeUI();
                }
                else
                {
                    statusMessageText.text = "Status: Not enough Revenue for " + upgrades[i].upgradeName;
                }

                return;
            }
        }
    }

    private void ApplyUpgradeEffect(UpgradeEffect effect)
    {
        if (effect.targetResourceType == ResourceType.RevenuePerSecond)
        {
            globalRevenueMultiplier *= effect.multiplier;
        }
    }

    private void OnApplicationQuit()
    {
        SaveGameState();

        float sessionPlaytime = Time.time - sessionStartTime;
        string line = "Session Playtime: " + sessionPlaytime.ToString("F2") + " seconds - " + DateTime.Now;

        File.AppendAllText(playtimeFilePath, line + Environment.NewLine);
    }

    /*
    public ResourceManager resourceManager;

    public TextMeshProUGUI revenueText;
    public TextMeshProUGUI reputationText;
    public TextMeshProUGUI revenuePerSecondText;
    public TextMeshProUGUI juniorDevCountText;
    public TextMeshProUGUI releasedGamesCountText;
    public TextMeshProUGUI statusMessageText;

    public TextMeshProUGUI engineUpgradeStateText;
    public TextMeshProUGUI assetLibraryUpgradeStateText;
    public TextMeshProUGUI buildOptimizationUpgradeStateText;

    public float developGameClickValue = 10f;

    public List<Upgrade> upgrades = new List<Upgrade>();

    private Generator juniorDeveloperGenerator;
    private Generator releasedGameGenerator;

    private float globalRevenueMultiplier = 1f;

    private string dataPath;
    private string upgradeFilePath;
    private string saveFilePath;
    private string playtimeFilePath;

    private float sessionStartTime;

    private void Start()
    {
        if (resourceManager == null)
        {
            resourceManager = GetComponent<ResourceManager>();
        }

        Debug.Log(Application.persistentDataPath);

        InitializeFilePaths();
        CreatePlaytimeFileIfNeeded();

        juniorDeveloperGenerator = new JuniorDeveloperGenerator();
        releasedGameGenerator = new ReleasedGameGenerator();

        LoadUpgradeData();
        LoadGameState();
        UpdateUpgradeAvailability();
        UpdateRevenuePerSecond();
        UpdateUI();

        sessionStartTime = Time.time;
    }

    private void Update()
    {
        RunPassiveIncome();
        UpdateUI();
    }

    private void InitializeFilePaths()
    {
        dataPath = Application.persistentDataPath + "/GameStudioSimulatorData/";

        if (!Directory.Exists(dataPath))
        {
            Directory.CreateDirectory(dataPath);
        }

        upgradeFilePath = dataPath + "UpgradeData.xml";
        saveFilePath = dataPath + "SaveData.json";
        playtimeFilePath = dataPath + "SessionPlaytimes.txt";
    }

    private void CreatePlaytimeFileIfNeeded()
    {
        if (!File.Exists(playtimeFilePath))
        {
            File.WriteAllText(playtimeFilePath, "Game Studio Simulator Session Playtimes\n");
        }
    }

    private void LoadUpgradeData()
    {
        if (!File.Exists(upgradeFilePath))
        {
            CreateDefaultUpgradeFile();
        }

        XmlSerializer serializer = new XmlSerializer(typeof(UpgradeFileCollection));

        using (FileStream stream = File.OpenRead(upgradeFilePath))
        {
            UpgradeFileCollection loadedData = (UpgradeFileCollection)serializer.Deserialize(stream);
            upgrades.Clear();

            for (int i = 0; i < loadedData.upgrades.Count; i++)
            {
                UpgradeFileData data = loadedData.upgrades[i];

                upgrades.Add(new Upgrade(
                    data.upgradeName,
                    data.cost,
                    new UpgradeEffect(data.multiplier, data.targetResourceType),
                    data.tier,
                    data.state
                ));
            }
        }
    }

    private void CreateDefaultUpgradeFile()
    {
        UpgradeFileCollection defaultData = new UpgradeFileCollection();

        defaultData.upgrades.Add(new UpgradeFileData(
            "Better Engine",
            75f,
            1.25f,
            ResourceType.RevenuePerSecond,
            1,
            UpgradeState.Available
        ));

        defaultData.upgrades.Add(new UpgradeFileData(
            "Asset Library",
            150f,
            1.5f,
            ResourceType.RevenuePerSecond,
            2,
            UpgradeState.Locked
        ));

        defaultData.upgrades.Add(new UpgradeFileData(
            "Build Optimization",
            250f,
            2f,
            ResourceType.RevenuePerSecond,
            3,
            UpgradeState.Locked
        ));

        XmlSerializer serializer = new XmlSerializer(typeof(UpgradeFileCollection));

        using (FileStream stream = File.Create(upgradeFilePath))
        {
            serializer.Serialize(stream, defaultData);
        }
    }

    private void LoadGameState()
    {
        if (!File.Exists(saveFilePath))
        {
            return;
        }

        using (StreamReader stream = new StreamReader(saveFilePath))
        {
            string jsonString = stream.ReadToEnd();
            SaveData saveData = JsonUtility.FromJson<SaveData>(jsonString);

            if (saveData == null)
            {
                return;
            }

            resourceManager.SetResource(ResourceType.Revenue, saveData.revenue);
            resourceManager.SetResource(ResourceType.Reputation, saveData.reputation);

            juniorDeveloperGenerator.ownedCount = saveData.juniorDeveloperCount;
            releasedGameGenerator.ownedCount = saveData.releasedGameCount;

            for (int i = 0; i < upgrades.Count; i++)
            {
                upgrades[i].state = i == 0 ? UpgradeState.Available : UpgradeState.Locked;
            }

            for (int i = 0; i < saveData.purchasedUpgrades.Count; i++)
            {
                Upgrade loadedUpgrade = GetUpgradeByName(saveData.purchasedUpgrades[i]);

                if (loadedUpgrade != null)
                {
                    loadedUpgrade.state = UpgradeState.Purchased;
                }
            }

            RebuildGlobalRevenueMultiplier();
        }
    }

    private void SaveGameState()
    {
        SaveData saveData = new SaveData();

        saveData.revenue = resourceManager.GetResource(ResourceType.Revenue);
        saveData.reputation = resourceManager.GetResource(ResourceType.Reputation);
        saveData.juniorDeveloperCount = juniorDeveloperGenerator.ownedCount;
        saveData.releasedGameCount = releasedGameGenerator.ownedCount;

        for (int i = 0; i < upgrades.Count; i++)
        {
            if (upgrades[i].state == UpgradeState.Purchased)
            {
                saveData.purchasedUpgrades.Add(upgrades[i].upgradeName);
            }
        }

        string jsonString = JsonUtility.ToJson(saveData, true);

        using (StreamWriter stream = File.CreateText(saveFilePath))
        {
            stream.WriteLine(jsonString);
        }
    }

    private void RebuildGlobalRevenueMultiplier()
    {
        globalRevenueMultiplier = 1f;

        for (int i = 0; i < upgrades.Count; i++)
        {
            if (upgrades[i].state == UpgradeState.Purchased)
            {
                ApplyUpgradeEffect(upgrades[i].effect);
            }
        }
    }

    private Upgrade GetUpgradeByName(string upgradeName)
    {
        for (int i = 0; i < upgrades.Count; i++)
        {
            if (upgrades[i].upgradeName == upgradeName)
            {
                return upgrades[i];
            }
        }

        return null;
    }

    private void RunPassiveIncome()
    {
        float totalPassiveIncome = 0f;

        totalPassiveIncome += juniorDeveloperGenerator.Produce();
        totalPassiveIncome += releasedGameGenerator.Produce();
        totalPassiveIncome *= globalRevenueMultiplier;

        resourceManager.AddResource(ResourceType.Revenue, totalPassiveIncome * Time.deltaTime);
    }

    private void UpdateRevenuePerSecond()
    {
        float totalPassiveIncome = 0f;

        totalPassiveIncome += juniorDeveloperGenerator.Produce();
        totalPassiveIncome += releasedGameGenerator.Produce();
        totalPassiveIncome *= globalRevenueMultiplier;

        resourceManager.SetResource(ResourceType.RevenuePerSecond, totalPassiveIncome);
    }

    private void UpdateUpgradeAvailability()
    {
        for (int i = 0; i < upgrades.Count; i++)
        {
            if (upgrades[i].state == UpgradeState.Purchased)
            {
                continue;
            }

            if (i == 0)
            {
                upgrades[i].state = UpgradeState.Available;
            }
            else if (upgrades[i - 1].state == UpgradeState.Purchased)
            {
                upgrades[i].state = UpgradeState.Available;
            }
            else
            {
                upgrades[i].state = UpgradeState.Locked;
            }
        }
    }

    private void UpdateUI()
    {
        revenueText.text = "Revenue: $" + resourceManager.GetResource(ResourceType.Revenue).ToString("F1");
        reputationText.text = "Reputation: " + resourceManager.GetResource(ResourceType.Reputation).ToString("F1");
        revenuePerSecondText.text = "Revenue / Sec: $" + resourceManager.GetResource(ResourceType.RevenuePerSecond).ToString("F1");

        juniorDevCountText.text = "Dev Count: " + juniorDeveloperGenerator.ownedCount;
        releasedGamesCountText.text = "Game Count: " + releasedGameGenerator.ownedCount;

        engineUpgradeStateText.text = upgrades[0].state.ToString();
        assetLibraryUpgradeStateText.text = upgrades[1].state.ToString();
        buildOptimizationUpgradeStateText.text = upgrades[2].state.ToString();
    }

    public void DevelopGame()
    {
        float clickAmount = developGameClickValue * globalRevenueMultiplier;
        resourceManager.AddResource(ResourceType.Revenue, clickAmount);
        resourceManager.AddResource(ResourceType.Reputation, 1f);
        statusMessageText.text = "Status: Developed a game project.";
        SaveGameState();
        UpdateUI();
    }

    public void BuyJuniorDeveloper()
    {
        string message;
        bool success = TryPurchaseGenerator(juniorDeveloperGenerator, out message);

        statusMessageText.text = "Status: " + message;

        if (success)
        {
            UpdateRevenuePerSecond();
            SaveGameState();
            UpdateUI();
        }
    }

    public void BuyReleasedGame()
    {
        string message;
        bool success = TryPurchaseGenerator(releasedGameGenerator, out message);

        statusMessageText.text = "Status: " + message;

        if (success)
        {
            resourceManager.AddResource(ResourceType.Reputation, 5f);
            UpdateRevenuePerSecond();
            SaveGameState();
            UpdateUI();
        }
    }

    private bool TryPurchaseGenerator(Generator generator, out string message)
    {
        float cost = generator.GetCurrentCost();

        if (resourceManager.SpendResource(ResourceType.Revenue, cost))
        {
            generator.ownedCount++;
            message = "Purchased " + generator.generatorName;
            return true;
        }

        message = "Not enough Revenue for " + generator.generatorName;
        return false;
    }

    public void BuyBetterEngine()
    {
        BuyUpgrade("Better Engine");
    }

    public void BuyAssetLibrary()
    {
        BuyUpgrade("Asset Library");
    }

    public void BuyBuildOptimization()
    {
        BuyUpgrade("Build Optimization");
    }

    private void BuyUpgrade(string upgradeName)
    {
        for (int i = 0; i < upgrades.Count; i++)
        {
            if (upgrades[i].upgradeName == upgradeName)
            {
                if (upgrades[i].state != UpgradeState.Available)
                {
                    statusMessageText.text = "Status: Upgrade is not available.";
                    return;
                }

                if (resourceManager.SpendResource(ResourceType.Revenue, upgrades[i].cost))
                {
                    upgrades[i].state = UpgradeState.Purchased;
                    ApplyUpgradeEffect(upgrades[i].effect);
                    UpdateRevenuePerSecond();
                    UpdateUpgradeAvailability();
                    SaveGameState();
                    statusMessageText.text = "Status: Purchased " + upgrades[i].upgradeName;
                    UpdateUI();
                }
                else
                {
                    statusMessageText.text = "Status: Not enough Revenue for " + upgrades[i].upgradeName;
                }

                return;
            }
        }
    }

    private void ApplyUpgradeEffect(UpgradeEffect effect)
    {
        if (effect.targetResourceType == ResourceType.RevenuePerSecond)
        {
            globalRevenueMultiplier *= effect.multiplier;
        }
    }

    private void OnApplicationQuit()
    {
        SaveGameState();

        float sessionPlaytime = Time.time - sessionStartTime;
        string line = "Session Playtime: " + sessionPlaytime.ToString("F2") + " seconds - " + DateTime.Now;

        File.AppendAllText(playtimeFilePath, line + Environment.NewLine);
    }
    */
}
