using Raftipelago.Data;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace Raftipelago.Network
{
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

        private MethodInfo _setIsPlayerInWorldMethodInfo;
        private MethodInfo _sendChatMessageMethodInfo;
        private MethodInfo _locationFromCurrentWorldUnlockedMethodInfo;
        private MethodInfo _getLocationIdFromNameMethodInfo;
        private MethodInfo _getItemNameFromIdMethodInfo;
        private MethodInfo _heartbeatMethodInfo;
        private MethodInfo _disconnectMethodInfo;
        public ProxiedArchipelago()
        {
            _initDllData();
        }

        public void Connect(string URL, string username, string password)
        {
            var proxyServerRef = _proxyAssembly.GetType(ArchipelagoProxyClassNamespaceIdentifier);
            _proxyServer = _createNewArchipelagoProxy(proxyServerRef, URL);
            _hookUpEvents(proxyServerRef);
            _connectToArchipelago(proxyServerRef, username, password);
        }

        public void Heartbeat()
        {
            _heartbeatMethodInfo.Invoke(_proxyServer, null);
        }

        public void SetIsInWorld(bool inWorld)
        {
            Debug.Log(_setIsPlayerInWorldMethodInfo);
            _setIsPlayerInWorldMethodInfo.Invoke(_proxyServer, new object[] { inWorld });
        }

        public void SendChatMessage(string message)
        {
            _sendChatMessageMethodInfo.Invoke(_proxyServer, new object[] { message });
        }

        public void LocationUnlocked(params int[] locationIds)
        {
            _locationFromCurrentWorldUnlockedMethodInfo.Invoke(_proxyServer, new object[] { locationIds });
        }

        public void LocationUnlocked(params string[] locationNames)
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

        public string GetItemNameFromId(int itemId)
        {
            return (string)_getItemNameFromIdMethodInfo.Invoke(_proxyServer, new object[] { itemId });
        }

        public void Disconnect()
        {
            try
            {
                _disconnectMethodInfo.Invoke(_proxyServer, new object[] { });
            }
            catch (Exception e)
            {
                // At this point all we can do is clean up everything else -- don't throw exception up or we'll fail to unload everything
                Debug.LogError(e);
            }
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
                _copyDllIfNecessary(embeddedFilePath, outputFilePath);
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
            var assemblyData = ComponentManager<EmbeddedFileUtils>.Value.ReadRawFile(embeddedFilePath);
            try
            {
                File.WriteAllBytes(outputFilePath, assemblyData);
            }
            catch (Exception)
            {
                // TODO Check exception type and print if error occurs
            }
        }

        private object _createNewArchipelagoProxy(Type proxyServerRef, string hostUrl)
        {
            return proxyServerRef.GetConstructor(new Type[] { typeof(string) }).Invoke(new object[] { hostUrl });
        }

        private void _hookUpEvents(Type proxyServerRef)
        {
            // Events for data sent to us
            _attachEvent(proxyServerRef, "PrintMessage", PrintMessage);
            _attachEvent(proxyServerRef, "RaftItemUnlockedForCurrentWorld", RaftItemLockedForCurrentWorld);

            // Events for data that we send to proxy
            // If a method is overloaded, it needs specific types. Otherwise, no typing is needed.
            _setIsPlayerInWorldMethodInfo = proxyServerRef.GetMethod("SetIsPlayerInWorld");
            _sendChatMessageMethodInfo = proxyServerRef.GetMethod("SendChatMessage");
            _locationFromCurrentWorldUnlockedMethodInfo = proxyServerRef.GetMethod("LocationFromCurrentWorldUnlocked");
            _getLocationIdFromNameMethodInfo = proxyServerRef.GetMethod("GetLocationIdFromName");
            _getItemNameFromIdMethodInfo = proxyServerRef.GetMethod("GetItemNameFromId");
            _heartbeatMethodInfo = proxyServerRef.GetMethod("Heartbeat");
            _disconnectMethodInfo = proxyServerRef.GetMethod("Disconnect");
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
                Debug.Log(e);
                throw e;
            }
        }

        private void PrintMessage(string msg)
        {
            Debug.Log(msg);
        }

        private void RaftItemLockedForCurrentWorld(int itemId, string player)
        {
            var sentItemName = GetItemNameFromId(itemId);
            // TODO Verify that these aren't overwritten when a world is loaded
            var foundItem = ComponentManager<CraftingMenu>.Value.AllRecipes.Find(itm => itm.UniqueName == sentItemName);
            if (foundItem != null)
            {
                // TODO How to get SteamID of remote player or otherwise display different player name
                //(ComponentManager<NotificationManager>.Value.ShowNotification("Research") as Notification_Research).researchInfoQue.Enqueue(
                //    new Notification_Research_Info(foundItem.settings_Inventory.DisplayName, ___localPlayer.steamID, ComponentManager<SpriteManager>.Value.GetArchipelagoSprite()));
                Debug.Log("Unlocking " + foundItem.UniqueName);
                foundItem.settings_recipe.Learned = true;
            }
            else
            {
                Debug.Log($"Unable to find {sentItemName} ({itemId})");
            }
        }

        private void _attachEvent(Type proxyServerRef, string eventName, Action<string> callback)
        {
            var eventRef = proxyServerRef.GetEvent(eventName);
            eventRef.AddEventHandler(_proxyServer, callback);
        }

        private void _attachEvent(Type proxyServerRef, string eventName, Action<int, string> callback)
        {
            var eventRef = proxyServerRef.GetEvent(eventName);
            eventRef.AddEventHandler(_proxyServer, callback);
        }
    }
}
