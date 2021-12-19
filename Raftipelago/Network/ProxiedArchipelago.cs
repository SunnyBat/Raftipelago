using Raftipelago.Data;
using System;
using System.IO;
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

        private MethodInfo _setPlayerIsInWorldMethodInfo;
        private MethodInfo _sendChatMessageMethodInfo;
        private MethodInfo _locationFromCurrentWorldUnlockedMethodInfo;
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

        public void SetIsInWorld(bool inWorld)
        {
            _setPlayerIsInWorldMethodInfo.Invoke(_proxyServer, new object[] { inWorld });
        }

        public void SendChatMessage(string message)
        {
            _sendChatMessageMethodInfo.Invoke(_proxyServer, new object[] { message });
        }

        public void LocationUnlocked(params int[] locationIds)
        {
            _locationFromCurrentWorldUnlockedMethodInfo.Invoke(_proxyServer, new object[] { locationIds });
        }

        public void Disconnect()
        {
            _disconnectMethodInfo.Invoke(_proxyServer, new object[] { });
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
                if (_proxyAssembly == null) // Only take first one
                {
                    Debug.Log("Loading proxy assembly " + outputFilePath);
                    _proxyAssembly = Assembly.LoadFrom(outputFilePath);
                }
                else
                {
                    Debug.Log("Loading library assembly " + outputFilePath);
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
            _attachEvent(proxyServerRef, "PrintMessage", (message) => {
                Debug.Log(message);
            });
            _attachEvent(proxyServerRef, "RaftItemUnlockedForCurrentWorld", (int itemId, string player) => {
                Debug.Log($"{player} found {itemId}"); // TODO Make item available
            });

            // Events for data that we send to proxy
            _setPlayerIsInWorldMethodInfo = proxyServerRef.GetMethod("SetPlayerIsInWorld");
            _sendChatMessageMethodInfo = proxyServerRef.GetMethod("SendChatMessage");
            _locationFromCurrentWorldUnlockedMethodInfo = proxyServerRef.GetMethod("LocationFromCurrentWorldUnlocked");
            _disconnectMethodInfo = proxyServerRef.GetMethod("Disconnect");
        }

        private void _connectToArchipelago(Type proxyServerRef, string username, string password)
        {
            try
            {
                Debug.Log("CTA_1");
                var proxyServerStartMethodInfo = proxyServerRef.GetMethod("Connect", new Type[] { typeof(string), typeof(string) });
                Debug.Log("CTA_2");
                proxyServerStartMethodInfo.Invoke(_proxyServer, new object[] { username, password });
                Debug.Log("CTA_3");
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                throw e;
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
