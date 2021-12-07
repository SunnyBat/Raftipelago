using Raftipelago.Data;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading;
using UnityEngine;

namespace Raftipelago
{
    public class ProxyServerDIOnly
    {
        private const string ArchipelagoProxyClassNamespaceIdentifier = "ArchipelagoProxy.ArchipelagoProxy";
        private const string AppDataFolderName = "Raftipelago";
        private const string EmbeddedFileDirectory = "Data";
        /// <summary>
        /// Index 0 is the ArchipelagoProxy DLL that we want to actually run code from
        /// </summary>
        private readonly string[] LibraryFileNames = new string[] { "ArchipelagoProxy.dll", "Archipelago.MultiClient.Net.dll", "websocket-sharp.dll" };

        private Assembly _proxyAssembly;
        private object _proxyServer;

        private MethodInfo _setPlayerIsInWorldMethodInfo;
        private MethodInfo _sendChatMessageMethodInfo;
        private MethodInfo _locationFromCurrentWorldUnlockedMethodInfo;
        public ProxyServerDIOnly()
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

        public void SetIsInWorld(bool inWorld)
        {
            _setPlayerIsInWorldMethodInfo.Invoke(_proxyServer, new object[] { inWorld });
        }

        public void SendChatMessage(string message)
        {
            _sendChatMessageMethodInfo.Invoke(_proxyServer, new object[] { message });
        }

        public void LocationUnlocked(int locationId)
        {
            _locationFromCurrentWorldUnlockedMethodInfo.Invoke(_proxyServer, new object[] { });
        }

        public void Disconnect()
        {
        }

        private void _initDllData()
        {
            string appDataDirectory = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            var proxyServerDirectory = Path.Combine(appDataDirectory, AppDataFolderName); // TODO Change to Path.Format or whatever instead of using / directly
            if (!Directory.Exists(proxyServerDirectory))
            {
                Directory.CreateDirectory(proxyServerDirectory);
            }
            foreach (var fileName in LibraryFileNames)
            {
                var embeddedFilePath = Path.Combine(EmbeddedFileDirectory, fileName);
                var outputFilePath = Path.Combine(proxyServerDirectory, fileName);
                _copyDllIfNecessary(embeddedFilePath, outputFilePath);
                _proxyAssembly = _proxyAssembly ?? Assembly.LoadFrom(outputFilePath); // Only take first one
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
            _attachEvent(proxyServerRef, "ChatMessageReceived", (string message) => {
                Debug.Log(message); // TODO Display to user in friendly way
            });
            _attachEvent(proxyServerRef, "RaftItemUnlockedForCurrentWorld", (int itemId, string player) => {
                Debug.Log($"{player} found {itemId}"); // TODO Make item available
            });

            // Events for data that we send to proxy
            _setPlayerIsInWorldMethodInfo = proxyServerRef.GetEvent("SetPlayerIsInWorld").GetRaiseMethod();
            _sendChatMessageMethodInfo = proxyServerRef.GetEvent("SendChatMessage").GetRaiseMethod();
            _locationFromCurrentWorldUnlockedMethodInfo = proxyServerRef.GetEvent("LocationFromCurrentWorldUnlocked").GetRaiseMethod();
        }

        private void _connectToArchipelago(Type proxyServerRef, string username, string password)
        {
            var proxyServerStartMethodInfo = proxyServerRef.GetMethod("Connect", new Type[] { typeof(string), typeof(string) });
            proxyServerStartMethodInfo.Invoke(_proxyServer, new object[] { username, password });
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
