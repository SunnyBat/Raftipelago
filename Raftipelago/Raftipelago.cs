using HarmonyLib;
using Raftipelago;
using Raftipelago.Data;
using Raftipelago.Network;
using Raftipelago.Patches;
using Raftipelago.UnityScripts;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEngine;

public class RaftipelagoThree : Mod
{
    private const string EmbeddedFileDirectory = "Data";
    private const string AppDataFolderName = "Raftipelago";

    private Harmony patcher;
    private IEnumerator serverHeartbeat;
    public void Start()
    {
        string appDataDirectory = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var proxyServerDirectory = Path.Combine(appDataDirectory, AppDataFolderName);
        ComponentManager<EmbeddedFileUtils>.Value = ComponentManager<EmbeddedFileUtils>.Value ?? new EmbeddedFileUtils(GetEmbeddedFileBytes);
        // We only need to load assemblies once; Raft needs to be restarted to load new versions, so we just keep the instance around forever
        ComponentManager<AssemblyManager>.Value = ComponentManager<AssemblyManager>.Value ?? new AssemblyManager(EmbeddedFileDirectory, proxyServerDirectory);
        ComponentManager<ExternalData>.Value = ComponentManager<ExternalData>.Value ?? new ExternalData(ComponentManager<EmbeddedFileUtils>.Value);
        ComponentManager<SpriteManager>.Value = ComponentManager<SpriteManager>.Value ?? new SpriteManager();
        ComponentManager<IArchipelagoLink>.Value = ComponentManager<IArchipelagoLink>.Value ?? new ProxiedArchipelago();
        ComponentManager<IArchipelagoLink>.Value.SetAlreadyReceivedItemIds(CommonUtils.GetUnlockedItemPacks(SaveAndLoad.WorldToLoad) ?? new List<int>());
        patcher = new Harmony("com.github.sunnybat.raftipelago");
        patcher.PatchAll(Assembly.GetExecutingAssembly());
        serverHeartbeat = ArchipelagoLinkHeartbeat.CreateNewHeartbeat(ComponentManager<IArchipelagoLink>.Value, 0.1f); // Trigger every 100ms
        if (isInWorld())
        {
            WorldLoaded_ArchipelagoSetup();
        }
        StartCoroutine(serverHeartbeat);
        Debug.Log("Raftipelago has been loaded!");
    }

    public void OnModUnload()
    {
        StopCoroutine(serverHeartbeat);
        ComponentManager<IArchipelagoLink>.Value?.Disconnect();
        if (isInWorld())
        {
            CommonUtils.SetUnlockedItemPacks(SaveAndLoad.WorldToLoad, ComponentManager<IArchipelagoLink>.Value.GetAllReceivedItemIds());
        }
        ComponentManager<IArchipelagoLink>.Value = null;
        ComponentManager<ExternalData>.Value = null; // Allows configurations to reload on new load
        patcher?.UnpatchAll("com.github.sunnybat.raftipelago");
        Debug.Log("Raftipelago has been unloaded.");
    }

    // This should ONLY be used for Archipelago-related setup; this is called even after
    // the world has been loaded for a while.
    public override void WorldEvent_WorldLoaded()
    {
        WorldLoaded_ArchipelagoSetup();
    }

    public override void WorldEvent_WorldUnloaded()
    {
        ComponentManager<IArchipelagoLink>.Value.SetIsInWorld(false);
        // Reset completion status since it will be updated upon world load
        ComponentManager<IArchipelagoLink>.Value.SetGameCompleted(false);
        // Reset station count when world unloaded so we don't trigger on reload into a different world
        HarmonyPatch_BalboaRelayStationScreen_RefreshScreen.previousStationCount = -1;
    }

    private static void WorldLoaded_ArchipelagoSetup()
    {
        ComponentManager<IArchipelagoLink>.Value.SetIsInWorld(true);
    }

