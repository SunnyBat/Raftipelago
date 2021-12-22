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
            ComponentManager<IArchipelagoLink>.Value.SetIsInWorld(true);
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
        Debug.Log("Raftipelago has been stopped. You may reconnect at any time.");
    }

    public override void WorldEvent_WorldLoaded()
    {
        ComponentManager<IArchipelagoLink>.Value.SetIsInWorld(true);
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
        if (arguments.Length == 1 && arguments[1].Equals("confirmDisconnect", System.StringComparison.InvariantCultureIgnoreCase))
        {
            ComponentManager<IArchipelagoLink>.Value.Disconnect();
        }
        else
        {
            Debug.LogError("Usage: <i>disconnect confirmDisconnect</i>");
            arguments?.Do(arg => Debug.Log(arg));
        }
    }

    private bool isInWorld()
    {
        return ComponentManager<CraftingMenu>.Value != null; // TODO Better way to determine?
    }

    private void DebugStuff2()
    {
        var i_rT = ComponentManager<Inventory_ResearchTable>.Value;
        var hsb = new StringBuilder();
        var sb = new StringBuilder();
        i_rT.GetMenuItems().ForEach(rmi =>
        {
            var baseItem = rmi.GetItem();
            if (!baseItem.settings_recipe.HiddenInResearchTable && !baseItem.settings_recipe.LearnedViaBlueprint)
            {
                sb.Append(baseItem.UniqueName + ",");
            }
            else
            {
                hsb.Append(baseItem.UniqueName + ",");
            }
        });
        Debug.Log(hsb);
        Debug.Log("==============");
        Debug.Log(sb);
    }

    private void DebugStuff()
    {
        var availableItemList = new string[] {
            "Empty Cup", "Simple Purifier", "Simple Grill", "Small Crop Plot", // Food/Water
            "Research Table", "Simple Bed", "Small Storage", // Other
            "Building Hammer", "Plastic Hook", "Stone Axe", "Fishing Rod", "Shark Bait", // Tools
            "Wooden Spear", // Weapons
            // Equipment
            "Rope", "Nail", "Wet Brick", // Resources
            "Throwable Anchor", "Paddle", "Sail", "Streamer", // Navigation
            "Seating", "Tables", "Shelves", "Sign", "Calendar", "Rug", "Clock" // Decorations
        };
        var craftingMenu = ComponentManager<CraftingMenu>.Value;
        var matchedItemCount = 0;
        var allItemData = new StringBuilder();
        allItemData.Append("[");
        craftingMenu.AllRecipes.Do(recipe =>
        {
            if (recipe.settings_recipe.CraftingCategory != CraftingCategory.Hidden
                && recipe.settings_recipe.CraftingCategory != CraftingCategory.Decorations
                && recipe.settings_recipe.CraftingCategory != CraftingCategory.CreativeMode
                && recipe.settings_recipe.CraftingCategory != CraftingCategory.Skin
                && !recipe.settings_recipe.LearnedFromBeginning)
            {
                if (allItemData.Length > 1)
                {
                    allItemData.Append(",");
                }
                allItemData.Append("{");
                allItemData.Append($"\"Name\":\"{recipe.name}\"");
                allItemData.Append(",");
                allItemData.Append($"\"DisplayName\":\"{recipe.settings_Inventory.DisplayName}\"");
                allItemData.Append(",");
                allItemData.Append($"\"UniqueName\":\"{recipe.UniqueName}\"");
                allItemData.Append(",");
                allItemData.Append($"\"LearnedByDefault\":\"{recipe.settings_recipe.LearnedFromBeginning}\"");
                allItemData.Append(",");
                allItemData.Append($"\"Learned\":\"{recipe.settings_recipe.Learned}\"");
                allItemData.Append(",");
                allItemData.Append($"\"LearnedViaBlueprint\":\"{recipe.settings_recipe.LearnedViaBlueprint}\"");
                allItemData.Append(",");
                allItemData.Append($"\"CanCraft\":\"{recipe.settings_recipe.CanCraft}\"");
                allItemData.Append(",");
                allItemData.Append($"\"CraftingCategory\":\"{recipe.settings_recipe.CraftingCategory}\"");
                allItemData.Append(",");
                allItemData.Append($"\"SubCategory\":\"{recipe.settings_recipe.SubCategory}\"");
                allItemData.Append("}");
            }
        });
        allItemData.Append("]");
        Debug.Log($"E:{availableItemList.Length}, A:{matchedItemCount}");
        Debug.Log(allItemData.ToString());
    }
}