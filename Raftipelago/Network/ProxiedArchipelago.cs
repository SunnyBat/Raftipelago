using Newtonsoft.Json;
using Raftipelago.Data;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using UnityEngine;

namespace Raftipelago.Network
{
    /// <summary>
    /// A communication layer for Archipelago that uses dynamically-loaded libraries.
    /// </summary>
    public class ProxiedArchipelago : IArchipelagoLink
    {
        private const string ArchipelagoProxyClassNamespaceIdentifier = "ArchipelagoProxy.ArchipelagoProxy";
        private const string ResourcePackIdentifier = "Resource Pack: ";
        private readonly Regex ResourcePackCommandRegex = new Regex(@"^\s*(\d+)\s+(.*)$");

        private Assembly _proxyAssembly;
        private object _proxyServer;

        private MethodInfo _isSuccessfullyConnectedMethodInfo;
        private MethodInfo _setIsPlayerInWorldMethodInfo;
        private MethodInfo _sendChatMessageMethodInfo;
        private MethodInfo _locationFromCurrentWorldUnlockedMethodInfo;
        private MethodInfo _getLocationIdFromNameMethodInfo; 
        private MethodInfo _getItemNameFromIdMethodInfo;
        private MethodInfo _getPlayerAliasMethodInfo;
        private MethodInfo _setGameCompletedMethodInfo;
        private MethodInfo _heartbeatMethodInfo;
        private MethodInfo _disconnectMethodInfo;

        private List<int> _alreadyReceivedItemIds = new List<int>();
        private Dictionary<string, int> _progressiveLevels = new Dictionary<string, int>();
        private bool shouldPrintDebugMessages = false;
        public ProxiedArchipelago()
        {
            _proxyAssembly = ComponentManager<AssemblyManager>.Value.GetAssembly(AssemblyManager.ArchipelagoProxyAssembly);
            var proxyServerRef = _proxyAssembly.GetType(ArchipelagoProxyClassNamespaceIdentifier);
            _initMethodInfo(proxyServerRef);
        }

        public void Connect(string URL, string username, string password)
        {
            if (IsSuccessfullyConnected())
            {
                throw new InvalidOperationException("Already connected to server");
            }
            else
            {
                // Reset progressives on each connect since we'll be rewriting it all
                foreach (var progressiveName in ComponentManager<ExternalData>.Value.ProgressiveTechnologyMappings.Keys)
                {
                    _progressiveLevels[progressiveName] = -1; // None unlocked = -1
                }
                var proxyServerRef = _proxyAssembly.GetType(ArchipelagoProxyClassNamespaceIdentifier);
                _proxyServer = _createNewArchipelagoProxy(proxyServerRef, URL);
                _hookUpEvents(proxyServerRef);
                _connectToArchipelago(proxyServerRef, username, password);
            }
        }

        public bool IsSuccessfullyConnected()
        {
            return _proxyServer != null && (bool) _isSuccessfullyConnectedMethodInfo.Invoke(_proxyServer, null);
        }

        public void Heartbeat()
        {
            if (_proxyServer != null)
            {
                _heartbeatMethodInfo.Invoke(_proxyServer, null);
            }
        }

        public void SetIsInWorld(bool inWorld)
        {
            if (_proxyServer != null)
            {
                if (inWorld)
                {
                    var locationList = new List<string>();
                    locationList.AddRange(ComponentManager<Inventory_ResearchTable>.Value.GetMenuItems()
                        .FindAll(itm => itm.Learned && CommonUtils.IsValidResearchTableItem(itm.GetItem()))
                        .Select(itm => itm.GetItem().settings_Inventory.DisplayName));
                    WorldManager.AllLandmarks.ForEach(landmark =>
                    {
                        // TODO Should I filter by story island?
                        // Can get IDs from SceneLoader debug prints.
                        // In theory notes will only be on story islands, so non-story islands will be fine.
                        foreach (var landmarkItem in landmark.landmarkItems)
                        {
                            if (CommonUtils.IsNoteOrBlueprint(landmarkItem) && !landmarkItem.gameObject.activeSelf
                                && ComponentManager<ExternalData>.Value.UniqueLocationNameToFriendlyNameMappings.TryGetValue(landmarkItem.name, out string friendlyName))
                            {
                                if (friendlyName == "Tangaroa Next Frequency") // Special condition for victory
                                {
                                    SetGameCompleted(true);
                                }
                                else
                                {
                                    locationList.Add(friendlyName);
                                }
                            }
                        }
                    });
                    if (CommonUtils.HasFinishedRelayStationQuest())
                    {
                        locationList.Add("Relay Station quest");
                    }
                    LocationUnlocked(locationList.ToArray());
                }
                _setIsPlayerInWorldMethodInfo.Invoke(_proxyServer, new object[] { inWorld, false });
            }
        }

