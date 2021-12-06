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
        private const string AppDataFolderName = "Raftipelago";
        private const string LibraryFileName = "ArchipelagoProxy.dll";
        private readonly string EmbeddedFilePath = Path.Combine("Data", LibraryFileName);

        private object _proxyServer;
        private MethodInfo _sendMessage;
        private string _disconnectMessageType;
        public ProxyServerDIOnly()
        {
            string appDataDirectory = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            string proxyServerDirectory = Path.Combine(appDataDirectory, AppDataFolderName); // TODO Change to Path.Format or whatever instead of using / directly
            string archipelagoProxyFile = Path.Combine(proxyServerDirectory, LibraryFileName);
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
            var assemblyReference = Assembly.LoadFrom(archipelagoProxyFile);
            var constantsRef = assemblyReference.GetType("ArchipelagoProxy.Constants");
            _disconnectMessageType = (string) constantsRef.GetField("StopConnectionMessageType").GetRawConstantValue();
            Debug.Log(_disconnectMessageType);
            var proxyServerRef = assemblyReference.GetType("ArchipelagoProxy.ProxyServer");
            _proxyServer = proxyServerRef.GetConstructor(new Type[] { typeof(int) }).Invoke(new object[] { 6942 });
            Debug.Log($"_proxyServer =  {_proxyServer != null}");
            Action<string, string> packetReceivedCallback = (messageType, msg) => {
                Debug.Log($"Packet received ({messageType}): {msg}");
                // TODO Handle packet received
            };
            proxyServerRef.GetMethod("AddPacketReceivedEvent").Invoke(_proxyServer, new object[] { packetReceivedCallback });
            _sendMessage = proxyServerRef.GetMethod("SendPacket");

            var proxyServerStartMethodInfo = proxyServerRef.GetMethod("InteractUntilConnectionClosed", new Type[] {});
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
