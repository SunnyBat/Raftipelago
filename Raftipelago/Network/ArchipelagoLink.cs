using Raftipelago.Data;
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
            // TODO Send Sync packet to receive list of items for this world whenever (re)connecting to server
            // TODO Send list of locations checked whenever (re)connecting to server
            // TODO Parse ReceivedItems packet and add to list when update received
        }

        public void LocationUnlocked(Item_Base locationDefaultRaftItem)
        {
            var locationId = ComponentManager<ItemMapping>.Value.getArchipelagoLocationId(locationDefaultRaftItem.UniqueIndex);
            if (!_allCheckedLocations.Contains(locationId))
            {
                _allCheckedLocations.Add(locationId);
                _UpdateLocationsChecked();
            }
        }

        public void LocationUnlocked(int locationDefaultRaftItemUniqueIndex)
        {
            var raftItem = ComponentManager<CraftingMenu>.Value.AllRecipes.Find(item => item.UniqueIndex == locationDefaultRaftItemUniqueIndex);
            LocationUnlocked(raftItem);
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

        // TODO this should actually be a NetworkItem array, but we don't have models atm :(
        // We'll boil that down to a List<int> or int[] anways, so this is fine
        private void _updateReceivedItems(IEnumerable<int> archipelagoIds)
        {
            var craftingManager = ComponentManager<CraftingMenu>.Value;
            foreach (var archipelagoId in archipelagoIds)
            {
                // TODO Optimize AllRecipes search (probably shove archipelagoIds->Item into map)
                var raftItemIndex = ComponentManager<ItemMapping>.Value.getRaftUniqueIndex(archipelagoId);
                var raftItem = craftingManager.AllRecipes.Find(item => item.UniqueIndex == raftItemIndex);
                raftItem.settings_recipe.Learned = true;
                craftingManager.ReselectCategory(); // TODO Check to see if this actually refreshes the inventory/crafting UI
            }
            // TODO Set Learned to false for items not in archipelagoIds list if not default and not learned?
        }

        public void CloseSession()
        {
            //_session.Disconnect();
        }
    }
}