        public void SendChatMessage(string message)
        {
            if (_proxyServer != null)
            {
                _sendChatMessageMethodInfo.Invoke(_proxyServer, new object[] { message });
            }
        }

        public void LocationUnlocked(params int[] locationIds)
        {
            if (_proxyServer != null)
            {
                _locationFromCurrentWorldUnlockedMethodInfo.Invoke(_proxyServer, new object[] { locationIds });
            }
        }

        public void LocationUnlocked(params string[] locationNames)
        {
            if (_proxyServer != null)
            {
                List<int> locationIds = new List<int>();
                foreach (var locName in locationNames)
                {
                    var locationId = (int)_getLocationIdFromNameMethodInfo.Invoke(_proxyServer, new object[] { locName });
                    if (locationId != -1)
                    {
                        locationIds.Add(locationId);
                    }
                    else
                    {
                        Debug.Log("Error finding ID for location " + locName + ", event will be swallowed");
                    }
                }
                if (locationIds.Count > 0)
                {
                    LocationUnlocked(locationIds.ToArray());
                }
            }
        }

        public string GetItemNameFromId(int itemId)
        {
            if (IsSuccessfullyConnected())
            {
                return (string)_getItemNameFromIdMethodInfo.Invoke(_proxyServer, new object[] { itemId });
            }
            else
            {
                return null;
            }
        }

        public string GetPlayerAlias(int playerId)
        {
            if (_proxyServer != null)
            {
                return (string)_getPlayerAliasMethodInfo.Invoke(_proxyServer, new object[] { playerId });
            }
            else
            {
                return null;
            }
        }

        public void SetGameCompleted(bool completed)
        {
            if (_proxyServer != null)
            {
                _setGameCompletedMethodInfo.Invoke(_proxyServer, new object[] { completed });
            }
        }

        public void Disconnect()
        {
            if (_proxyServer != null)
            {
                try
                {
                    _disconnectMethodInfo.Invoke(_proxyServer, new object[] { });
                    _proxyServer = null; // Reset since we will make a new one to reconnect
                }
                catch (Exception e)
                {
                    // At this point all we can do is clean up and try again later
                    _proxyServer = null;
                    Debug.LogError(e);
                }
            }
        }

        public string GetNameFromPlayerId(int playerId)
        {
            return (string) _getItemNameFromIdMethodInfo.Invoke(_proxyServer, new object[] { playerId });
        }

        public void ToggleDebug()
        {
            shouldPrintDebugMessages = !shouldPrintDebugMessages;
        }

        public void SetAlreadyReceivedItemIds(List<int> resourcePackIds)
        {
            this._alreadyReceivedItemIds = new List<int>(resourcePackIds);
        }

        public List<int> GetAllReceivedItemIds()
        {
            return _alreadyReceivedItemIds;
        }

        private void _initMethodInfo(Type proxyServerRef)
        {
            // Events for data that we send to proxy
            // If a method is overloaded, it needs specific types. Otherwise, no typing is needed.
            _isSuccessfullyConnectedMethodInfo = proxyServerRef.GetMethod("IsSuccessfullyConnected");
            _setIsPlayerInWorldMethodInfo = proxyServerRef.GetMethod("SetIsPlayerInWorld");
            _sendChatMessageMethodInfo = proxyServerRef.GetMethod("SendChatMessage");
            _locationFromCurrentWorldUnlockedMethodInfo = proxyServerRef.GetMethod("LocationFromCurrentWorldUnlocked");
            _getLocationIdFromNameMethodInfo = proxyServerRef.GetMethod("GetLocationIdFromName");
            _getItemNameFromIdMethodInfo = proxyServerRef.GetMethod("GetItemNameFromId");
            _getPlayerAliasMethodInfo = proxyServerRef.GetMethod("GetPlayerAlias");
            _setGameCompletedMethodInfo = proxyServerRef.GetMethod("SetGameCompleted");
            _heartbeatMethodInfo = proxyServerRef.GetMethod("Heartbeat");
            _disconnectMethodInfo = proxyServerRef.GetMethod("Disconnect");
        }

        private object _createNewArchipelagoProxy(Type proxyServerRef, string hostUrl)
        {
            return proxyServerRef.GetConstructor(new Type[] { typeof(string) }).Invoke(new object[] { hostUrl });
        }

        private void _hookUpEvents(Type proxyServerRef)
        {
            // Events for data sent to us
            proxyServerRef.GetMethod("AddConnectedToServerEvent").Invoke(_proxyServer, new object[] { (Action)ConnnectedToServer });
            proxyServerRef.GetMethod("AddRaftItemUnlockedForCurrentWorldEvent").Invoke(_proxyServer, new object[] { (Action<int, int>)RaftItemUnLockedForCurrentWorld });
            proxyServerRef.GetMethod("AddPrintMessageEvent").Invoke(_proxyServer, new object[] { (Action<string>)PrintMessage });
            proxyServerRef.GetMethod("AddDebugMessageEvent").Invoke(_proxyServer, new object[] { (Action<string>)DebugMessage });
        }

