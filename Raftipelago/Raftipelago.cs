using HarmonyLib;
using Raftipelago;
using Raftipelago.Data;
using Raftipelago.Network;
using Raftipelago.UnityScripts;
using System.Collections;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEngine;

public class RaftipelagoThree : Mod
{
    private Harmony patcher;
    private IEnumerator serverHeartbeat;
    public void Start()
    {
        ComponentManager<EmbeddedFileUtils>.Value = ComponentManager<EmbeddedFileUtils>.Value ?? new EmbeddedFileUtils(GetEmbeddedFileBytes);
        ComponentManager<SpriteManager>.Value = ComponentManager<SpriteManager>.Value ?? new SpriteManager();
        ComponentManager<IArchipelagoLink>.Value = ComponentManager<IArchipelagoLink>.Value ?? new ProxiedArchipelago();
        patcher = new Harmony("com.github.sunnybat.raftipelago");
        patcher.PatchAll(Assembly.GetExecutingAssembly());
        serverHeartbeat = ArchipelagoLinkHeartbeat.CreateNewHeartbeat(ComponentManager<IArchipelagoLink>.Value, 0.1f); // Trigger every 100ms
        if (isInWorld())
        {
            WorldEvent_WorldLoaded();
        }
        StartCoroutine(serverHeartbeat);
        Debug.Log("Mod Raftipelago has been loaded!");
    }

    public void OnModUnload()
    {
        StopCoroutine(serverHeartbeat);
        ComponentManager<IArchipelagoLink>.Value?.Disconnect();
        ComponentManager<IArchipelagoLink>.Value = null;
        patcher?.UnpatchAll("com.github.sunnybat.raftipelago");
        Debug.Log("Raftipelago has been stopped.");
    }

    // This should ONLY be used for Archipelago-related setup; this is called even after
    // the world has been loaded for a while.
    public override void WorldEvent_WorldLoaded()
    {
        var archipelagoLink = ComponentManager<IArchipelagoLink>.Value;
        archipelagoLink.SetIsInWorld(true);
    }

    public override void WorldEvent_WorldUnloaded()
    {
        ComponentManager<IArchipelagoLink>.Value.SetIsInWorld(false);
    }

    // TODO Move to in-game chat instead of console
    [ConsoleCommand("/chatMessage", "Send a message through the Raft proxy")]
    private static void Command_chatMessage(string[] arguments)
    {
        if (arguments.Length >= 1)
        {
            ComponentManager<IArchipelagoLink>.Value.SendChatMessage(string.Join(" ", arguments));
        }
        else
        {
            Debug.LogError("Usage: <i>/chatMessage (message)</i>");
        }
    }

    // TODO Add to in-game chat as well (keep this implementation to be able to choose either)
    [ConsoleCommand("/connect", "Connect to the Archipelago server. It's recommended to use a full address, eg \"/connect http://archipelago.gg:38281 UsernameGoesHere OptionalPassword\".")]
    private static void Command_Connect(string[] arguments)
    {
        if (ComponentManager<IArchipelagoLink>.Value.IsSuccessfullyConnected() == true)
        {
            Debug.Log("Already connected to Archipelago. Disconnect before attempting to connect to a different server.");
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
            Debug.LogError("Must be loaded into a world to generate item list.");
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
            Debug.LogError("Must be loaded into a world to generate item list.");
        }
    }

    private static bool isInWorld()
    {
        return ComponentManager<CraftingMenu>.Value != null; // TODO Better way to determine?
    }
}