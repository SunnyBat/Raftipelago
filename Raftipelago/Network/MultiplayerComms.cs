﻿using Raftipelago.Data;
using Steamworks;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEngine;

namespace Raftipelago.Network
{
    public class MultiplayerComms
    {
        private const string ModUtils_OldSlug = "2.1.0"; // Effectively versioning for ModUtils RGD data
        private const NetworkChannel ModUtils_Channel = (NetworkChannel)47000;
        private const int GenericMessageID = 47001;
        private Dictionary<long, int> PlayerItemIndeces = new Dictionary<long, int>();
        private Type _Type_RGD_Raftipelago;
        private ConstructorInfo _ConstructorInfo_RGD_Raftipelago;
        private FieldInfo _FieldInfo_RGD_Raftipelago_playerIndeces;

        public void RegisterData()
        {
            Assembly raftipelagoTypesAssembly = ComponentManager<AssemblyManager>.Value.GetAssembly(AssemblyManager.RaftipelagoTypesAssembly);
            _Type_RGD_Raftipelago = raftipelagoTypesAssembly.GetType("RaftipelagoTypes.RGD_Raftipelago");
            _ConstructorInfo_RGD_Raftipelago = _Type_RGD_Raftipelago.GetConstructor(new Type[] { typeof(Dictionary<long, int>) });
            _FieldInfo_RGD_Raftipelago_playerIndeces = _Type_RGD_Raftipelago.GetField("Raftipelago_PlayerCurrentItemIndeces", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.GetField | BindingFlags.Instance);
            ModUtils_RegisterSerializer(typeof(Dictionary<long, int>), SerializeDictionary<long, int>, DeserializeDictionary<long, int>);
            ModUtils_RegisterSerializer(typeof(Dictionary<long, string>), SerializeDictionary<long, string>, DeserializeDictionary<long, string>);
            ModUtils_RegisterSerializer(typeof(Dictionary<int, string>), SerializeDictionary<int, string>, DeserializeDictionary<int, string>);
            ModUtils_RegisterSerializer(typeof(Dictionary<string, object>), SerializeDictionary<string, object>, DeserializeDictionary<string, object>);
            ModUtils_RegisterSerializer(typeof(List<long>), SerializeList<long>, DeserializeList<long>);
            ModUtils_RegisterSerializer(typeof(List<int>), SerializeList<int>, DeserializeList<int>);
        }

