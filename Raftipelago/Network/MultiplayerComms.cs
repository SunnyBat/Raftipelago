using Raftipelago.Data;
using Steamworks;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;

namespace Raftipelago.Network
{
    public class MultiplayerComms
    {
        private const string ModUtils_OldSlug = "2.1.0"; // Effectively versioning for ModUtils RGD data
        private const NetworkChannel ModUtils_Channel = (NetworkChannel)47000;
        private Dictionary<long, int> PlayerItemIndeces = new Dictionary<long, int>();
        private Type _Type_RGD_Raftipelago;
        private Type _Type_Message_ArchipelagoData;
        private Type _Type_Message_ArchipelagoItemsReceived;
        private Type _Type_Message_ArchipelagoDeathLink;
        private FieldInfo _FieldInfo_playerIndeces;
        private PropertyInfo _PropertyInfo_Message_ArchipelagoData_ItemIdToNameMap;
        private PropertyInfo _PropertyInfo_Message_ArchipelagoData_PlayerIdToNameMap;
        private PropertyInfo _PropertyInfo_Message_ArchipelagoData_SlotData;
        private PropertyInfo _PropertyInfo_Message_ArchipelagoData_CurrentReceivedItemIndeces;
        private PropertyInfo _PropertyInfo_Message_ArchipelagoItemsReceived_ItemIds;
        private PropertyInfo _PropertyInfo_Message_ArchipelagoItemsReceived_LocationIds;
        private PropertyInfo _PropertyInfo_Message_ArchipelagoItemsReceived_PlayerIds;
        private PropertyInfo _PropertyInfo_Message_ArchipelagoItemsReceived_CurrentItemIndexes;
        public MultiplayerComms()
        {
        }

