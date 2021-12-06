using HarmonyLib;
using Raftipelago;
using Raftipelago.Data;
using Raftipelago.Network;
using System;
using System.IO;
using System.Reflection;
using System.Text;
using System.Threading;
using UnityEngine;

public class RaftipelagoTwo : Mod
{
    private Harmony patcher;
    public void Start()
    {
        if (ComponentManager<ArchipelagoLink>.Value == null)
        {
            ComponentManager<ArchipelagoLink>.Value = new ArchipelagoLink(); // TODO Get URL, username, password from user
        }
        else
        {
            Debug.LogError("ArchipelagoLink still active, cannot connect");
        }
        ComponentManager<EmbeddedFileUtils>.Value = ComponentManager<EmbeddedFileUtils>.Value ?? new EmbeddedFileUtils(GetEmbeddedFileBytes);
        ComponentManager<SpriteManager>.Value = ComponentManager<SpriteManager>.Value ?? new SpriteManager();
        ComponentManager<ItemMapping>.Value = ComponentManager<ItemMapping>.Value ?? new ItemMapping();
        startProxyServer();
        patcher = new Harmony("com.github.sunnybat.raftipelago");
        patcher.PatchAll(Assembly.GetExecutingAssembly());
        //DebugStuff();
        Debug.Log("Mod Raftipelago has been loaded!");
    }

    public void OnModUnload()
    {
        // TODO Any additional cleanup
        patcher.UnpatchAll("com.github.sunnybat.raftipelago");
        ComponentManager<ArchipelagoLink>.Value?.CloseSession();
        ComponentManager<ArchipelagoLink>.Value = null;
        ComponentManager<ProxyServerDIOnly>.Value?.Disconnect();
        ComponentManager<ProxyServerDIOnly>.Value = null;
        Debug.Log("Mod Raftipelago has been unloaded!");
    }


    [ConsoleCommand("proxymsg", "Send a message through the Raft proxy")]
    private static void SendRawMessage(string[] arguments)
    {
        if (arguments.Length == 2)
        {
            ComponentManager<ProxyServerDIOnly>.Value?.sendMessage(arguments[0], arguments[1]);
        }
        else
        {
            Debug.LogError("Usage: <i>proxymsg (messageType) (message)</i>");
            arguments?.Do(arg => Debug.Log(arg));
        }
    }

    private void startProxyServer()
    {
        ComponentManager<ProxyServerDIOnly>.Value = ComponentManager<ProxyServerDIOnly>.Value ?? new ProxyServerDIOnly();
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