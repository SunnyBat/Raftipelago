using Raftipelago.Network.Behaviors;
using Raftipelago.Data;
using Steamworks;
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;

namespace Raftipelago
{
    public class ItemTracker
    {
        private const string ResourcePackIdentifier = "Resource Pack: ";
        private readonly Regex ResourcePackCommandRegex = new Regex(@"^\s*(\d+)\s+(.*)$");

        /// <summary>
        /// A list of all Item IDs that have already been received. This prevents duplicates.
        /// </summary>
        private List<long> _alreadyReceivedItemData = new List<long>();
        /// <summary>
        /// A list of all levels of progressive items received. -1 is none received, 0 is one received, etc
        /// </summary>
        private Dictionary<string, int> _progressiveLevels = new Dictionary<string, int>();

        public void SetAlreadyReceivedItemData(List<long> resourcePackIds)
        {
            _alreadyReceivedItemData = new List<long>(resourcePackIds);
        }

        public List<long> GetAllReceivedItemIds()
        {
            return _alreadyReceivedItemData;
        }

        public void ResetData()
        {
            ResetProgressives();
            _alreadyReceivedItemData.Clear();
        }

        public void ResetProgressives()
        {
            foreach (var progressiveName in ComponentManager<ExternalData>.Value.ProgressiveTechnologyMappings.Keys)
            {
                _progressiveLevels[progressiveName] = -1; // None unlocked = -1
            }
        }

        public void RaftItemUnlockedForCurrentWorld(int itemId, int locationId, int player)
        {
            var sentItemName = ComponentManager<ArchipelagoDataManager>.Value.GetItemName(itemId);
            if (!_unlockResourcePack(itemId, locationId, sentItemName, player)
                && !_unlockProgressive(itemId, locationId, sentItemName, player)
                && _unlockItem(itemId, sentItemName, locationId, player) == UnlockResult.NotFound)
            {
                Debug.LogError($"Unable to find {sentItemName} ({itemId}, {locationId})");
            }
            if (Semih_Network.IsHost)
            {
                var itemPacket = ComponentManager<AssemblyManager>.Value.GetAssembly(AssemblyManager.RaftipelagoTypesAssembly).GetType("RaftipelagoTypes.RaftipelagoPacket_SyncItems")
                    .GetConstructor(new Type[] { typeof(Messages), typeof(MonoBehaviour_Network) }).Invoke(new object[] { Messages.NOTHING, ComponentManager<ItemSyncBehaviour>.Value });
                var sid = ComponentManager<AssemblyManager>.Value.GetAssembly(AssemblyManager.RaftipelagoTypesAssembly).GetType("RaftipelagoTypes.SyncItemsData");
                var arr = Array.CreateInstance(sid, 1);
                var itmSend = sid.GetConstructor(new Type[] { }).Invoke(null);
                itmSend.GetType().GetProperty("ItemId").SetValue(itmSend, itemId);
                itmSend.GetType().GetProperty("LocationId").SetValue(itmSend, locationId);
                itmSend.GetType().GetProperty("PlayerId").SetValue(itmSend, player);
                arr.SetValue(itmSend, 0);
                itemPacket.GetType().GetProperty("Items").SetValue(itemPacket, arr);
                ComponentManager<Semih_Network>.Value.RPC((Message)itemPacket, Target.Other, EP2PSend.k_EP2PSendReliable, NetworkChannel.Channel_Game);
            }
        }

