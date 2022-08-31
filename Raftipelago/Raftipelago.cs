using HarmonyLib;
using Raftipelago;
using Raftipelago.Data;
using Raftipelago.Network;
using Raftipelago.Patches;
using Raftipelago.UnityScripts;
using System;
using System.Collections;
using System.IO;
using System.Reflection;
using System.Text;
using UnityEngine;

public class RaftipelagoThree : Mod
{
    // Sets ModUtils class to handle sending/receiving data (so everything's not shoved into this one class)
    private static MultiplayerComms ModUtils_Reciever = ComponentManager<MultiplayerComms>.Value = new MultiplayerComms();

    private const string EmbeddedFileDirectory = "Data";
    public const string AppDataFolderName = "Raftipelago";

    private Harmony patcher;
    private IEnumerator serverHeartbeat;

    public void Start()
    {
        //if (_isInWorld())
        //{
        //    base.UnloadMod();
        //    throw new Exception("Raftipelago must be loaded in main menu.");
        //}
        string appDataDirectory = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var proxyServerDirectory = Path.Combine(appDataDirectory, AppDataFolderName);
        // We only need to load assemblies once; Raft needs to be restarted to load new versions, so we just keep the instance around forever
        ComponentManager<EmbeddedFileUtils>.Value = ComponentManager<EmbeddedFileUtils>.Value ?? new EmbeddedFileUtils(GetEmbeddedFileBytes);
        ComponentManager<AssemblyManager>.Value = ComponentManager<AssemblyManager>.Value ?? new AssemblyManager(EmbeddedFileDirectory, proxyServerDirectory);
        try
        {
            ModUtils_Reciever.RegisterData();
        }
        catch (Exception e)
        {
            Raftipelago.Logger.Trace("ERROR");
            //base.UnloadMod();
            throw e;
        }
        ComponentManager<ExternalData>.Value = ComponentManager<ExternalData>.Value ?? new ExternalData(ComponentManager<EmbeddedFileUtils>.Value);
        ComponentManager<SpriteManager>.Value = ComponentManager<SpriteManager>.Value ?? new SpriteManager();
        ComponentManager<ItemTracker>.Value = ComponentManager<ItemTracker>.Value ?? new ItemTracker();
        ComponentManager<ArchipelagoDataManager>.Value = ComponentManager<ArchipelagoDataManager>.Value ?? new ArchipelagoDataManager();
        ComponentManager<ItemTracker>.Value.ResetData();
        patcher = new Harmony("com.github.sunnybat.raftipelago");
        patcher.PatchAll(Assembly.GetExecutingAssembly());
        ComponentManager<IArchipelagoLink>.Value = ComponentManager<IArchipelagoLink>.Value ?? new ProxiedArchipelago();
        serverHeartbeat = ArchipelagoLinkHeartbeat.CreateNewHeartbeat(ComponentManager<IArchipelagoLink>.Value, 0.1f); // Trigger every 100ms
        StartCoroutine(serverHeartbeat);
    }

    public void OnModUnload()
    {
        StopCoroutine(serverHeartbeat);
        ComponentManager<IArchipelagoLink>.Value?.Disconnect();
        if (_isInWorld())
        {
            ComponentManager<ItemTracker>.Value.ResetData();
        }
        ComponentManager<IArchipelagoLink>.Value?.onUnload();
        ComponentManager<IArchipelagoLink>.Value = null;
        patcher?.UnpatchAll("com.github.sunnybat.raftipelago");
    }

    // This should ONLY be used for Archipelago-related setup; this is called even after
    // the world has been loaded for a while.
    public override void WorldEvent_WorldLoaded()
    {
        Raftipelago.Logger.Trace("World loaded");
        // Always add these so they have the same IDs
        if (Raft_Network.IsHost)
        {
            Raftipelago.Logger.Debug("Is world host");
            ComponentManager<IArchipelagoLink>.Value.SetIsInWorld(true);
        }
        else
        {
            Raftipelago.Logger.Debug("Not world host");
        }
    }

