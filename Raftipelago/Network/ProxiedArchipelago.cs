using Newtonsoft.Json;
using Raftipelago.Data;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace Raftipelago.Network
{
    /// <summary>
    /// A communication layer for Archipelago that uses dynamically-loaded libraries.
    /// </summary>
    public class ProxiedArchipelago : IArchipelagoLink
    {
        private const string ArchipelagoProxyClassNamespaceIdentifier = "ArchipelagoProxy.ArchipelagoProxy";
        private const string AppDataFolderName = "Raftipelago";
        private const string EmbeddedFileDirectory = "Data";
        /// <summary>
        /// Index 0 is the ArchipelagoProxy DLL that we want to actually run code from
        /// </summary>
        private readonly string[] LibraryFileNames = new string[] { "ArchipelagoProxy.dll", "Newtonsoft.Json.dll", "websocket-sharp.dll", "Archipelago.MultiClient.Net.dll" };

        private Assembly _proxyAssembly;
        private object _proxyServer;

        private MethodInfo _isSuccessfullyConnectedMethodInfo;
        private MethodInfo _setIsPlayerInWorldMethodInfo;
        private MethodInfo _sendChatMessageMethodInfo;
        private MethodInfo _locationFromCurrentWorldUnlockedMethodInfo;
        private MethodInfo _getLocationIdFromNameMethodInfo; 
        private MethodInfo _getItemNameFromIdMethodInfo;
        private MethodInfo _getPlayerAliasMethodInfo;
        private MethodInfo _heartbeatMethodInfo;
        private MethodInfo _disconnectMethodInfo;

        private bool shouldPrintDebugMessages = false;
        public ProxiedArchipelago()
        {
            _initDllData();
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
                _setIsPlayerInWorldMethodInfo.Invoke(_proxyServer, new object[] { inWorld, false });
                if (inWorld)
                {
                    var locationList = new List<string>();
                    locationList.AddRange(ComponentManager<Inventory_ResearchTable>.Value.GetMenuItems()
                        .FindAll(itm => itm.Learned)
                        .Select(itm => itm.GetItem().UniqueName));
                    WorldManager.AllLandmarks.ForEach(landmark =>
                    {
                        // TODO Should I filter by story island?
                        // Can get IDs from SceneLoader debug prints.
                        // In theory notes will only be on story islands, so non-story islands will be fine.
                        foreach (var landmarkItem in landmark.landmarkItems)
                        {
                            if (CommonUtils.IsNoteOrBlueprint(landmarkItem) && !landmarkItem.gameObject.activeSelf)
                            {
                                locationList.Add(landmarkItem.name);
                            }
                        }
                    });
                    LocationUnlocked(locationList.ToArray());
                }
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
                    Debug.Log("Unlocking locations " + string.Join(",", locationIds));
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

        private void _initDllData()
        {
            string appDataDirectory = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            var proxyServerDirectory = Path.Combine(appDataDirectory, AppDataFolderName);
            if (!Directory.Exists(proxyServerDirectory))
            {
                Directory.CreateDirectory(proxyServerDirectory);
            }
            foreach (var fileName in LibraryFileNames)
            {
                var embeddedFilePath = Path.Combine(EmbeddedFileDirectory, fileName);
                var outputFilePath = Path.Combine(proxyServerDirectory, fileName);
                //_copyDllIfNecessary(embeddedFilePath, outputFilePath);
                if (_proxyAssembly == null) // Only take first one
                {
                    _proxyAssembly = Assembly.LoadFrom(outputFilePath);
                }
                else
                {
                    Assembly.LoadFrom(outputFilePath);
                }
            }
        }

        private void _copyDllIfNecessary(string embeddedFilePath, string outputFilePath)
        {
            try
            {
                var assemblyData = ComponentManager<EmbeddedFileUtils>.Value.ReadRawFile(embeddedFilePath);
                File.WriteAllBytes(outputFilePath, assemblyData);
            }
            catch (Exception)
            {
                // TODO Check exception type and print if unexpected error occurs
            }
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
                Debug.Log(e);
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
                ChatManager.LocalDebugChatMessage(msg);
            }
            catch (Exception)
            {
                Debug.Log(msg);
            }
        }

        private void DebugMessage(string msg)
        {
            Debug.Log(msg);
        }

        private void RaftItemUnLockedForCurrentWorld(int itemId, int player)
        {
            var sentItemName = GetItemNameFromId(itemId);
            if (!_unlockRecipe(sentItemName, player) && !_unlockNote(sentItemName, player))
            {
                Debug.LogError($"Unable to find {sentItemName} ({itemId})");
            }
        }

        private bool _unlockRecipe(string itemName, int fromPlayerId)
        {
            var foundItem = ComponentManager<CraftingMenu>.Value.AllRecipes.Find(itm => itm.UniqueName == itemName);
            if (foundItem != null)
            {
                if (!foundItem.settings_recipe.Learned)
                {
                    // TODO How to get SteamID of remote player or otherwise display different player name
                    try
                    {
                        (ComponentManager<NotificationManager>.Value.ShowNotification("Research") as Notification_Research).researchInfoQue.Enqueue(
                            new Notification_Research_Info(foundItem.settings_Inventory.DisplayName,
                                CommonUtils.GetFakeSteamIDForArchipelagoPlayerId(fromPlayerId),
                                ComponentManager<SpriteManager>.Value.GetArchipelagoSprite()));
                    }
                    catch (Exception)
                    {
                        PrintMessage($"Received {itemName} from {GetPlayerAlias(fromPlayerId)}");
                    }
                    foundItem.settings_recipe.Learned = true;
                }
                return true;
            }
            return false;
        }

        private bool _unlockNote(string noteName, int fromPlayerId)
        {
            var notebook = ComponentManager<NoteBook>.Value;
            var nbNetwork = (Semih_Network)typeof(NoteBook).GetField("network", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(notebook);
            foreach (var nbNote in nbNetwork.GetLocalPlayer().NoteBookUI.GetAllNotes())
            {
                if (nbNote.name == noteName)
                {
                    notebook.UnlockSpecificNoteWithUniqueNoteIndex(nbNote.noteIndex, true, false);
                    ComponentManager<NotificationManager>.Value.ShowNotification("NoteBookNote");
                    return true;
                }
            }
            return false;
        }
    }
}
