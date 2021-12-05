using Archipelago.MultiClient.Net;
using Archipelago.MultiClient.Net.Packets;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Raftipelago.Network
{
    public class ArchipelagoLink
    {
        private readonly ArchipelagoSession _session;
        public ArchipelagoLink()
        {
            Debug.Log("ArchipelagoLink debug active");
        }

        public ArchipelagoLink(string urlToHost, string username, string password)
        {
            _session = new ArchipelagoSession(urlToHost);
            _session.Connect(); // TODO How2connect async (it doesn't return a Task wtf)
            var connectPacket = new ConnectPacket();
            connectPacket.Name = username;
            connectPacket.Password = password;
            connectPacket.Uuid = new Guid().ToString(); // TODO Unique ID per session, per user, per world?
            connectPacket.Game = "Raft";
            connectPacket.Version = new Version(0, 2, 0);
            _session.SendPacket(connectPacket);
            _session.PacketReceived += packet => {
                Debug.Log($"Packet received: {packet} ({packet.PacketType})");
            };

            // TODO Send ClientReady packet after connected
            // TODO Send Sync packet to receive list of items for this world
        }

        public void UpdateLocationsChecked(List<int> checkedLocations)
        {
            if (_session == null)
            {
                return;
            }

            var locationListPacket = new LocationChecksPacket();
            locationListPacket.Locations = checkedLocations;
            _session.SendPacket(locationListPacket);
        }

        public void CloseSession()
        {
            _session.Disconnect();
        }
    }
}
