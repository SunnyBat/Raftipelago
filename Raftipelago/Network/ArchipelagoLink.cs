//using Archipelago.MultiClient.Net;
//using Archipelago.MultiClient.Net.Packets;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Raftipelago.Network
{
    public class ArchipelagoLink
    {
        //private readonly ArchipelagoSession _session;
        private readonly List<int> _allCheckedLocations = new List<int>();
        public ArchipelagoLink()
        {
            Debug.Log("ArchipelagoLink debug active");
        }

        public ArchipelagoLink(string urlToHost, string username, string password)
        {
            //_session = new ArchipelagoSession(urlToHost);
            //_session.Connect(); // TODO How2connect async (it doesn't return a Task wtf)
            //var connectPacket = new ConnectPacket();
            //connectPacket.Name = username;
            //connectPacket.Password = password;
            //connectPacket.Uuid = new Guid().ToString(); // TODO Unique ID per session, per user, per world?
            //connectPacket.Game = "Raft";
            //connectPacket.Version = new Version(0, 2, 0);
            //_session.SendPacket(connectPacket);
            //_session.PacketReceived += packet => {
            //    Debug.Log($"Packet received: {packet} ({packet.PacketType})");
            //};

            // TODO Send ClientReady packet after connected
            // TODO Send Sync packet to receive list of items for this world
            // TODO Parse ReceivedItems packet and add to list
        }

        public void ItemUnlocked(Item_Base raftItem)
        {
            var locationId = raftItem.UniqueIndex; // TODO Change to Archipelago location ID
            if (!_allCheckedLocations.Contains(locationId))
            {
                _allCheckedLocations.Add(locationId);
                _UpdateLocationsChecked();
            }
        }

        public void ItemUnlocked(int raftItemUniqueIndex)
        {
            var raftItem = ComponentManager<CraftingMenu>.Value.AllRecipes.Find(item => item.UniqueIndex == raftItemUniqueIndex);
            ItemUnlocked(raftItem);
        }

        private void _UpdateLocationsChecked()
        {
            //if (_session == null)
            //{
            //    return;
            //}

            //var locationListPacket = new LocationChecksPacket();
            //locationListPacket.Locations = checkedLocations;
            //_session.SendPacket(locationListPacket);
        }

        private void _updateReceivedItems(int[] archipelagoIds)
        {
            foreach (var archipelagoId in archipelagoIds)
            {
                // TODO Optimize AllRecipes search (probably shove archipelagoIds->Item into map)
                var raftItemIndex = archipelagoId; // TODO Get Raft UniqueIndex
                var raftItem = ComponentManager<CraftingMenu>.Value.AllRecipes.Find(item => item.UniqueIndex == raftItemIndex);
                raftItem.settings_recipe.Learned = true;
                // TODO Set Learned to false if not default and not learned
            }
        }

        public void CloseSession()
        {
            //_session.Disconnect();
        }
    }
}