        public void RegisterData()
        {
            Assembly raftipelagoTypesAssembly = ComponentManager<AssemblyManager>.Value.GetAssembly(AssemblyManager.RaftipelagoTypesAssembly);
            _Type_RGD_Raftipelago = raftipelagoTypesAssembly.GetType("RaftipelagoTypes.RGD_Raftipelago");
            _Type_Message_ArchipelagoData = raftipelagoTypesAssembly.GetType("RaftipelagoTypes.Message_ArchipelagoData");
            _Type_Message_ArchipelagoItemsReceived = raftipelagoTypesAssembly.GetType("RaftipelagoTypes.Message_ArchipelagoItemsReceived");
            _Type_Message_ArchipelagoDeathLink = raftipelagoTypesAssembly.GetType("RaftipelagoTypes.Message_ArchipelagoDeathLink");
            _FieldInfo_playerIndeces = _Type_RGD_Raftipelago.GetField("Raftipelago_PlayerCurrentItemIndeces", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.GetField | BindingFlags.Instance);
            _PropertyInfo_Message_ArchipelagoData_ItemIdToNameMap = _Type_Message_ArchipelagoData.GetProperty("ItemIdToNameMap");
            _PropertyInfo_Message_ArchipelagoData_PlayerIdToNameMap = _Type_Message_ArchipelagoData.GetProperty("PlayerIdToNameMap");
            _PropertyInfo_Message_ArchipelagoData_SlotData = _Type_Message_ArchipelagoData.GetProperty("SlotData");
            _PropertyInfo_Message_ArchipelagoData_CurrentReceivedItemIndeces = _Type_Message_ArchipelagoData.GetProperty("CurrentReceivedItemIndeces");
            _PropertyInfo_Message_ArchipelagoItemsReceived_ItemIds = _Type_Message_ArchipelagoItemsReceived.GetProperty("ItemIds");
            _PropertyInfo_Message_ArchipelagoItemsReceived_LocationIds = _Type_Message_ArchipelagoItemsReceived.GetProperty("LocationIds");
            _PropertyInfo_Message_ArchipelagoItemsReceived_PlayerIds = _Type_Message_ArchipelagoItemsReceived.GetProperty("PlayerIds");
            _PropertyInfo_Message_ArchipelagoItemsReceived_CurrentItemIndexes = _Type_Message_ArchipelagoItemsReceived.GetProperty("CurrentItemIndexes");
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
            if (!Raft_Network.IsHost && channel == ModUtils_Channel)
            {
                switch (message.Type)
                {
                    case RaftipelagoMessageTypes.ARCHIPELAGO_DATA:
                        if (message.GetType() == _Type_Message_ArchipelagoData)
                        {
                            Logger.Debug("AP data received");
                            ComponentManager<ArchipelagoDataManager>.Value.ItemIdToName = (Dictionary<long, string>)_PropertyInfo_Message_ArchipelagoData_ItemIdToNameMap.GetValue(message);
                            ComponentManager<ArchipelagoDataManager>.Value.PlayerIdToName = (Dictionary<int, string>)_PropertyInfo_Message_ArchipelagoData_PlayerIdToNameMap.GetValue(message);
                            ComponentManager<ArchipelagoDataManager>.Value.SlotData = (Dictionary<string, object>)_PropertyInfo_Message_ArchipelagoData_SlotData.GetValue(message);
                            var currentItemIndeces = (Dictionary<long, int>)_PropertyInfo_Message_ArchipelagoData_CurrentReceivedItemIndeces.GetValue(message);
                            if (currentItemIndeces.TryGetValue((long)RAPI.GetLocalPlayer().steamID.m_SteamID, out int currentIndex))
                            {
                                Logger.Info("Item index set to " + currentIndex);
                                ComponentManager<ItemTracker>.Value.CurrentReceivedItemIndex = currentIndex;
                            }
                            else
                            {
                                Logger.Info("No item index received for local player; assuming no items previously received");
                            }
                        }
                        else
                        {
                            Logger.Error($"AP data received, but invalid message given ({message.GetType()})");
                        }
                        break;
                    case RaftipelagoMessageTypes.ITEM_RECEIVED:
                        if (message.GetType() == _Type_Message_ArchipelagoItemsReceived)
                        {
                            Logger.Debug("AP item(s) received");
                            var itemIds = (List<long>)_PropertyInfo_Message_ArchipelagoItemsReceived_ItemIds.GetValue(message);
                            var locationIds = (List<long>)_PropertyInfo_Message_ArchipelagoItemsReceived_LocationIds.GetValue(message);
                            var playerIds = (List<int>)_PropertyInfo_Message_ArchipelagoItemsReceived_PlayerIds.GetValue(message);
                            var currentItemIndexes = (List<int>)_PropertyInfo_Message_ArchipelagoItemsReceived_CurrentItemIndexes.GetValue(message);
                            if (itemIds.Count == locationIds.Count
                                && itemIds.Count == playerIds.Count
                                && itemIds.Count == currentItemIndexes.Count)
                            {
                                Logger.Debug("Valid data");
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
                        }
                        else
                        {
                            Logger.Error($"AP item received, but invalid message given ({message.GetType()})");
                        }
                        break;
                    case RaftipelagoMessageTypes.DEATHLINK_RECEIVED:
                        if (message.GetType() == _Type_Message_ArchipelagoDeathLink)
                        {
                            Logger.Trace($"DeathLink received, killing player");
                            RAPI.GetLocalPlayer().Kill();
                        }
                        else
                        {
                            Logger.Error($"AP DeathLink received, but invalid message given ({message.GetType()})");
                        }
                        break;
                }
            }
            return true;
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
                Logger.Trace("Save Item index (not present)");
            }
            var rgdObj = _Type_RGD_Raftipelago.GetConstructor(new Type[] { typeof(Dictionary<long, int>) }).Invoke(new object[] { PlayerItemIndeces });
            return (RGD) rgdObj;
        }