        public bool _unlockResourcePack(int itemId, int locationId, string sentItemName, int player)
        {
            if (sentItemName?.StartsWith(ResourcePackIdentifier) ?? false)
            {
                if (_alreadyReceivedItemData.AddUniqueOnly(calculateUniqueIdentifier(itemId, locationId, player)))
                {
                    var itemCommand = sentItemName.Substring(ResourcePackIdentifier.Length);
                    var resourcePackMatch = ResourcePackCommandRegex.Match(itemCommand);
                    if (resourcePackMatch.Success && int.TryParse(resourcePackMatch.Groups[1].Value, out int itemCount))
                    {
                        RAPI.GetLocalPlayer().Inventory.AddItem(resourcePackMatch.Groups[2].Value, itemCount);
                        (ComponentManager<NotificationManager>.Value.ShowNotification("Research") as Notification_Research).researchInfoQue.Enqueue(
                            new Notification_Research_Info(itemCommand,
                                CommonUtils.GetFakeSteamIDForArchipelagoPlayerId(player),
                                ComponentManager<SpriteManager>.Value.GetArchipelagoSprite()));
                    }
                    else
                    {
                        Debug.LogError("Could not parse resource command " + itemCommand);
                    }
                }
                return true;
            }
            return false;
        }

        // TODO Optimize -- we loop for every unlocked item, we can loop once for all unlocks
        public bool _unlockProgressive(int itemId, int locationId, string progressiveName, int fromPlayerId)
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
                            Debug.LogError($"Unable to unlock {item} from {progressiveName}");
                        }
                        else if (itemResult == UnlockResult.NewlyUnlocked)
                        {
                            unlockedAnyItem = true;
                        }
                    }

                    if (unlockedAnyItem)
                    {
                        _sendResearchNotification(progressiveName, fromPlayerId);
                    }
                }
                // else duplicate, ignore
                return true;
            }
            else
            {
                return false;
            }
        }

        public UnlockResult _unlockItem(int itemId, string itemName, int locationId, int fromPlayerId, bool showNotification = true)
        {
            var result = _unlockRecipe(itemId, itemName, locationId, fromPlayerId, showNotification);
            if (result == UnlockResult.NotFound)
            {
                result = _unlockNote(itemName, showNotification);
            }
            return result;
        }

        public UnlockResult _unlockRecipe(int itemId, string itemName, int locationId, int fromPlayerId, bool showNotification)
        {
            var uniqueItemName = CommonUtils.TryGetOrKey(ComponentManager<ExternalData>.Value.FriendlyItemNameToUniqueNameMappings, itemName);
            var foundItem = ComponentManager<CraftingMenu>.Value.AllRecipes.Find(itm => itm.UniqueName == uniqueItemName);
            if (foundItem != null)
            {
                var wasLearned = foundItem.settings_recipe.Learned;
                foundItem.settings_recipe.Learned = true;
                if (!wasLearned && _alreadyReceivedItemData.AddUniqueOnly(calculateUniqueIdentifier(itemId, locationId, fromPlayerId)))
                {
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
            catch (Exception)
            {
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

        /// <summary>
        /// Runs off of the assumption that we have a limited ID set for items and locations.
        /// </summary>
        /// <param name="itemId">The Item ID unlocked</param>
        /// <param name="locationId">The Location ID unlocked</param>
        /// <returns></returns>
        private long calculateUniqueIdentifier(int itemId, int locationId, int playerId)
        {
            // First 16 bits = blank, second 16 bits = ItemID, third 16 bits = LocationID, last 16 bits = PlayerID
            long itemIdShifted = (itemId - 47000) & 0xFFFF;
            itemIdShifted <<= 32; // Could also cast full result before shift to long; we shift bits out of number without casting
            long locationIdShifted = (locationId - 48000) & 0xFFFF;
            locationIdShifted <<= 16;
            long playerIdShifted = playerId & 0xFFFF;
            return itemIdShifted | locationIdShifted | playerIdShifted;
        }

        public Tuple<int, int, int> ParseUniqueIdentifier(long identifier)
        {
            var itemId = (identifier >> 32) & 0xFFFF;
            itemId += 47000;
            var locationId = (identifier  >> 16) & 0xFFFF;
            locationId += 48000;
            var playerId = identifier & 0xFFFF;
            return new Tuple<int, int, int>((int)itemId, (int)locationId, (int)playerId);
        }
    }
}
