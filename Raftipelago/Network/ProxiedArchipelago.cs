using Raftipelago.Network.Behaviors;
using Raftipelago.Data;
using Steamworks;
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

        private AppDomain _appDomain;
        private Type _proxyServerType;
        private object _proxyServer;

        private MethodInfo _isSuccessfullyConnectedMethodInfo;
        private MethodInfo _irreversablyDestroyMethodInfo;
        private MethodInfo _setIsPlayerInWorldMethodInfo;
        private MethodInfo _sendChatMessageMethodInfo;
        private MethodInfo _locationFromCurrentWorldUnlockedMethodInfo;
        private MethodInfo _getLocationIdFromNameMethodInfo;
        private MethodInfo _getItemNameFromIdMethodInfo;
        private MethodInfo _getLastLoadedSlotDataMethodInfo;
        private MethodInfo _getPlayerAliasMethodInfo;
        private MethodInfo _setGameCompletedMethodInfo;
        private MethodInfo _requeueAllItemsMethodInfo;
        private MethodInfo _heartbeatMethodInfo;
        private MethodInfo _disconnectMethodInfo;

        private bool shouldPrintDebugMessages = false;
        private bool hasLoadedRaftWorldBefore = false;
        public ProxiedArchipelago()
        {
            _initAppDomain();
            _proxyServerType = ComponentManager<AssemblyManager>.Value.GetAssembly(AssemblyManager.ArchipelagoProxyAssembly).GetType(ArchipelagoProxyClassNamespaceIdentifier);
            _initMethodInfo(_proxyServerType);
        }

        public void onUnload()
        {
            _unloadAppDomain();
        }

        public void Connect(string URL, string username, string password)
        {
            if (IsSuccessfullyConnected())
            {
                throw new InvalidOperationException("Already connected to server");
            }
            else
            {
                Debug.Log("Connecting");
                _resetForNextLoad();
                _proxyServer = _appDomain.CreateInstanceAndUnwrap("ArchipelagoProxy", ArchipelagoProxyClassNamespaceIdentifier, false, BindingFlags.Default, null, new object[] { URL }, null, null);
                _hookUpEvents();
                _connectToArchipelago(username, password);
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
                    hasLoadedRaftWorldBefore = true;
                    var locationList = new List<string>();
                    locationList.AddRange(ComponentManager<Inventory_ResearchTable>.Value.GetMenuItems()
                        .FindAll(itm => itm.Learned && CommonUtils.IsValidResearchTableItem(itm.GetItem()))
                        .Select(itm => CommonUtils.TryGetOrKey(ComponentManager<ExternalData>.Value.UniqueLocationNameToFriendlyNameMappings, itm.GetItem().UniqueName)));
                    WorldManager.AllLandmarks.ForEach(landmark =>
                    {
                        foreach (var landmarkItem in landmark.landmarkItems)
                        {
                            if (CommonUtils.IsNoteOrBlueprint(landmarkItem) && !landmarkItem.gameObject.activeSelf
                                && ComponentManager<ExternalData>.Value.UniqueLocationNameToFriendlyNameMappings.TryGetValue(landmarkItem.name, out string friendlyName))
                            {
                                locationList.Add(friendlyName);
                            }
                            else if (ComponentManager<ExternalData>.Value.QuestLocations.ContainsKey(landmarkItem.name)) // Friendly names and non-LandmarkItems will be ignored. This is fine.
                            {
                                var shouldSendLocation = false;
                                // Will need to add additional processing if new QuestInteractableComponents show up
                                foreach (var objOnOff in landmarkItem.GetComponents<QuestInteractableComponent_ObjectOnOff>())
                                {
                                    // Grab the specific required component (eg a toolbox) and hide it
                                    var dataComponents = (List<QuestInteractable_ComponentData_ObjectOnOff>)objOnOff.GetType().GetField("dataComponents").GetValue(objOnOff);
                                    foreach (var dataComp in dataComponents)
                                    {
                                        foreach (var gameObject in dataComp.gameObjects)
                                        {
                                            shouldSendLocation |= !gameObject.activeSelf; // If disabled, items have been turned in
                                        }
                                    }
                                }
                                if (shouldSendLocation && ComponentManager<ExternalData>.Value.UniqueLocationNameToFriendlyNameMappings.TryGetValue(landmarkItem.name, out string specialLocationName))
                                {
                                    locationList.Add(specialLocationName);
                                }
                            }
                            else if (CommonUtils.IsCharacter(landmarkItem))
                            {
                                var characterLandmark = (LandmarkItem_CharacterUnlock)landmarkItem;
                                var characterUnlock = (CharacterUnlock)typeof(LandmarkItem_CharacterUnlock).GetField("characterUnlock", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(characterLandmark);
                                var characterModel = (GameObject)typeof(CharacterUnlock).GetField("characterModel", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(characterUnlock);
                                if (!characterModel.activeSelf)
                                {
                                    var characterData = (SO_Character)typeof(CharacterUnlock).GetField("characterToUnlock", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(characterUnlock);
                                    if (ComponentManager<ExternalData>.Value.UniqueLocationNameToFriendlyNameMappings.TryGetValue(characterData.name, out string characterFriendlyName))
                                    {
                                        locationList.Add(characterFriendlyName);
                                    }
                                    else
                                    {
                                        Debug.LogError("Unknown character: " + landmarkItem.name);
                                    }
                                }
                            }
                        }
                    });
                    if (CommonUtils.HasFinishedRelayStationQuest())
                    {
                        locationList.Add("Relay Station quest");
                    }
                    if (CommonUtils.HasCompletedTheGame())
                    {
                        SetGameCompleted(true);
                    }
                    LocationUnlocked(locationList.ToArray());
                }
                else
                {
                    _resetForNextLoad(true);
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
                try
                {
                    return (string)_getItemNameFromIdMethodInfo.Invoke(_proxyServer, new object[] { itemId });
                }
                catch (Exception)
                {
                    return null;
                }
            }
            else
            {
                if (!Raft_Network.IsHost)
                {
                    return ComponentManager<ArchipelagoDataManager>.Value.ItemIdToName.GetValueOrDefault(itemId, null);
                }
                return null;
            }
        }

        public Dictionary<string, object> GetLastLoadedSlotData()
        {
            if (IsSuccessfullyConnected())
            {
                return (Dictionary<string, object>)_getLastLoadedSlotDataMethodInfo.Invoke(_proxyServer, new object[0]);
            }
            else
            {
                return null;
            }
        }

        public string GetPlayerAlias(int playerId)
        {
            if (IsSuccessfullyConnected())
            {
                try
                {
                    return (string)_getPlayerAliasMethodInfo.Invoke(_proxyServer, new object[] { playerId });
                }
                catch (Exception)
                {
                    return null;
                }
            }
            else
            {
                if (!Raft_Network.IsHost)
                {
                    return ComponentManager<ArchipelagoDataManager>.Value.PlayerIdToName.GetValueOrDefault(playerId, null);
                }
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
                }
                catch (Exception e)
                {
                    Debug.LogError(e);
                }
            }
        }

        public void ToggleDebug()
        {
            shouldPrintDebugMessages = !shouldPrintDebugMessages;
        }

        public Dictionary<int, string> GetAllItemIds()
        {
            var ret = new Dictionary<int, string>();
            bool wasSuccessful = true;
            int currentIndex = 47001; // TODO Constant
            do
            {
                try
                {
                    var itemName = GetItemNameFromId(currentIndex);
                    if (!string.IsNullOrWhiteSpace(itemName))
                    {
                        ret.Add(currentIndex, GetItemNameFromId(currentIndex));
                    }
                    else
                    {
                        wasSuccessful = false;
                    }
                }
                catch (Exception)
                {
                    wasSuccessful = false;
                }
                currentIndex++;
            } while (wasSuccessful);
            return ret;
        }

        public Dictionary<int, string> GetAllPlayerIds()
        {
            var ret = new Dictionary<int, string>();
            bool wasSuccessful = true;
            int currentIndex = 1; // 0 = Server
            do
            {
                try
                {
                    var playerAlias = GetPlayerAlias(currentIndex);
                    if (!string.IsNullOrWhiteSpace(playerAlias))
                    {
                        ret.Add(currentIndex, playerAlias);
                    }
                    else
                    {
                        wasSuccessful = false;
                    }
                }
                catch (Exception)
                {
                    wasSuccessful = false;
                }
                currentIndex++;
            } while (wasSuccessful);
            return ret;
        }

        private void _initAppDomain()
        {
            string appDataDirectory = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            var proxyServerDirectory = Path.Combine(appDataDirectory, RaftipelagoThree.AppDataFolderName);
            var ads = new AppDomainSetup();
            ads.PrivateBinPath = proxyServerDirectory;
            _appDomain = AppDomain.CreateDomain(Guid.NewGuid().ToString(), new System.Security.Policy.Evidence(), ads);
        }

        private void _resetForNextLoad(bool isReload = false)
        {
            // Reset progressives on each connect since we'll be rewriting it all
            ComponentManager<ItemTracker>.Value.ResetProgressives();
            if (isReload && hasLoadedRaftWorldBefore && _proxyServer != null) // Don't requeue if never loaded world before -- we'll just duplicate for no reason
            {
                _requeueAllItemsMethodInfo.Invoke(_proxyServer, null);
            }
            else if (!isReload && _proxyServer != null)
            {
                _irreversablyDestroyMethodInfo.Invoke(_proxyServer, null);
            }
        }

        private void _unloadAppDomain()
        {
            if (_appDomain != null)
            {
                AppDomain.Unload(_appDomain);
                _appDomain = null;
            }
        }

        private void _initMethodInfo(Type proxyServerRef)
        {
            // Events for data that we send to proxy
            // If a method is overloaded, it needs specific types. Otherwise, no typing is needed.
            _isSuccessfullyConnectedMethodInfo = proxyServerRef.GetMethod("IsSuccessfullyConnected");
            _irreversablyDestroyMethodInfo = proxyServerRef.GetMethod("IrreversablyDestroy");
            _setIsPlayerInWorldMethodInfo = proxyServerRef.GetMethod("SetIsPlayerInWorld");
            _sendChatMessageMethodInfo = proxyServerRef.GetMethod("SendChatMessage");
            _locationFromCurrentWorldUnlockedMethodInfo = proxyServerRef.GetMethod("LocationFromCurrentWorldUnlocked");
            _getLocationIdFromNameMethodInfo = proxyServerRef.GetMethod("GetLocationIdFromName");
            _getItemNameFromIdMethodInfo = proxyServerRef.GetMethod("GetItemNameFromId");
            _getLastLoadedSlotDataMethodInfo = proxyServerRef.GetMethod("GetLastLoadedSlotData");
            _getPlayerAliasMethodInfo = proxyServerRef.GetMethod("GetPlayerAlias");
            _setGameCompletedMethodInfo = proxyServerRef.GetMethod("SetGameCompleted");
            _requeueAllItemsMethodInfo = proxyServerRef.GetMethod("RequeueAllItems");
            _heartbeatMethodInfo = proxyServerRef.GetMethod("Heartbeat");
            _disconnectMethodInfo = proxyServerRef.GetMethod("Disconnect");
        }

        private void _hookUpEvents()
        {
            // Events for data sent to us
            _proxyServerType.GetMethod("AddConnectedToServerEvent").Invoke(_proxyServer, new object[] { GetNewEventObject((Action)ConnnectedToServer, "ActionHandler") });
            _proxyServerType.GetMethod("AddRaftItemUnlockedForCurrentWorldEvent")
                .Invoke(_proxyServer, new object[] { GetNewEventObject((Action<int, int, int>)ComponentManager<ItemTracker>.Value.RaftItemUnlockedForCurrentWorld, "TripleArgumentActionHandler`3", typeof(int), typeof(int), typeof(int)) });
            _proxyServerType.GetMethod("AddPrintMessageEvent")
                .Invoke(_proxyServer, new object[] { GetNewEventObject((Action<string>)PrintMessage, "SingleArgumentActionHandler`1", typeof(string)) });
            _proxyServerType.GetMethod("AddDebugMessageEvent")
                .Invoke(_proxyServer, new object[] { GetNewEventObject((Action<string>)DebugMessage, "SingleArgumentActionHandler`1", typeof(string)) });
        }

        private object GetNewEventObject<T>(T arg, string typeName, params Type[] genericTypes)
        {
            var raftipelagoTypes = ComponentManager<AssemblyManager>.Value.GetAssembly(AssemblyManager.RaftipelagoTypesAssembly);
            var constructorArgTypes = new Type[] { typeof(T) };
            var typeInfo = raftipelagoTypes.GetType("RaftipelagoTypes." + typeName);
            if (genericTypes.Length > 0)
            {
                return typeInfo.MakeGenericType(genericTypes).GetConstructor(constructorArgTypes).Invoke(new object[] { arg });
            }
            else
            {
                return typeInfo.GetConstructor(constructorArgTypes).Invoke(new object[] { arg });
            }
        }

        private void _connectToArchipelago(string username, string password)
        {
            try
            {
                var proxyServerStartMethodInfo = _proxyServerType.GetMethod("Connect", new Type[] { typeof(string), typeof(string) });
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
            BehaviourHelper.SendArchipelagoData();
        }

        private void PrintMessage(string msg)
        {
            if (!Raft_Network.InMenuScene)
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
            else
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
    }
}