        private void _connectToArchipelago(Type proxyServerRef, string username, string password)
        {
            try
            {
                var proxyServerStartMethodInfo = proxyServerRef.GetMethod("Connect", new Type[] { typeof(string), typeof(string) });
                proxyServerStartMethodInfo.Invoke(_proxyServer, new object[] { username, password });
            }
            catch (Exception e)
            {
                PrintMessage("Error while connecting to server.");
                Debug.LogError(e);
                throw e;
            }
        }

        private void ConnnectedToServer()
        {
        }

        private void PrintMessage(string msg)
        {
            try
            {
                ComponentManager<ChatManager>.Value.HandleChatMessageInput(msg, CommonUtils.GetFakeSteamIDForArchipelagoPlayerId(0));
            }
            catch (Exception)
            {
                Debug.Log(msg);
            }
        }

        private void DebugMessage(string msg)
        {
            if (shouldPrintDebugMessages)
            {
                Debug.Log(msg);
            }
        }

        private void RaftItemUnLockedForCurrentWorld(int itemId, int player)
        {
            var sentItemName = GetItemNameFromId(itemId);
            if (!_unlockResourcePack(itemId, sentItemName, player) && !_unlockProgressive(itemId, sentItemName, player) && _unlockItem(itemId, sentItemName, player) == UnlockResult.NotFound)
            {
                Debug.LogError($"Unable to find {sentItemName} ({itemId})");
            }
        }

        private bool _unlockResourcePack(int itemId, string sentItemName, int player)
        {
            if (sentItemName.StartsWith(ResourcePackIdentifier))
            {
                if (_alreadyReceivedItemIds.AddUniqueOnly(itemId))
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
        private bool _unlockProgressive(int itemId, string progressiveName, int fromPlayerId)
        {
            if (_progressiveLevels.ContainsKey(progressiveName) && ComponentManager<ExternalData>.Value.ProgressiveTechnologyMappings.ContainsKey(progressiveName))
            {
                if (++_progressiveLevels[progressiveName] < ComponentManager<ExternalData>.Value.ProgressiveTechnologyMappings[progressiveName].Length)
                {
                    bool unlockedAnyItem = false;
                    foreach (var item in ComponentManager<ExternalData>.Value.ProgressiveTechnologyMappings[progressiveName][_progressiveLevels[progressiveName]])
                    {
                        var itemResult = _unlockItem(itemId, item, fromPlayerId, false);
                        if (itemResult == UnlockResult.NotFound)
                        {
                            Debug.LogError($"Unable to find {item} ({GetPlayerAlias(fromPlayerId)})");
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
                else
                {
                    Debug.LogWarning($"{progressiveName} received, but all items already given");
                }
                return true;
            }
            else
            {
                return false;
            }
        }

        private UnlockResult _unlockItem(int itemId, string itemName, int fromPlayerId, bool showNotification = true)
        {
            var result = _unlockRecipe(itemId, itemName, fromPlayerId, showNotification);
            if (result == UnlockResult.NotFound)
            {
                result = _unlockNote(itemName, showNotification);
            }
            return result;
        }

        private UnlockResult _unlockRecipe(int itemId, string itemName, int fromPlayerId, bool showNotification)
        {
            var foundItem = ComponentManager<CraftingMenu>.Value.AllRecipes.Find(itm => itm.settings_Inventory.DisplayName == itemName);
            if (foundItem != null)
            {
                foundItem.settings_recipe.Learned = true;
                if (_alreadyReceivedItemIds.AddUniqueOnly(itemId))
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

        private void _sendResearchNotification(string displayName, int playerId)
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
                PrintMessage($"Received {displayName} from {GetPlayerAlias(playerId)}");
            }
        }

        private UnlockResult _unlockNote(string noteName, bool showNotification)
        {
            if (ComponentManager<ExternalData>.Value.FriendlyItemNameToUniqueNameMappings.TryGetValue(noteName, out string uniqueNoteName))
            {
                var notebook = ComponentManager<NoteBook>.Value;
                var nbNetwork = (Semih_Network)typeof(NoteBook).GetField("network", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(notebook);
                foreach (var nbNote in nbNetwork.GetLocalPlayer().NoteBookUI.GetAllNotes())
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

        private enum UnlockResult
        {
            NotFound = 1,
            AlreadyUnlocked = 2,
            NewlyUnlocked = 3
        }
    }
}
