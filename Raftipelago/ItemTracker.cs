using Raftipelago.Data;
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Raftipelago.Network;

namespace Raftipelago
{
    public class ItemTracker
    {
        private const string ResourcePackIdentifier = "Resource Pack: ";
        private readonly Regex ResourcePackCommandRegex = new Regex(@"^\s*(\d+)\s+(.*)$");

        /// <summary>
        /// The index up to which items have been processed
        /// </summary>
        public int CurrentReceivedItemIndex { get; set; }

        /// <summary>
        /// A list of all levels of progressive items received. -1 is none received, 0 is one received, etc
        /// </summary>
        private Dictionary<string, int> _progressiveLevels = new Dictionary<string, int>();

        public void ResetData()
        {
            Logger.Debug("Resetting item data");
            ResetProgressives();
            CurrentReceivedItemIndex = 0;
        }

        public void ResetProgressives()
        {
            foreach (var progressiveName in ComponentManager<ExternalData>.Value.ProgressiveTechnologyMappings.Keys)
            {
                _progressiveLevels[progressiveName] = -1; // None unlocked = -1
            }
        }

        public void RaftItemUnlockedForCurrentWorld(long itemId, long locationId, int player, int itemIndex)
        {
            var sentItemName = ComponentManager<ArchipelagoDataManager>.Value.GetItemName(itemId);
            bool unlockingForFirstTime = CurrentReceivedItemIndex < itemIndex;
            if (!_unlockResourcePack(itemId, locationId, sentItemName, player, unlockingForFirstTime)
                && !_unlockProgressive(itemId, locationId, sentItemName, player, unlockingForFirstTime)
                && _unlockItem(itemId, sentItemName, locationId, player, unlockingForFirstTime) == UnlockResult.NotFound)
            {
                Logger.Error($"Unable to find {sentItemName} ({itemId}, {locationId})");
            }
            else
            {
                Logger.Trace($"Unlocked {itemId}::{locationId}::{player} ({itemIndex})");
            }
            if (Raft_Network.IsHost)
            {
                ComponentManager<MultiplayerComms>.Value.SendItem(itemId, locationId, player, itemIndex);
            }
            if (unlockingForFirstTime)
            {
                CurrentReceivedItemIndex = itemIndex;
            }
        }

        public bool _unlockResourcePack(long itemId, long locationId, string sentItemName, int player, bool unlockingForFirstTime)
        {
            if (sentItemName?.StartsWith(ResourcePackIdentifier) ?? false)
            {
                Logger.Trace($"Resource Pack identified: {sentItemName}");
                if (unlockingForFirstTime)
                {
                    var itemCommand = sentItemName.Substring(ResourcePackIdentifier.Length);
                    var resourcePackMatch = ResourcePackCommandRegex.Match(itemCommand);
                    if (resourcePackMatch.Success && int.TryParse(resourcePackMatch.Groups[1].Value, out int itemCount))
                    {
                        Logger.Info($"Resource Pack received: {itemCount} {resourcePackMatch.Groups[2].Value}");
                        RAPI.GetLocalPlayer().Inventory.AddItem(resourcePackMatch.Groups[2].Value, itemCount);
                        (ComponentManager<NotificationManager>.Value.ShowNotification("Research") as Notification_Research).researchInfoQue.Enqueue(
                            new Notification_Research_Info(itemCommand,
                                CommonUtils.GetFakeSteamIDForArchipelagoPlayerId(player),
                                ComponentManager<SpriteManager>.Value.GetArchipelagoSprite()));
                    }
                    else
                    {
                        Logger.Error("Could not parse resource command " + itemCommand);
                    }
                }
                Logger.Debug($"Resource Pack {sentItemName} already received, swallowing");
                return true;
            }
            return false;
        }