    public override void WorldEvent_WorldUnloaded()
    {
        Raftipelago.Logger.Trace("World unloaded");
        // *Sync objects are automatically cleared from NetworkUpdateManager
        // We can just leave the values hanging in ComponentManager since they'll be overwritten
        ComponentManager<IArchipelagoLink>.Value.SetIsInWorld(false);
        // Reset completion status since it will be updated upon world load
        ComponentManager<IArchipelagoLink>.Value.SetGameCompleted(false);
        // Reset station count when world unloaded so we don't trigger on reload into a different world
        HarmonyPatch_BalboaRelayStationScreen_RefreshScreen.previousStationCount = -1;
        // Rreset ItemTracker unlocks so we don't trigger on reload into a different world
        ComponentManager<ItemTracker>.Value.ResetData();
    }

    [ConsoleCommand("/connect", "Connect to the Archipelago server. It's recommended to use a full address, eg \"/connect http://archipelago.gg:38281 UsernameGoesHere OptionalPassword\".")]
    private static void Command_Connect(string[] arguments)
    {
        if (!Raft_Network.IsHost && !Raft_Network.InMenuScene)
        {
            Debug.LogError("Only the world host can connect to Archipelago.");
        }
        else if (ComponentManager<IArchipelagoLink>.Value.IsSuccessfullyConnected() == true)
        {
            Debug.LogError("Already connected to Archipelago. Disconnect with /disconnect before attempting to connect to a different server. Be careful when connecting to different servers with the same world; Archipelago location unlocks are permanent.");
        }
        else if (arguments.Length >= 2)
        {
            Raftipelago.Logger.Debug("Disconnecting");
            ComponentManager<IArchipelagoLink>.Value.Disconnect();
            Raftipelago.Logger.Debug("Reading args");
            string serverAddress = arguments[0];
            int currentIndex = 1;
            string username = _readNextValue(arguments, ref currentIndex);
            string password = _readNextValue(arguments, ref currentIndex);
            Raftipelago.Logger.Debug("Connecting");
            ComponentManager<IArchipelagoLink>.Value.Connect(serverAddress, username, string.IsNullOrEmpty(password) ? null : password);
            Raftipelago.Logger.Debug("Postconnect");
            //ComponentManager<IArchipelagoLink>.Value.SetIsInWorld(_isInWorld());
            Raftipelago.Logger.Debug("Connect command sent");
        }
        else
        {
            Debug.LogError("Usage: <i>/connect (URL) (Username) (Password)</i> -- Password is optional. Parenthesis should be omitted unless part of URL, username, or password. If a value has spaces, use \"\" around it, eg \"My Unique Username\".");
        }
    }

