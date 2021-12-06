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
        private const string LibraryFileName = "ArchipelagoProxy.dll";
        private readonly string EmbeddedFilePath = Path.Combine("Data", LibraryFileName);

        private string proxyServerDirectory;
        private string archipelagoProxyFile;

        private object _proxyServer;
        private MethodInfo _sendMessage;
        private string _disconnectMessageType;

        private MethodInfo _setPlayerIsInWorldMethodInfo;
        private MethodInfo _sendChatMessageMethodInfo;
        private MethodInfo _locationFromCurrentWorldUnlockedMethodInfo;
        public ProxyServerDIOnly()
        {
            _initDllData();
            _copyDllIfNecessary();
        }

        public void Connect(string URL, string username, string password)
        {
            var assemblyReference = Assembly.LoadFrom(archipelagoProxyFile);
            var proxyServerRef = assemblyReference.GetType(ArchipelagoProxyClassNamespaceIdentifier);
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
            proxyServerDirectory = Path.Combine(appDataDirectory, AppDataFolderName); // TODO Change to Path.Format or whatever instead of using / directly
            archipelagoProxyFile = Path.Combine(proxyServerDirectory, LibraryFileName);
        }

        private void _copyDllIfNecessary()
        {
            if (!Directory.Exists(proxyServerDirectory))
            {
                Directory.CreateDirectory(proxyServerDirectory);
            }
            var assemblyData = ComponentManager<EmbeddedFileUtils>.Value.ReadRawFile(EmbeddedFilePath);
            try
            {
                File.WriteAllBytes(archipelagoProxyFile, assemblyData);
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