        // Occurs on the host when a world starts loading (only occurs if the save contains an RGD saved to
        // the world by the ModUtils_SaveLocalData method). The RGD given to this method will be the one this
        // mod saved to the world. Note: This will be run before any of the other world data is loaded.
        void ModUtils_LoadLocalData(RGD data)
        {
            if (data?.GetType() == _Type_RGD_Raftipelago)
            {
                Logger.Debug("Loading RGD data");
                PlayerItemIndeces = (Dictionary<long, int>)_FieldInfo_playerIndeces.GetValue(data);
                if (PlayerItemIndeces != null && PlayerItemIndeces.TryGetValue((long)RAPI.GetLocalPlayer().steamID.m_SteamID, out int localItemIndex))
                {
                    Logger.Trace("Load Item index: " + localItemIndex);
                    ComponentManager<ItemTracker>.Value.CurrentReceivedItemIndex = localItemIndex;
                }
                else
                {
                    Logger.Warn($"Failed to load item index ({PlayerItemIndeces?.Count} items)");
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
            ModUtils_MessageRecieved(default(CSteamID), ModUtils_Channel, message);
        }

        public void SendArchipelagoData()
        {
            sendMessage(_generateArchipelagoDataMessage());
        }

        public void SendItem(long itemId, long locationId, int playerId, int itemIndex)
        {
            _updateConnectedPlayerItemIndeces(itemIndex);
            sendMessage((Message)_Type_Message_ArchipelagoItemsReceived.GetConstructor(new Type[] { typeof(List<long>), typeof(List<long>), typeof(List<int>), typeof(List<int>) }).Invoke(new object[] { new List<long>() { itemId }, new List<long>() { locationId }, new List<int>() { playerId }, new List<int>() { itemIndex } }));
        }

        public void SendItems(List<long> itemIds, List<long> locationIds, List<int> playerIds, List<int> itemIndeces)
        {
            _updateConnectedPlayerItemIndeces(itemIndeces.Max());
            sendMessage((Message)_Type_Message_ArchipelagoItemsReceived.GetConstructor(new Type[] { typeof(List<long>), typeof(List<long>), typeof(List<int>), typeof(List<int>) }).Invoke(new object[] { itemIds, locationIds, playerIds, itemIndeces }));
        }

        public void SendDeathLink()
        {
            sendMessage((Message)_Type_Message_ArchipelagoDeathLink.GetConstructor(new Type[0]).Invoke(new object[0]));
        }

        private void sendMessage(Message message)
        {
            RAPI.SendNetworkMessage(message, channel: (int)ModUtils_Channel);
        }

        // This MUST be static
        private static void ModUtils_RegisterSerializer(Type type, Func<object, byte[]> toBytes, Func<byte[], object> fromBytes)
        {
            // Stub method will be replaced with ModUtils implementation once this object has been created. Do not call
            // this in the constructor; trigger this on mod start
            throw new NotImplementedException("ModUtils did not replace RegisterSerializer() -- mod likely not loaded.");
        }

        private void _updateConnectedPlayerItemIndeces(int itemIndex)
        {
            _updatePlayerItemIndex((long)RAPI.GetLocalPlayer().steamID.m_SteamID, itemIndex);
            foreach (var player in ComponentManager<Raft_Network>.Value.remoteUsers)
            {
                _updatePlayerItemIndex((long)player.Value.steamID.m_SteamID, itemIndex);
            }
        }

        private void _updatePlayerItemIndex(long playerId, int itemIndex)
        {
            PlayerItemIndeces.TryGetValue(playerId, out int currentIndex);
            PlayerItemIndeces[playerId] = Math.Max(itemIndex, currentIndex);
        }

        private Message _generateArchipelagoDataMessage()
        {
            var constructor = _Type_Message_ArchipelagoData.GetConstructor(new Type[] { typeof(Dictionary<long, string>), typeof(Dictionary<int, string>), typeof(Dictionary<string, object>), typeof(Dictionary<long, int>) });
            return (Message)constructor.Invoke(new object[] {ComponentManager<IArchipelagoLink>.Value.GetAllItemIds(),
                ComponentManager<IArchipelagoLink>.Value.GetAllPlayerIds(),
                ComponentManager<IArchipelagoLink>.Value.GetLastLoadedSlotData(),
                PlayerItemIndeces });
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
        private class RaftipelagoMessageTypes
        {
            public const Messages ARCHIPELAGO_DATA = (Messages)47500;
            public const Messages ITEM_RECEIVED = (Messages)47501;
            public const Messages DEATHLINK_RECEIVED = (Messages)47502;
        }
    }
}