    [ConsoleCommand("/resync", "Resyncs Archipelago data from server host. Generally unnecessary.")]
    private static void Command_ResyncData(string[] arguments)
    {
        ComponentManager<MultiplayerComms>.Value.SendArchipelagoData();
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

    [ConsoleCommand("/setRaftipelagoLogLevel", "Set the logging level for Raftipelago. Valid values are 1-6, with 1 being Trace and 6 being Fatal.")]
    private static void Command_SetLogLevel(string[] arguments)
    {
        if (arguments.Length == 1 && Enum.TryParse<Raftipelago.Logger.LogLevel>(arguments[0], out var logLevel) && Enum.IsDefined(typeof(Raftipelago.Logger.LogLevel), logLevel))
        {
            Raftipelago.Logger.SetLogLevel(logLevel);
            Debug.Log("Set log level to " + logLevel.ToString());
        }
        else
        {
            Debug.LogError("Usage: <i>setRaftipelagoLogLevel #</i> where # is between 1 (Trace) and 6 (Fatal)");
        }
    }

    [ConsoleCommand("/setRaftipelagoStackLevel", "Set the stack level for Raftipelago logging. Valid values are 1-5, with 1 being Full and 5 being None")]
    private static void Command_SetStackLevel(string[] arguments)
    {
        if (arguments.Length == 1 && Enum.TryParse<Raftipelago.Logger.StackLevel>(arguments[0], out var stackLevel) && Enum.IsDefined(typeof(Raftipelago.Logger.StackLevel), stackLevel))
        {
            Raftipelago.Logger.SetStackLevel(stackLevel);
            Debug.Log("Set stack level to " + stackLevel.ToString());
        }
        else
        {
            Debug.LogError("Usage: <i>setRaftipelagoLogLevel #</i> where # is between 1 (Full) and 5 (None)");
        }
    }

#if false
    [ConsoleCommand("/generateItemsForRaftipelago", "Development-related command. Generates the JSON for Raft's friendly name mappings. Must be generated using the English locale.")]
    private static void Command_GenerateFriendlyItems(string[] arguments)
    {
        if (isInWorld())
        {
            Debug.Log(DataGenerator.GenerateRaftipelagoFriendlyItemList());
        }
        else
        {
            Debug.LogError("Must be loaded into a world to generate item list.");
        }
    }

    [ConsoleCommand("/generateLocationsForRaftipelago", "Development-related command. Generates the JSON for Raft's friendly location mappings. Must be generated using the English locale.")]
    private static void Command_GenerateFriendlyLocations(string[] arguments)
    {
        if (isInWorld())
        {
            Debug.Log(DataGenerator.GenerateFriendlyLocationList());
        }
        else
        {
            Debug.LogError("Must be loaded into a world to generate item list.");
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

    [ConsoleCommand("/generateProgressives", "Development-related command. Generates the JSON for Archipelago's Raft progressives list.")]
    private static void Command_GenerateProgressives(string[] arguments)
    {
        Debug.Log(DataGenerator.GenerateProgressiveList());
    }

    [ConsoleCommand("/printItems", "Development-related command. Prints a list of items.")]
    private static void Command_PrintItems(string[] arguments)
    {
        var filter = (arguments.Length >= 1 ? arguments[0] : "").ToLower();
        ItemManager.GetAllItems().ForEach(item =>
        {
            if (item?.UniqueName != null && (item.UniqueName.ToLower().Contains(filter) || item.name.ToLower().Contains(filter)))
            {
                Debug.Log($"{item.name} :: {item.UniqueName}");
            }
        });
    }

    [ConsoleCommand("/spawn", "Spawns a landmark, namely any type of island.")]
    private static void Command_Spawn(string[] arguments)
    {
        string text = arguments[0];
        Array values = Enum.GetValues(typeof(ChunkPointType));
        int num = 150;
        if (arguments.Length >= 2 && int.TryParse(arguments[1], out int num2))
        {
            num = num2;
        }
        foreach (object obj in values)
        {
            ChunkPointType pointType = (ChunkPointType)obj;
            if (text.Contains("clear", StringComparison.OrdinalIgnoreCase) && arguments.Length >= 2)
            {
                string text2 = arguments[1];
                if (text2.Contains("all", StringComparison.OrdinalIgnoreCase))
                {
                    ComponentManager<ChunkManager>.Value.ClearAllChunkPoints();
                    break;
                }
                if (pointType.ToString().Contains(text2, StringComparison.OrdinalIgnoreCase))
                {
                    ComponentManager<ChunkManager>.Value.ClearChunkPoints(pointType, 0U);
                    break;
                }
            }
            else if (pointType.ToString().Contains(text, StringComparison.OrdinalIgnoreCase))
            {
                ComponentManager<ChunkManager>.Value.AddChunkPointCheat(pointType, global::Raft.direction.normalized * (float)num);
                break;
            }
        }
    }
#endif

    private static string _readNextValue(string[] arguments, ref int currentIndex)
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

    private static bool _isInWorld()
    {
        return !Raft_Network.InMenuScene;
    }
}