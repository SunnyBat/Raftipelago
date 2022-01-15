using HarmonyLib;
using Raftipelago;
using Raftipelago.Network.Behaviors;
using Raftipelago.Data;
using Raftipelago.Network;
using Raftipelago.Patches;
using Raftipelago.UnityScripts;
using Steamworks;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using UnityEngine;

public class RaftipelagoThree : Mod
{
    private const string EmbeddedFileDirectory = "Data";
    public const string AppDataFolderName = "Raftipelago";

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
        ComponentManager<ItemTracker>.Value = ComponentManager<ItemTracker>.Value ?? new ItemTracker();
        ComponentManager<ArchipelagoDataManager>.Value = ComponentManager<ArchipelagoDataManager>.Value ?? new ArchipelagoDataManager();
        ComponentManager<ItemTracker>.Value.SetAlreadyReceivedItemData(CommonUtils.GetUnlockedItemIdentifiers(SaveAndLoad.WorldToLoad) ?? new List<long>());
        ComponentManager<ItemTracker>.Value.ResetProgressives();
        patcher = new Harmony("com.github.sunnybat.raftipelago");
        patcher.PatchAll(Assembly.GetExecutingAssembly());
        ComponentManager<IArchipelagoLink>.Value = ComponentManager<IArchipelagoLink>.Value ?? new ProxiedArchipelago();
        serverHeartbeat = ArchipelagoLinkHeartbeat.CreateNewHeartbeat(ComponentManager<IArchipelagoLink>.Value, 0.1f); // Trigger every 100ms
        if (isInWorld())
        {
            WorldLoaded_ArchipelagoSetup();
        }
        StartCoroutine(serverHeartbeat);
    }

    public void OnModUnload()
    {
        StopCoroutine(serverHeartbeat);
        ComponentManager<IArchipelagoLink>.Value?.Disconnect();
        if (isInWorld())
        {
            if (SaveAndLoad.WorldToLoad == null)
            {
                // WorldToLoad is only used on world load, we can modify it without consequence
                SaveAndLoad.WorldToLoad = (RGD_Game)ComponentManager<AssemblyManager>.Value.GetAssembly(AssemblyManager.RaftipelagoTypesAssembly)
                    .GetType("RaftipelagoTypes.RGD_Game_Raftipelago").GetConstructor(new Type[] {}).Invoke(null);
            }
            CommonUtils.SetUnlockedItemIdentifiers(SaveAndLoad.WorldToLoad, ComponentManager<ItemTracker>.Value.GetAllReceivedItemIds());
            NetworkUpdateManager.RemoveBehaviour(ComponentManager<ArchipelagoDataSync>.Value);
            NetworkUpdateManager.RemoveBehaviour(ComponentManager<ItemSyncBehaviour>.Value);
            NetworkUpdateManager.RemoveBehaviour(ComponentManager<ResendDataBehaviour>.Value);
        }
        ComponentManager<IArchipelagoLink>.Value?.onUnload();
        ComponentManager<IArchipelagoLink>.Value = null;
        patcher?.UnpatchAll("com.github.sunnybat.raftipelago");
    }

    // This should ONLY be used for Archipelago-related setup; this is called even after
    // the world has been loaded for a while.
    public override void WorldEvent_WorldLoaded()
    {
        ComponentManager<ItemTracker>.Value.ResetProgressives();
        WorldLoaded_ArchipelagoSetup();
    }

    public override void WorldEvent_WorldUnloaded()
    {
        // *Sync objects are automatically cleared from NetworkUpdateManager
        // We can just leave the values hanging in ComponentManager since they'll be overwritten
        ComponentManager<IArchipelagoLink>.Value.SetIsInWorld(false);
        // Reset completion status since it will be updated upon world load
        ComponentManager<IArchipelagoLink>.Value.SetGameCompleted(false);
        // Reset station count when world unloaded so we don't trigger on reload into a different world
        HarmonyPatch_BalboaRelayStationScreen_RefreshScreen.previousStationCount = -1;
    }

    private static void WorldLoaded_ArchipelagoSetup()
    {
        // Always add these so they have the same IDs
        CommonUtils.Reset();
        var wrapperObject = new GameObject(); // Required to create proper Behaviour objects (they need a parent)
        ComponentManager<ArchipelagoDataSync>.Value = (ArchipelagoDataSync)wrapperObject.AddComponent(typeof(ArchipelagoDataSync));
        NetworkUpdateManager.AddBehaviour(ComponentManager<ArchipelagoDataSync>.Value);

        ComponentManager<ItemSyncBehaviour>.Value = (ItemSyncBehaviour)wrapperObject.AddComponent(typeof(ItemSyncBehaviour));
        NetworkUpdateManager.AddBehaviour(ComponentManager<ItemSyncBehaviour>.Value);

        ComponentManager<ResendDataBehaviour>.Value = (ResendDataBehaviour)wrapperObject.AddComponent(typeof(ResendDataBehaviour));
        NetworkUpdateManager.AddBehaviour(ComponentManager<ResendDataBehaviour>.Value);

        if (Semih_Network.IsHost)
        {
            ComponentManager<IArchipelagoLink>.Value.SetIsInWorld(true);
        }
        else
        {
            var resendPacket = ComponentManager<AssemblyManager>.Value.GetAssembly(AssemblyManager.RaftipelagoTypesAssembly).GetType("RaftipelagoTypes.RaftipelagoPacket_ResendData")
                .GetConstructor(new Type[] { typeof(Messages), typeof(MonoBehaviour_Network) }).Invoke(new object[] { Messages.NOTHING, ComponentManager<ResendDataBehaviour>.Value });
            ComponentManager<Semih_Network>.Value.RPC((Message)resendPacket, Target.Other, EP2PSend.k_EP2PSendReliable, NetworkChannel.Channel_Game);
        }
    }

    [ConsoleCommand("/connect", "Connect to the Archipelago server. It's recommended to use a full address, eg \"/connect http://archipelago.gg:38281 UsernameGoesHere OptionalPassword\".")]
    private static void Command_Connect(string[] arguments)
    {
        if (!Semih_Network.IsHost && !Semih_Network.InMenuScene)
        {
            Debug.LogError("Only the world host can connect to Archipelago.");
        }
        else if (ComponentManager<IArchipelagoLink>.Value.IsSuccessfullyConnected() == true)
        {
            Debug.LogError("Already connected to Archipelago. Disconnect with /disconnect before attempting to connect to a different server. Be careful when connecting to different servers with the same world; Archipelago location unlocks are permanent.");
        }
        else if (arguments.Length >= 2)
        {
            ComponentManager<IArchipelagoLink>.Value.Disconnect();
            string serverAddress = arguments[0];
            int currentIndex = 1;
            string username = readNextValue(arguments, ref currentIndex);
            string password = readNextValue(arguments, ref currentIndex);
            ComponentManager<IArchipelagoLink>.Value.Connect(serverAddress, username, string.IsNullOrEmpty(password) ? null : password);
            ComponentManager<IArchipelagoLink>.Value.SetIsInWorld(isInWorld());
        }
        else
        {
            Debug.LogError("Usage: <i>/connect (URL) (Username) (Password)</i> -- Password is optional. Parenthesis should be omitted unless part of URL, username, or password. If a value has spaces, use \"\" around it, eg \"My Unique Username\".");
        }
    }

    private static string readNextValue(string[] arguments, ref int currentIndex)
    {
        if (currentIndex >= arguments.Length)
        {
            return null;
        }
        StringBuilder valueBuilder = new StringBuilder();
        bool quotedValue = false;
        do
        {
            if (arguments[currentIndex].StartsWith("\""))
            {
                if (arguments[currentIndex].EndsWith("\"")) // Account for "MyValue"
                {
                    valueBuilder.Append(arguments[currentIndex].Substring(1, arguments[currentIndex].Length - 2));
                }
                else // "My Value[...]"
                {
                    quotedValue = true;
                    valueBuilder.Append(arguments[currentIndex].Substring(1));
                }
            }
            else
            {
                if (quotedValue)
                {
                    valueBuilder.Append(" "); // This may have been more than one space -- this is unavoidable without char substitutes. Not in scope at the moment.
                }

                if (quotedValue && arguments[currentIndex].EndsWith("\"")) // "My Value[...]"
                {
                    valueBuilder.Append(arguments[currentIndex].Substring(0, arguments[currentIndex].Length - 1));
                    quotedValue = false;
                }
                else // Non-quoted, or potentially MyValue" -- this may be a typo, but the mod will just do as it's told
                {
                    valueBuilder.Append(arguments[currentIndex]);
                }
            }
            currentIndex++;
        }
        while (quotedValue && currentIndex < arguments.Length);

        return valueBuilder.ToString();
    }

    [ConsoleCommand("/resync", "Resyncs Archipelago data from server host. Generally unnecessary.")]
    private static void Command_ResyncData(string[] arguments)
    {
        var resendPacket = ComponentManager<AssemblyManager>.Value.GetAssembly(AssemblyManager.RaftipelagoTypesAssembly).GetType("RaftipelagoTypes.RaftipelagoPacket_ResendData")
            .GetConstructor(new Type[] { typeof(Messages), typeof(MonoBehaviour_Network) }).Invoke(new object[] { Messages.NOTHING, ComponentManager<ResendDataBehaviour>.Value });
        ComponentManager<Semih_Network>.Value.RPC((Message)resendPacket, Target.All, EP2PSend.k_EP2PSendReliable, NetworkChannel.Channel_Game);
    }

    [ConsoleCommand("/disconnect", "Disconnects from the Archipelago server. You must put \"confirmDisconnect\" in order to confirm that you want to disconnect from the current session.")]
    private static void Command_Disconnect(string[] arguments)
    {
        if (arguments.Length == 1 && arguments[0].Equals("confirmDisconnect", StringComparison.InvariantCultureIgnoreCase))
        {
            ComponentManager<IArchipelagoLink>.Value.Disconnect();
        }
        else
        {
            Debug.LogError("Usage: <i>disconnect confirmDisconnect</i>");
        }
    }

    [ConsoleCommand("/generateItems", "Development-related command. Generates the JSON for Archipelago's Raft item list. Must be loaded into a world to use.")]
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

    [ConsoleCommand("/generateHiddenItems", "Development-related command. Generates the JSON for Archipelago's Raft item list, but it only gives you items that are not supposed to be included in the Archipelago item list. Must be loaded into a world to use.")]
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

    [ConsoleCommand("/generateLocations", "Development-related command. Generates the JSON for Archipelago's Raft location list. Must be loaded into a world to use.")]
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

    [ConsoleCommand("/generateHiddenLocations", "Development-related command. Generates the JSON for Archipelago's Raft location list, but it only gives you inaccessible researches in the Research Table. Must be loaded into a world to use.")]
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
        return !Semih_Network.InMenuScene;
    }
}