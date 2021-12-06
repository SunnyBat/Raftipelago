using Raftipelago.Data;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace Raftipelago
{
    public class ProxyServerDIOnly
    {
        private const string ProxyServerDirectory = "%APPDATA%/Raftipelago"; // TODO Change to Path.Format or whatever instead of using / directly
        private readonly string ArchipelagoProxyFile = $"{ProxyServerDirectory}/ArchipelagoProxy.dll";

        private object _proxyServer;
        private MethodInfo _sendMessage;
        private string _disconnectMessageType;
        public ProxyServerDIOnly()
        {
            if (!Directory.Exists(ProxyServerDirectory))
            {
                Directory.CreateDirectory(ProxyServerDirectory);
            }
            var assemblyData = ComponentManager<EmbeddedFileUtils>.Value.ReadRawFile("Data/ArchipelagoProxy.dll");
            File.WriteAllBytes(ArchipelagoProxyFile, assemblyData);
            var assemblyReference = Assembly.LoadFrom(ArchipelagoProxyFile);
            var constantsRef = assemblyReference.GetType("ArchipelagoProxy.Constants");
            _disconnectMessageType = (string) constantsRef.GetField("StopConnectionMessageType").GetValue(null);
            var proxyServerRef = assemblyReference.GetType("ArchipelagoProxy.ProxyServer");
            _proxyServer = proxyServerRef.GetConstructor(new Type[] { typeof(int) }).Invoke(new object[] { 6942 });
            Action<string, string> packetReceivedCallback = (messageType, msg) => {
                Debug.Log($"Packet received ({messageType}): {msg}");
                // TODO Handle packet received
            };
            proxyServerRef.GetMethod("AddPacketReceivedEvent").Invoke(_proxyServer, new object[] { packetReceivedCallback });
            _sendMessage = proxyServerRef.GetMethod("");

            var proxyServerStartMethodInfo = proxyServerRef.GetMethod("InteractUntilConnectionClosed");
            Thread t3 = new Thread(() => proxyServerStartMethodInfo.Invoke(_proxyServer, new object[0])); // Mulithread since this blocks forever
            t3.Start();
            // TODO Wait for server to finish starting and verify it's working before returning
        }

        public void sendMessage(string messageType, string message)
        {
            _sendMessage.Invoke(_proxyServer, new object[] { messageType, message });
        }

        public void Disconnect()
        {
            sendMessage(_disconnectMessageType, "");
        }
    }
}