    // TODO Add to in-game chat as well (keep this implementation to be able to choose either)
    [ConsoleCommand("/connect", "Connect to the Archipelago server. It's recommended to use a full address, eg \"/connect http://archipelago.gg:38281 UsernameGoesHere OptionalPassword\".")]
    private static void Command_Connect(string[] arguments)
    {
        if (ComponentManager<IArchipelagoLink>.Value.IsSuccessfullyConnected() == true)
        {
            Debug.LogError("Already connected to Archipelago. Disconnect with /disconnect before attempting to connect to a different server.");
        }
        else if (arguments.Length >= 2)
        {
            ComponentManager<IArchipelagoLink>.Value.Disconnect();
            ComponentManager<IArchipelagoLink>.Value.Connect(arguments[0], arguments[1], arguments.Length > 2 ? arguments[2] : null);
            ComponentManager<IArchipelagoLink>.Value.SetIsInWorld(isInWorld());
        }
        else
        {
            Debug.LogError("Usage: <i>/connect (URL) (Username) (Password)</i> -- Password is optional. Parenthesis should be omitted unless part of URL or username. If a value has spaces, use \"\" around it, eg \"My Unique Username\".");
        }
    }

    // TODO Add to in-game chat as well (keep this implementation to be able to choose either)
    [ConsoleCommand("/disconnect", "Disconnects from the Archipelago server. You must put \"confirmDisconnect\" in order to confirm that you want to disconnect from the current session.")]
    private static void Command_Disconnect(string[] arguments)
    {
        if (arguments.Length == 1 && arguments[0].Equals("confirmDisconnect", System.StringComparison.InvariantCultureIgnoreCase))
        {
            ComponentManager<IArchipelagoLink>.Value.Disconnect();
        }
        else
        {
            Debug.LogError("Usage: <i>disconnect confirmDisconnect</i>");
        }
    }

    [ConsoleCommand("/generateItems", "Generates the JSON for Archipelago's Raft item list. Must be loaded into a world to use.")]
    private static void Command_GenerateItems(string[] arguments)
    {
        if (isInWorld())
        {
            Debug.Log(DataGenerator.GenerateRawArchipelagoItemList());
        }
        else
        {
            Debug.LogError("Must be loaded into a world to generate item list.");
        }
    }

    [ConsoleCommand("/generateHiddenItems", "Generates the JSON for Archipelago's Raft item list, but it only gives you items that are not supposed to be included in the Archipelago item list. Must be loaded into a world to use.")]
    private static void Command_GenerateHiddenItems(string[] arguments)
    {
        if (isInWorld())
        {
            Debug.Log(DataGenerator.GenerateRawArchipelagoItemList(true));
        }
        else
        {
            Debug.LogError("Must be loaded into a world to generate item list.");
        }
    }

    [ConsoleCommand("/generateLocations", "Generates the JSON for Archipelago's Raft location list. Must be loaded into a world to use.")]
    private static void Command_GenerateLocations(string[] arguments)
    {
        if (isInWorld())
        {
            Debug.Log(DataGenerator.GenerateLocationList());
        }
        else
        {
            Debug.LogError("Must be loaded into a world to generate location list.");
        }
    }

    [ConsoleCommand("/generateHiddenLocations", "Generates the JSON for Archipelago's Raft location list, but it only gives you inaccessible researches in the Research Table. Must be loaded into a world to use.")]
    private static void Command_GenerateHiddenLocations(string[] arguments)
    {
        if (isInWorld())
        {
            Debug.Log(DataGenerator.GenerateLocationList(true));
        }
        else
        {
            Debug.LogError("Must be loaded into a world to generate location list.");
        }
    }

    [ConsoleCommand("/toggleDebug", "Toggles Raftipelago debug prints.")]
    private static void Command_ToggleDebug(string[] arguments)
    {
        ComponentManager<IArchipelagoLink>.Value.ToggleDebug();
    }

    private static bool isInWorld()
    {
        return ComponentManager<CraftingMenu>.Value != null; // TODO Better way to determine?
    }
}