        // Occurs when a network message is recieved on one of the network channels your mod is set to listen to.
        // Return type can be void or bool. If the return type is null you can return true to indicate that your
        // mod handled the message, if the method is void or returns false then the message will continue to be
        // passed to other mods listening to the network channel.
        private bool ModUtils_MessageRecieved(CSteamID steamID, NetworkChannel channel, Message message)
        {
            // Channel check isn't needed, but adding anyways
            // Host ignores all AP-related packets, as the host is the one initially receiving them
            if (message == null)
            {
                Logger.Warn($"Null message received ({steamID}, {channel})");
            }
            else if (channel != ModUtils_Channel)
            {
                Logger.Debug($"Invalid message channel received ({steamID}, {channel}, {message})");
            }
            else if (Raft_Network.IsHost)
            {
                Logger.Debug($"Packet received ({message.Type})");
                var messageValues = ModUtils_GetGenericMessageValues(message);
                switch (message.Type)
                {
                    case RaftipelagoMessageTypes.REQUEST_RESYNC:
                        // Don't need to validate message length, we have all the data we need so just send it
                        SendAllArchipelagoData(steamID);
                        break;
                    case RaftipelagoMessageTypes.DEATHLINK_REASON: // Local player (not us) died
                        _validateObjectArrayAndRun(messageValues, 1, () =>
                        {
                            if (ComponentManager<IArchipelagoLink>.Value.IsDeathLinkEnabled())
                            {
                                SendDeathLink((string)ModUtils_GetGenericMessageValues(message)[0]);
                                RAPI.GetLocalPlayer().Stats.Damage(99999, Vector3.zero, Vector3.zero, EntityType.None);
                            }
                            else
                            {
                                Logger.Debug("Received DeathLink from other player, but DeathLink is not enabled. Ignoring.");
                            }
                        }, $"Received DEATHLINK_REASON but had invalid amount of values ({messageValues?.Length})");
                        break;
                    case RaftipelagoMessageTypes.ARCHIPELAGO_DATA:
                    case RaftipelagoMessageTypes.ITEM_RECEIVED:
                    case RaftipelagoMessageTypes.DEATHLINK_RECEIVED:
                        Logger.Trace($"Received packet {message.Type} from {steamID}, ignoring");
                        break;
                    default:
                        Logger.Debug($"Unknown packet received ({steamID}, {message.Type})");
                        break;
                }
            }
            else if (!Raft_Network.IsHost && channel == ModUtils_Channel)
            {
                Logger.Debug($"Packet received ({message.Type})");
                var messageValues = ModUtils_GetGenericMessageValues(message);
                switch (message.Type)
                {
                    case RaftipelagoMessageTypes.ARCHIPELAGO_DATA:
                        _validateObjectArrayAndRun(messageValues, 4, () =>
                        {
                            Logger.Trace("AP data received");
                            ComponentManager<ItemTracker>.Value.ResetProgressives();
                            ComponentManager<ArchipelagoDataManager>.Value.ItemIdToName = (Dictionary<long, string>)messageValues[0];
                            ComponentManager<ArchipelagoDataManager>.Value.PlayerIdToName = (Dictionary<int, string>)messageValues[1];
                            ComponentManager<ArchipelagoDataManager>.Value.SlotData = (Dictionary<string, object>)messageValues[2];
                            var currentItemIndeces = (Dictionary<long, int>)messageValues[3];
                            _debugDictionary(ComponentManager<ArchipelagoDataManager>.Value.ItemIdToName);
                            _debugDictionary(ComponentManager<ArchipelagoDataManager>.Value.PlayerIdToName);
                            _debugDictionary(ComponentManager<ArchipelagoDataManager>.Value.SlotData);
                            _debugDictionary(currentItemIndeces);
                            if (currentItemIndeces.TryGetValue((long)RAPI.GetLocalPlayer().steamID.m_SteamID, out int currentIndex))
                            {
                                Logger.Debug($"Item index set to {currentIndex}");
                                ComponentManager<ItemTracker>.Value.CurrentReceivedItemIndex = currentIndex;
                            }
                            else
                            {
                                Logger.Debug("No item index received for local player; assuming no items previously received");
                            }
                        }, $"AP data received, but invalid message values given ({messageValues?.Length})");
                        break;
                    case RaftipelagoMessageTypes.ITEM_RECEIVED:
                        _validateObjectArrayAndRun(messageValues, 4, () =>
                        {
                            Logger.Trace("AP item(s) received");
                            var itemIds = (List<long>)messageValues[0];
                            var locationIds = (List<long>)messageValues[1];
                            var playerIds = (List<int>)messageValues[2];
                            var currentItemIndexes = (List<int>)messageValues[3];
                            if (itemIds == null || locationIds == null || playerIds == null || currentItemIndexes == null)
                            {
                                Logger.Warn($"Error processing ItemReceived packet -- one or more dictionaries are null ({itemIds != null}, {locationIds != null}, {playerIds != null}, {currentItemIndexes != null})");
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
                        }, $"AP item received, but invalid message values given ({messageValues?.Length})");
                        break;
                    case RaftipelagoMessageTypes.DEATHLINK_RECEIVED:
                        Logger.Debug($"DeathLink received, killing local player");
                        RAPI.GetLocalPlayer().Stats.Damage(99999, Vector3.zero, Vector3.zero, EntityType.None);
                        break;
                    case RaftipelagoMessageTypes.REQUEST_RESYNC:
                    case RaftipelagoMessageTypes.DEATHLINK_REASON:
                        Logger.Trace($"Ignoring resync/deathlink request (not host)");
                        break;
                    default:
                        Logger.Debug($"Unknown packet received ({steamID}, {message.Type})");
                        break;
                }
            }
            return true;
        }

        private void _validateObjectArrayAndRun(object[] messageValues, int expectedMessageValueLength, Action toRunIfValid, string messageIfInvalid)
        {
            if (messageValues?.Length == expectedMessageValueLength)
            {
                toRunIfValid();
            }
            else
            {
                Logger.Warn(messageIfInvalid);
            }
        }

        // The world save data is stored by mod slug so changing your mod's slug can cause the data to not be loaded.
        // If you include this field and set it to your mod's old slug then data saved while using the old slug should still be loaded.
        ///static string ModUtils_OldSlug = "old-mod-slug";

        // Occurs on the host when the world is saved. If a RGD is returned (not null), it will be saved to the world.
        RGD ModUtils_SaveLocalData()
        {
            if (PlayerItemIndeces.TryGetValue((long)RAPI.GetLocalPlayer().steamID.m_SteamID, out int dbgIndex))
            {
                Logger.Trace("Save Item index: " + dbgIndex);
            }
            else
            {
                Logger.Trace("Save Item index: (not present)");
            }
            var rgdObj = _ConstructorInfo_RGD_Raftipelago.Invoke(new object[] { PlayerItemIndeces });
            return (RGD) rgdObj;
        }

        // Occurs on the host when a world starts loading (only occurs if the save contains an RGD saved to
        // the world by the ModUtils_SaveLocalData method). The RGD given to this method will be the one this
        // mod saved to the world. Note: This will be run before any of the other world data is loaded.
        void ModUtils_LoadLocalData(RGD data)
        {
            if (data?.GetType() == _Type_RGD_Raftipelago)
            {
                Logger.Trace("Loading RGD data");
                PlayerItemIndeces = (Dictionary<long, int>)_FieldInfo_RGD_Raftipelago_playerIndeces.GetValue(data);
                if (PlayerItemIndeces != null)
                {
                    if (PlayerItemIndeces.TryGetValue((long)RAPI.GetLocalPlayer().steamID.m_SteamID, out int localItemIndex))
                    {
                        Logger.Debug("Local load Item index: " + localItemIndex);
                        ComponentManager<ItemTracker>.Value.CurrentReceivedItemIndex = localItemIndex;
                    }
                    else
                    {
                        Logger.Warn($"Failed to load local item index ({PlayerItemIndeces.Count} items)");
                    }
                    _debugDictionary(PlayerItemIndeces);
                }
                else
                {
                    Logger.Warn($"Failed to load PlayerItemIndices");
                    PlayerItemIndeces = new Dictionary<long, int>();
                }
            }
            else
            {
                Logger.Error("Unknown RGD type attempted to load: " + data?.GetType());
            }
        }

        // Occurs on the host when a client joins. If a Message is returned (not null), it will be included the
        // world load messages sent to the client.
        Message ModUtils_SaveRemoteData()
        {
            return _generateArchipelagoDataMessage();
        }

        // Occurs on the client when the host sends the world load data (only occurs if the sent data contains
        // a message provided by the ModUtils_SaveRemoteData method). The message given to this method will be
        // the one this mod sent with the world load data. Note: This will be run before any of the other world
        // data is recieved.
        void ModUtils_LoadRemoteData(Message message)
        {
            Logger.Debug("Received ModUtils remote message");
            ModUtils_MessageRecieved(default(CSteamID), ModUtils_Channel, message);
        }

        public void RequestArchipelagoDataResync()
        {
            sendMessage(CreateGenericMessage(RaftipelagoMessageTypes.REQUEST_RESYNC));
        }

        public void SendAllArchipelagoData(CSteamID? playerId = null)
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

        public void SendItems(List<long> itemIds, List<long> locationIds, List<int> playerIds, List<int> itemIndeces, CSteamID? steamID = null)
        {
            if (itemIndeces.Count > 0)
            {
                Logger.Debug($"Sending item count {itemIds.Count}");
                _updateConnectedPlayerItemIndeces(itemIndeces.Max());
                sendMessage(CreateGenericMessage(RaftipelagoMessageTypes.ITEM_RECEIVED, new object[] { itemIds, locationIds, playerIds, itemIndeces }), steamID);
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
                    sendMessage(CreateGenericMessage(RaftipelagoMessageTypes.DEATHLINK_RECEIVED));
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
                sendMessage(CreateGenericMessage(RaftipelagoMessageTypes.DEATHLINK_REASON, new object[] { message }));
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

        private void sendMessage(Message message, CSteamID? steamID = null)
        {
            if (steamID != null)
            {
                ComponentManager<Raft_Network>.Value.SendP2P((CSteamID) steamID, message, EP2PSend.k_EP2PSendReliable, ModUtils_Channel);
            }
            else
            {
                RAPI.SendNetworkMessage(message, channel: (int)ModUtils_Channel);
            }
        }

        // Not implemented by ModUtils, just a wrapper
        private static Message CreateGenericMessage(Messages messageType)
        {
            return CreateGenericMessage(messageType, new object[0]);
        }

        // Not implemented by ModUtils, just a wrapper
        private static Message CreateGenericMessage(Messages messageType, object[] values)
        {
            return ModUtils_CreateGenericMessage(messageType, GenericMessageID, values);
        }

        private static Message ModUtils_CreateGenericMessage(Messages messageType, int genericId, object[] values)
        {
            // Stub method will be replaced with ModUtils implementation once this object has been created. Do not call
            // this in the constructor; trigger this on mod start
            throw new NotImplementedException("ModUtils did not replace ModUtils_CreateGenericMessage() -- ModUtils likely not loaded.");
        }

        private static object[] ModUtils_GetGenericMessageValues(Message message)
        {
            // Stub method will be replaced with ModUtils implementation once this object has been created. Do not call
            // this in the constructor; trigger this on mod start
            throw new NotImplementedException("ModUtils did not replace ModUtils_GetGenericMessageValues() -- ModUtils likely not loaded.");
        }

        private static void ModUtils_RegisterSerializer(Type type, Func<object, byte[]> toBytes, Func<byte[], object> fromBytes)
        {
            // Stub method will be replaced with ModUtils implementation once this object has been created. Do not call
            // this in the constructor; trigger this on mod start
            throw new NotImplementedException("ModUtils did not replace RegisterSerializer() -- ModUtils likely not loaded.");
        }

        private void _updateConnectedPlayerItemIndeces(int itemIndex)
        {
            Logger.Trace("Update player indeces");
            _updatePlayerItemIndex((long)RAPI.GetLocalPlayer().steamID.m_SteamID, itemIndex);
            foreach (var player in ComponentManager<Raft_Network>.Value.remoteUsers)
            {
                Logger.Trace($"{player.Value.steamID} ({RAPI.GetUsernameFromSteamID(player.Value.steamID)}) => {itemIndex}");
                _updatePlayerItemIndex((long)player.Value.steamID.m_SteamID, itemIndex);
            }
        }

        private void _updatePlayerItemIndex(long playerId, int itemIndex)
        {
            PlayerItemIndeces.TryGetValue(playerId, out int currentIndex);
            PlayerItemIndeces[playerId] = Math.Max(itemIndex, currentIndex);
        }

        private static byte[] SerializeDictionary<T, U>(object toSerialize)
        {
            var serializedObject = Newtonsoft.Json.JsonConvert.SerializeObject(toSerialize);
            Logger.Debug("Serialized dictionary to " + serializedObject);
            return Encoding.UTF8.GetBytes(serializedObject);
        }

        private static Dictionary<T, U> DeserializeDictionary<T, U>(byte[] toDeserialize)
        {
            try
            {
                var serializedString = Encoding.UTF8.GetString(toDeserialize);
                Logger.Debug("Deserializing " + serializedString);
                return Newtonsoft.Json.JsonConvert.DeserializeObject<Dictionary<T, U>>(serializedString);
            }
            catch (Exception ex)
            {
                Logger.Error($"Failed to deserialize Dictionary: {ex.Message}");
                return new Dictionary<T, U>();
            }
        }

        private static byte[] SerializeList<T>(object toSerialize)
        {
            var serializedObject = Newtonsoft.Json.JsonConvert.SerializeObject(toSerialize);
            Logger.Debug("Serialized list to " + serializedObject);
            return Encoding.UTF8.GetBytes(serializedObject);
        }

        private static List<T> DeserializeList<T>(byte[] toDeserialize)
        {
            try
            {
                var serializedString = Encoding.UTF8.GetString(toDeserialize);
                Logger.Debug("Deserializing " + serializedString);
                return Newtonsoft.Json.JsonConvert.DeserializeObject<List<T>>(serializedString);
            }
            catch (Exception ex)
            {
                Logger.Error($"Failed to deserialize List: {ex.Message}");
                return new List<T>();
            }
        }

        // NOTE: This currently assumes that all item data is sent IMMEDIATELY after this packet.
        // Receiving this packet as a non-host will reset progressive data.
        private Message _generateArchipelagoDataMessage()
        {
            Logger.Trace("_generateArchipelagoDataMessage");
            var allItemIds = ComponentManager<IArchipelagoLink>.Value.GetAllItemIds();
            var allPlayerIds = ComponentManager<IArchipelagoLink>.Value.GetAllPlayerIds();
            var lastLoadedSlotData = ComponentManager<IArchipelagoLink>.Value.GetLastLoadedSlotData();
            _debugDictionary(allItemIds);
            _debugDictionary(allPlayerIds);
            _debugDictionary(lastLoadedSlotData);
            _debugDictionary(PlayerItemIndeces);
            return CreateGenericMessage(RaftipelagoMessageTypes.ARCHIPELAGO_DATA, new object[]
            {
                allItemIds,
                allPlayerIds,
                lastLoadedSlotData,
                PlayerItemIndeces
            });
        }

        public class RaftipelagoMessageTypes
        {
            public const Messages ARCHIPELAGO_DATA = (Messages)4571;
            public const Messages ITEM_RECEIVED = (Messages)4572;
            public const Messages DEATHLINK_RECEIVED = (Messages)4573;
            public const Messages REQUEST_RESYNC = (Messages)4574;
            public const Messages DEATHLINK_REASON = (Messages)4575;
        }
    }
}
