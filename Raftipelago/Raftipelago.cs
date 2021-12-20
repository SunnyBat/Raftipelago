using HarmonyLib;
using Raftipelago.Data;
using Raftipelago.Network;
using System.Reflection;
using System.Text;
using UnityEngine;

public class RaftipelagoThree : Mod
{
    private Harmony patcher;
    public void Start()
    {
        ComponentManager<EmbeddedFileUtils>.Value = ComponentManager<EmbeddedFileUtils>.Value ?? new EmbeddedFileUtils(GetEmbeddedFileBytes);
        ComponentManager<SpriteManager>.Value = ComponentManager<SpriteManager>.Value ?? new SpriteManager();
        startProxyServer();
        patcher = new Harmony("com.github.sunnybat.raftipelago");
        patcher.PatchAll(Assembly.GetExecutingAssembly());
        //DebugStuff2();
        //DebugStuff();
        Debug.Log("Mod Raftipelago has been loaded!");
    }

    public void OnModUnload()
    {
        // TODO Any additional cleanup
        ComponentManager<IArchipelagoLink>.Value?.Disconnect();
        ComponentManager<IArchipelagoLink>.Value = null;
        patcher?.UnpatchAll("com.github.sunnybat.raftipelago");
        Debug.Log("Mod Raftipelago has been unloaded!");
    }


    [ConsoleCommand("chatMessage", "Send a message through the Raft proxy")]
    private static void Command_chatMessage(string[] arguments)
    {
        if (arguments.Length == 1)
        {
            ComponentManager<IArchipelagoLink>.Value?.SendChatMessage(arguments[0]);
        }
        else
        {
            Debug.LogError("Usage: <i>chatMessage (message)</i>");
            arguments?.Do(arg => Debug.Log(arg));
        }
    }

    [ConsoleCommand("dq", "Test item dequeuing")]
    private static void dq(string[] arguments)
    {
        ((ProxiedArchipelago)ComponentManager<IArchipelagoLink>.Value)?.dq();
    }

    private void startProxyServer()
    {
        if (ComponentManager<IArchipelagoLink>.Value == null)
        {
            ComponentManager<IArchipelagoLink>.Value = new ProxiedArchipelago(); // TODO Get URL, username, password from user
        }
        else
        {
            Debug.LogError("ArchipelagoLink still active");
        }
        ComponentManager<IArchipelagoLink>.Value.Connect("localhost", "SunnyBat-Raft", "");
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
                && recipe.settings_recipe.CraftingCategory != CraftingCategory.Skin)
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