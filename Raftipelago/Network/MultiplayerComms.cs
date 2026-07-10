using HMLLibrary;
using Raftipelago.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Raftipelago.Network
{
    public class MultiplayerComms
    {
        private Dictionary<long, int> PlayerItemIndeces = new Dictionary<long, int>();

        // Occurs when a network message is recieved on one of the network channels your mod is set to listen to.
        // Return type can be void or bool. If the return type is null you can return true to indicate that your
        // mod handled the message, if the method is void or returns false then the message will continue to be
        // passed to other mods listening to the network channel.
        public bool HandleMessage(object message, Network_UserId from)
        {
            // Host ignores all AP-related packets, as the host is the one initially receiving them
            if (message == null)
            {
                Logger.Warn($"Null message received ({from})");
                return false;
            }
            else if (Raft_Network.IsHost)
            {
                Logger.Debug($"Packet received ({message.GetType().Name})");
                switch (message)
                {
                    case RequestResyncMessage _:
                        // Don't need to validate message length, we have all the data we need so just send it
                        SendAllArchipelagoData(from);
                        return true;
                    case DeathLinkReasonMessage deathLink: // Local player (not us) died
                        if (ComponentManager<IArchipelagoLink>.Value.IsDeathLinkEnabled())
                        {
                            SendDeathLink(deathLink.Reason);
                            RAPI.GetLocalPlayer().Stats.Damage(99999, Vector3.zero, Vector3.zero, EntityType.None, true);
                        }
                        else
                        {
                            Logger.Debug("Received DeathLink from other player, but DeathLink is not enabled. Ignoring.");
                        }
                        return true;
                    case ArchipelagoDataMessage _:
                    case ItemReceivedMessage _:
                    case DeathLinkReceivedMessage _:
                        Logger.Trace($"Received packet {message.GetType().Name} from {from}, ignoring (host)");
                        return true;
                    default:
                        Logger.Debug($"Unknown packet received ({from}, {message.GetType().Name})");
                        return false;
                }
            }
            else
            {
                Logger.Debug($"Packet received ({message.GetType().Name})");
                switch (message)
                {
                    case ArchipelagoDataMessage archipelagoData:
                        Logger.Trace("AP data received");
                        ComponentManager<ItemTracker>.Value.ResetProgressives();
                        ComponentManager<ArchipelagoDataManager>.Value.ItemIdToName = archipelagoData.ItemIdToName;
                        ComponentManager<ArchipelagoDataManager>.Value.PlayerIdToName = archipelagoData.PlayerIdToName;
                        ComponentManager<ArchipelagoDataManager>.Value.SlotData = _deserializeSlotData(archipelagoData.SlotDataJson);
                        var currentItemIndeces = archipelagoData.PlayerItemIndeces ?? new Dictionary<long, int>();
                        _debugDictionary(ComponentManager<ArchipelagoDataManager>.Value.ItemIdToName);
                        _debugDictionary(ComponentManager<ArchipelagoDataManager>.Value.PlayerIdToName);
                        _debugDictionary(ComponentManager<ArchipelagoDataManager>.Value.SlotData);
                        _debugDictionary(currentItemIndeces);
                        if (currentItemIndeces.TryGetValue((long)RAPI.GetLocalPlayer().steamID.Id, out int currentIndex))
                        {
                            Logger.Debug($"Restored item index {currentIndex} for local player from host data");
                            ComponentManager<ItemTracker>.Value.CurrentReceivedItemIndex = currentIndex;
                        }
                        else
                        {
                            Logger.Debug("No prior item index for local player in host data; assuming none received");
                        }
                        return true;
                    case ItemReceivedMessage itemReceived:
                        Logger.Trace("AP item(s) received");
                        var itemIds = itemReceived.ItemIds;
                        var locationIds = itemReceived.LocationIds;
                        var playerIds = itemReceived.PlayerIds;
                        var currentItemIndexes = itemReceived.ItemIndeces;
                        if (itemIds == null || locationIds == null || playerIds == null || currentItemIndexes == null)
                        {
                            Logger.Warn($"Error processing ItemReceived packet -- one or more lists are null ({itemIds != null}, {locationIds != null}, {playerIds != null}, {currentItemIndexes != null})");
                        }
                        else if (itemIds.Count == locationIds.Count
                            && itemIds.Count == playerIds.Count
                            && itemIds.Count == currentItemIndexes.Count)
                        {
                            Logger.Trace("Item data received");
                            for (int i = 0; i < itemIds.Count; i++)
                            {
                                ComponentManager<ItemTracker>.Value.RaftItemUnlockedForCurrentWorld(itemIds[i],
                                    locationIds[i],
                                    playerIds[i],
                                    currentItemIndexes[i]);
                            }
                        }
                        else
                        {
                            Logger.Error($"Field counts differ, dropping item packet: {itemIds.Count}, {locationIds.Count}, {playerIds.Count}, {currentItemIndexes.Count}");
                        }
                        return true;
                    case DeathLinkReceivedMessage _:
                        Logger.Debug($"DeathLink received, killing local player");
                        RAPI.GetLocalPlayer().Stats.Damage(99999, Vector3.zero, Vector3.zero, EntityType.None, true);
                        return true;
                    case RequestResyncMessage _:
                    case DeathLinkReasonMessage _:
                        Logger.Trace($"Ignoring resync/deathlink request (not host)");
                        return true;
                    default:
                        Logger.Debug($"Unknown packet received ({from}, {message.GetType().Name})");
                        return false;
                }
            }
        }

        public void SaveWorldData()
        {
            if (ComponentManager<RaftipelagoMod>.Value == null || !ComponentManager<RaftipelagoMod>.Value.IsExtraSettingsLoaded())
            {
                return;
            }
            var serialized = new Dictionary<string, string>();
            foreach (var kvp in PlayerItemIndeces)
            {
                serialized[kvp.Key.ToString()] = kvp.Value.ToString();
            }
            Logger.Trace($"Saving item indices to Extra Settings API ({PlayerItemIndeces.Count} players)");
            ComponentManager<RaftipelagoMod>.Value.SaveWorldItemData(serialized);
        }

        public void LoadWorldData()
        {
            if (ComponentManager<RaftipelagoMod>.Value == null || !ComponentManager<RaftipelagoMod>.Value.IsExtraSettingsLoaded())
            {
                Logger.Warn("Extra Settings API not loaded -- received item index saving is disabled. Install/enable Extra Settings API to avoid re-awarding Resource Packs on reload.");
                return;
            }
            var stored = ComponentManager<RaftipelagoMod>.Value.LoadWorldItemData();
            var restored = new Dictionary<long, int>();
            foreach (var kvp in stored)
            {
                if (long.TryParse(kvp.Key, out long playerId) && int.TryParse(kvp.Value, out int itemIndex))
                {
                    restored[playerId] = itemIndex;
                }
            }
            PlayerItemIndeces = restored;
            long localKey = (long)RAPI.GetLocalPlayer().steamID.Id;
            if (PlayerItemIndeces.TryGetValue(localKey, out int localItemIndex))
            {
                Logger.Debug($"Restored item index {localItemIndex} for local player from Extra Settings API ({PlayerItemIndeces.Count} players)");
                ComponentManager<ItemTracker>.Value.CurrentReceivedItemIndex = localItemIndex;
            }
            else
            {
                Logger.Debug($"No saved item index for local player ({PlayerItemIndeces.Count} players stored)");
            }
        }

        // Occurs on the host when a client joins. If a Message is returned (not null), it will be included the
        // world load messages sent to the client.
        public Message SaveRemoteData()
        {
            return new RMessage(ComponentManager<RaftipelagoMod>.Value.slug, _generateArchipelagoDataMessage());
        }

        // Occurs on the client when the host sends the world load data (only occurs if the sent data contains
        // a message provided by the ModUtils_SaveRemoteData method). The message given to this method will be
        // the one this mod sent with the world load data. Note: This will be run before any of the other world
        // data is recieved.
        public void LoadRemoteData(Message message)
        {
            Logger.Debug("Received remote world-load message");
            if (message is RMessage rMessage)
            {
                HandleMessage(rMessage.realMsg, default(Network_UserId));
            }
            else
            {
                Logger.Warn($"Unexpected remote world-load message type: {message?.GetType().Name}");
            }
        }

        public void RequestArchipelagoDataResync()
        {
            sendMessage(new RequestResyncMessage());
        }

        public void SendAllArchipelagoData(Network_UserId? playerId = null)
        {
            Logger.Trace($"Resyncing Archipelago data with {playerId?.ToString() ?? "everyone"}");
            sendMessage(_generateArchipelagoDataMessage(), playerId);
            var itemInfo = ComponentManager<IArchipelagoLink>.Value.GetAllItems();
            if (itemInfo != null)
            {
                Logger.Debug("Sending all items");
                SendItems(itemInfo.itemIds, itemInfo.locationIds, itemInfo.playerIDs, itemInfo.itemIndices, playerId);
            }
            else
            {
                Logger.Debug("Unable to send all items");
            }
        }

        public void SendItem(long itemId, long locationId, int playerId, int itemIndex)
        {
            Logger.Debug($"Sending item {itemId} :: {locationId} :: {playerId} :: {itemIndex}");
            SendItems(
                new List<long>() { itemId },
                new List<long>() { locationId },
                new List<int>() { playerId },
                new List<int>() { itemIndex }
            );
        }

        public void SendItems(List<long> itemIds, List<long> locationIds, List<int> playerIds, List<int> itemIndeces, Network_UserId? playerId = null)
        {
            if (itemIndeces.Count > 0)
            {
                Logger.Debug($"Sending item count {itemIds.Count}");
                _updateConnectedPlayerItemIndeces(itemIndeces.Max());
                sendMessage(new ItemReceivedMessage
                {
                    ItemIds = itemIds,
                    LocationIds = locationIds,
                    PlayerIds = playerIds,
                    ItemIndeces = itemIndeces
                }, playerId);
            }
            else
            {
                Logger.Debug($"No items to send, not sending packet");
            }
        }

        public void SendDeathLink(string message)
        {
            if (Raft_Network.IsHost)
            {
                if (ComponentManager<IArchipelagoLink>.Value.IsDeathLinkEnabled())
                {
                    Logger.Debug($"Sending DeathLink");
                    sendMessage(new DeathLinkReceivedMessage());
                    if (!string.IsNullOrWhiteSpace(message))
                    {
                        Logger.Debug($"And DeathLinking Archipelago ({message})");
                        ComponentManager<IArchipelagoLink>.Value.SendDeathLinkPacket(message);
                    }
                }
                else
                {
                    Logger.Debug("DeathLink requested by local host, but DeathLink is not enabled. Ignoring.");
                }
            }
            else if (!string.IsNullOrWhiteSpace(message))
            {
                Logger.Debug($"Sending DeathLink sync ({message})");
                sendMessage(new DeathLinkReasonMessage { Reason = message });
            }
            else
            {
                Logger.Warn($"SendDeathLink -- not host, invalid message");
            }
        }

        private void _debugDictionary<T, U>(Dictionary<T, U> toPrint)
        {
            if (toPrint != null)
            {
                Logger.Debug("Dictionary is not null");
                foreach (var kvp in toPrint)
                {
                    Logger.Trace($"{kvp.Key}: {kvp.Value}");
                }
            }
            else
            {
                Logger.Debug("Dictionary is null");
            }
        }

        private void sendMessage(object message, Network_UserId? playerId = null)
        {
            if (playerId != null)
            {
                ComponentManager<RaftipelagoMod>.Value.SendNetworkMessageToPlayer(message, playerId.Value);
            }
            else
            {
                ComponentManager<RaftipelagoMod>.Value.SendNetworkMessage(message);
            }
        }

        private void _updateConnectedPlayerItemIndeces(int itemIndex)
        {
            Logger.Trace("Update player indeces");
            long localKey = (long)RAPI.GetLocalPlayer().steamID.Id;
            _updatePlayerItemIndex(localKey, itemIndex);
            foreach (var player in ComponentManager<Raft_Network>.Value.remoteUsers)
            {
                Logger.Trace($"{player.Value.steamID} ({RAPI.GetUsernameFromUserID(player.Value.steamID)}) => {itemIndex}");
                _updatePlayerItemIndex((long)player.Value.steamID.Id, itemIndex);
            }
            SaveWorldData();
        }

        private void _updatePlayerItemIndex(long playerId, int itemIndex)
        {
            PlayerItemIndeces.TryGetValue(playerId, out int currentIndex);
            PlayerItemIndeces[playerId] = Math.Max(itemIndex, currentIndex);
        }

        private static string _serializeSlotData(Dictionary<string, object> slotData)
        {
            return Newtonsoft.Json.JsonConvert.SerializeObject(slotData ?? new Dictionary<string, object>());
        }

        private static Dictionary<string, object> _deserializeSlotData(string slotDataJson)
        {
            try
            {
                if (string.IsNullOrEmpty(slotDataJson))
                {
                    return new Dictionary<string, object>();
                }
                return Newtonsoft.Json.JsonConvert.DeserializeObject<Dictionary<string, object>>(slotDataJson);
            }
            catch (Exception ex)
            {
                Logger.Error($"Failed to deserialize slot data: {ex.Message}");
                return new Dictionary<string, object>();
            }
        }

        // NOTE: This currently assumes that all item data is sent IMMEDIATELY after this packet.
        // Receiving this packet as a non-host will reset progressive data.
        private ArchipelagoDataMessage _generateArchipelagoDataMessage()
        {
            Logger.Trace("_generateArchipelagoDataMessage");
            var allItemIds = ComponentManager<IArchipelagoLink>.Value.GetAllItemIds();
            var allPlayerIds = ComponentManager<IArchipelagoLink>.Value.GetAllPlayerIds();
            var lastLoadedSlotData = ComponentManager<IArchipelagoLink>.Value.GetLastLoadedSlotData();
            _debugDictionary(allItemIds);
            _debugDictionary(allPlayerIds);
            _debugDictionary(lastLoadedSlotData);
            _debugDictionary(PlayerItemIndeces);
            return new ArchipelagoDataMessage
            {
                ItemIdToName = allItemIds,
                PlayerIdToName = allPlayerIds,
                SlotDataJson = _serializeSlotData(lastLoadedSlotData),
                PlayerItemIndeces = PlayerItemIndeces
            };
        }
    }

    [Serializable]
    public class ArchipelagoDataMessage
    {
        public Dictionary<long, string> ItemIdToName;
        public Dictionary<int, string> PlayerIdToName;
        public string SlotDataJson;
        public Dictionary<long, int> PlayerItemIndeces;
    }

    [Serializable]
    public class ItemReceivedMessage
    {
        public List<long> ItemIds;
        public List<long> LocationIds;
        public List<int> PlayerIds;
        public List<int> ItemIndeces;
    }

    [Serializable]
    public class DeathLinkReceivedMessage
    {
    }

    [Serializable]
    public class RequestResyncMessage
    {
    }

    [Serializable]
    public class DeathLinkReasonMessage
    {
        public string Reason;
    }
}