        // TODO Optimize -- we loop for every unlocked item, we can loop once for all unlocks
        public bool _unlockProgressive(long itemId, long locationId, string progressiveName, int fromPlayerId, bool unlockingForFirstTime)
        {
            if (progressiveName != null && _progressiveLevels.ContainsKey(progressiveName) && ComponentManager<ExternalData>.Value.ProgressiveTechnologyMappings.ContainsKey(progressiveName))
            {
                if (++_progressiveLevels[progressiveName] < ComponentManager<ExternalData>.Value.ProgressiveTechnologyMappings[progressiveName].Length)
                {
                    bool unlockedAnyItem = false;
                    foreach (var item in ComponentManager<ExternalData>.Value.ProgressiveTechnologyMappings[progressiveName][_progressiveLevels[progressiveName]])
                    {
                        var itemResult = _unlockItem(itemId, item, locationId, fromPlayerId, false);
                        if (itemResult == UnlockResult.NotFound)
                        {
                            Logger.Error($"Unable to unlock {item} from {progressiveName}");
                        }
                        else if (itemResult == UnlockResult.NewlyUnlocked)
                        {
                            Logger.Info($"Unlocking {item} from {progressiveName}");
                            unlockedAnyItem = true;
                        }
                        else if (itemResult == UnlockResult.AlreadyUnlocked)
                        {
                            Logger.Info($"{item} already unlocked from {progressiveName}");
                        }
                    }

                    if (unlockedAnyItem)
                    {
                        _sendResearchNotification(progressiveName, fromPlayerId);
                    }
                }
                // else duplicate, ignore
                Logger.Info($"{progressiveName} is duplicate, swallowing");
                return true;
            }
            else
            {
                // Item is likely not progressive, ignore
                Logger.Trace($"Progressive unable to be unlocked. ItemID {itemId} | LocationID {locationId} | progressiveName {progressiveName} | fromPlayerId {fromPlayerId}");
                return false;
            }
        }

        public UnlockResult _unlockItem(long itemId, string itemName, long locationId, int fromPlayerId, bool unlockingForFirstTime, bool showNotification = true)
        {
            var result = _unlockRecipe(itemId, itemName, locationId, fromPlayerId, unlockingForFirstTime, showNotification);
            if (result == UnlockResult.NotFound)
            {
                result = _unlockNote(itemName, showNotification);
            }
            return result;
        }

        public UnlockResult _unlockRecipe(long itemId, string itemName, long locationId, int fromPlayerId, bool unlockingForFirstTime, bool showNotification)
        {
            var uniqueItemName = CommonUtils.TryGetOrKey(ComponentManager<ExternalData>.Value.FriendlyItemNameToUniqueNameMappings, itemName);
            var foundItem = ComponentManager<CraftingMenu>.Value.AllRecipes.Find(itm => itm.UniqueName == uniqueItemName);
            if (foundItem != null)
            {
                Logger.Trace($"Item found: {itemId} => {uniqueItemName}");
                var wasLearned = foundItem.settings_recipe.Learned;
                foundItem.settings_recipe.Learned = true;
                if (!wasLearned && unlockingForFirstTime)
                {
                    Logger.Info($"Unlocking new item: {itemId} => {uniqueItemName}");
                    if (showNotification)
                    {
                        _sendResearchNotification(foundItem.settings_Inventory.DisplayName, fromPlayerId);
                    }
                    if (CanvasHelper.ActiveMenu == MenuType.Inventory)
                    {
                        ComponentManager<CraftingMenu>.Value.ReselectCategory();
                    }
                    return UnlockResult.NewlyUnlocked;
                }
                else
                {
                    Logger.Info($"Item already unlocked: {itemId} => {uniqueItemName}");
                    return UnlockResult.AlreadyUnlocked;
                }
            }
            return UnlockResult.NotFound;
        }

        public void _sendResearchNotification(string displayName, int playerId)
        {
            try
            {
                (ComponentManager<NotificationManager>.Value.ShowNotification("Research") as Notification_Research).researchInfoQue.Enqueue(
                    new Notification_Research_Info(displayName,
                        CommonUtils.GetFakeSteamIDForArchipelagoPlayerId(playerId),
                        ComponentManager<SpriteManager>.Value.GetArchipelagoSprite()));
            }
            catch (Exception e)
            {
                Logger.Warn("Error sending research notification: " + e.Message);
            }
        }

        public UnlockResult _unlockNote(string noteName, bool showNotification)
        {
            if (ComponentManager<ExternalData>.Value.FriendlyItemNameToUniqueNameMappings.TryGetValue(noteName, out string uniqueNoteName))
            {
                var notebook = ComponentManager<NoteBook>.Value;
                foreach (var nbNote in RAPI.GetLocalPlayer().NoteBookUI.GetAllNotes())
                {
                    if (nbNote.name == uniqueNoteName)
                    {
                        if (notebook.UnlockSpecificNoteWithUniqueNoteIndex(nbNote.noteIndex, true, false))
                        {
                            if (showNotification)
                            {
                                ComponentManager<NotificationManager>.Value.ShowNotification("NoteBookNote");
                            }
                            return UnlockResult.NewlyUnlocked;
                        }
                        else
                        {
                            return UnlockResult.AlreadyUnlocked;
                        }
                    }
                }
            }
            return UnlockResult.NotFound;
        }

        public enum UnlockResult
        {
            NotFound = 1,
            AlreadyUnlocked = 2,
            NewlyUnlocked = 3
        }
    }
}
